"""Install a DMG into /Applications and test a real GUI launch on GitHub macOS only.

Explicitly authorized native CI test, including optional real OS feature tests.
Never changes Gatekeeper, quarantine, or the TCC database. Optional first-use
consent is approved exclusively through the real macOS UI on the CI VM.
"""

import argparse
from contextlib import nullcontext
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import tempfile

from package_macos_dmg import verify_signature
from verify_macos import require
from ci_macos_consent_account import consent_account

FEATURE_CASES = ("clipboard-paste", "clipboard-copy", "clipboard-duplicate",
                 "capture-display", "capture-region", "capture-window",
                 "global-display", "global-region", "global-window")
CONSENT_CHECKS = ("initialPromptObserved", "noCaptureBeforeApproval", "systemPromptActionPerformed",
                  "approvalUIConfirmed", "originalNormalTermination", "relaunchedViaLaunchServices",
                  "nativeFeaturesPassed", "approvedAppNormalTermination", "success")


def verify_consent(report):
    for name in CONSENT_CHECKS:
        require(report.get(name) is True, f"Missing or unsuccessful first-use consent check: {name}")
    require(report.get("settingsSwitchPressed") is True or report.get("directAllowClicked") is True,
            "No actual permission approval action was performed")
    require(report.get("tccDatabaseEdited") is False and report.get("launchMode") == "LaunchServices",
            "Consent must use the real UI and Launch Services")
    if report.get("authenticationRequired") is True:
        require(report.get("temporaryAccountAuthenticationCompleted") is True,
                "macOS consent authentication did not complete")


def verify_features(report):
    for name in FEATURE_CASES:
        require(report.get("cases", {}).get(name, {}).get("status") == "passed",
                f"Missing or unsuccessful native feature test: {name}")
    require(report.get("success") is True, "Native feature probe did not succeed")


def require_ci_host(arch):
    require(sys.platform == "darwin", "Installation test requires macOS")
    require(os.environ.get("GITHUB_ACTIONS") == "true" and os.environ.get("RUNNER_OS") == "macOS",
            "GUI installation test is restricted to GitHub Actions")
    require(platform.machine().lower() == {"arm64": "arm64", "x64": "x86_64"}[arch], "Host CPU does not match package")


def run(*args, **kwargs):
    return subprocess.run([str(arg) for arg in args], check=True, **kwargs)


def collect_diagnostics(output):
    for root in (Path.home() / "Library/Logs/DiagnosticReports", Path("/Library/Logs/DiagnosticReports")):
        if root.is_dir():
            for report in root.glob("SnapBerry*"):
                if report.is_file():
                    shutil.copy2(report, output / report.name)
    with (output / "app-system-log.txt").open("w") as log:
        subprocess.run(["/usr/bin/log", "show", "--last", "3m", "--style", "compact", "--predicate",
                        'process == "SnapBerry.Desktop"'], stdout=log, stderr=subprocess.STDOUT,
                       timeout=20, check=False)


def install_and_launch(dmg, arch, probe, output, features=None, consent=None):
    require_ci_host(arch)
    dmg, probe, output = dmg.resolve(), probe.resolve(), output.resolve()
    require(dmg.is_file() and probe.is_file(), "Missing DMG or compiled GUI probe")
    if features:
        features = features.resolve()
        require(features.is_file(), "Missing compiled native feature probe")
    if consent:
        consent = consent.resolve()
        require(consent.is_file() and features is not None, "Consent probe requires native feature probe")
    destination = Path("/Applications/SnapBerry.app")
    settings = Path.home() / "Library/Application Support/SnapBerry"
    require(not os.path.lexists(destination), "Refusing to overwrite an existing SnapBerry installation")
    require(not os.path.lexists(settings), "Refusing to overwrite existing SnapBerry settings or history")
    output.mkdir(parents=True, exist_ok=True)
    report = output / "gui-launch.json"
    installed = settings_created = probe_started = False
    try:
        with tempfile.TemporaryDirectory(prefix="snapberry-install-") as temporary:
            mount = Path(temporary) / "mounted"
            mount.mkdir()
            run("hdiutil", "attach", "-readonly", "-nobrowse", "-noautoopen", "-mountpoint", mount, dmg)
            try:
                require((mount / "Applications").is_symlink()
                        and (mount / "Applications").readlink() == destination.parent,
                        "DMG Applications link does not point to the installation directory")
                destination.mkdir()  # Atomic ownership claim; never overwrite another installation.
                installed = True
                run("/usr/bin/ditto", mount / "SnapBerry.app", destination)
                verify_signature(destination)
            finally:
                run("hdiutil", "detach", mount)
        print("Installation OK: copied from DMG to /Applications; signature verified; DMG detached", flush=True)
        settings.mkdir(parents=True)
        settings_created = True
        (settings / "desktop-settings.json").write_text(json.dumps({"Dark": False, "Hotkeys": {"Enabled": bool(features)}}), encoding="utf-8")
        probe_started = True
        feature_args = [features, settings / "Library", output] if features else []
        with consent_account() if consent else nullcontext(None) as credentials:
            if consent:
                feature_args.extend([consent, credentials])
            run(probe, destination, report, *feature_args, timeout=900 if consent else 600 if features else 120)
        result = json.loads(report.read_text(encoding="utf-8"))
        require(result.get("success") is True and result.get("normalTermination") is True,
                "GUI probe did not confirm a successful launch and termination")
        if features:
            verify_features(json.loads((output / "native-features.json").read_text(encoding="utf-8")))
        if consent:
            verify_consent(json.loads((output / "first-use-consent.json").read_text(encoding="utf-8")))
        print("Install / GUI smoke test OK: no shared .NET runtime, no mounted DMG dependencies", flush=True)
    finally:
        cleanup_ok = True
        if probe_started:
            try:
                run(probe, "--cleanup", destination, report, timeout=20)
            except (OSError, subprocess.SubprocessError) as error:
                cleanup_ok = False
                print(f"Test app cleanup failed: {error}", file=sys.stderr)
        try:
            if settings_created and (settings / "Library").is_dir():
                shutil.copytree(settings / "Library", output / "history", dirs_exist_ok=True)
            collect_diagnostics(output)
        except (OSError, subprocess.SubprocessError) as error:
            print(f"Diagnostic collection failed: {error}", file=sys.stderr)
        if cleanup_ok:
            if installed:
                shutil.rmtree(destination)
            if settings_created:
                shutil.rmtree(settings)
        require(cleanup_ok, "Owned test process could not be cleaned up")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("dmg", type=Path)
    parser.add_argument("--arch", choices=("arm64", "x64"), required=True)
    parser.add_argument("--probe", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--features", type=Path, help="Compiled native OS feature probe (CI only)")
    parser.add_argument("--consent", type=Path, help="Real first-use screen recording consent UI probe (CI only)")
    args = parser.parse_args()
    try:
        install_and_launch(args.dmg, args.arch, args.probe, args.output, args.features, args.consent)
    except (ValueError, OSError, KeyError, subprocess.SubprocessError) as error:
        print(f"Install / GUI smoke test failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
