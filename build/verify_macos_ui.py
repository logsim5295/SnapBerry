"""Run real native-window UI checks on disposable GitHub macOS runners only.

Artifacts contain PNGs/JSON/logs, never app bundles or installers. Does not grant
permissions or change privacy settings; missing runner permissions are failures.
"""
import argparse
import json
import os
from pathlib import Path
import platform
import shutil
import struct
import subprocess
import sys
import tempfile
import zipfile

from verify_macos import require, verify_package
from package_macos_dmg import BUILD, extract_app, native_code_files, relocate_payload, verify_signature

SCENES = ("main", "about", "hotkeys", "settings", "editor", "compact")


def require_ci_host(arch):
    require(sys.platform == "darwin", "Native UI verification requires macOS")
    require(os.environ.get("GITHUB_ACTIONS") == "true" and os.environ.get("RUNNER_OS") == "macOS",
            "Interactive UI checks are restricted to GitHub Actions macOS")
    require(platform.machine().lower() == {"arm64":"arm64", "x64":"x86_64"}[arch], "Host CPU does not match package")


def verify_report(report, root, theme, sha):
    require(report.get("success") is True and report.get("nativeWindowServer") is True
            and report.get("headless") is False, "Missing successful native WindowServer evidence")
    require(report.get("sourceSha") == sha and bool(sha), "Incorrect source commit")
    require(report.get("theme") == theme, "Incorrect theme")
    require(report.get("normalTermination") is True and report.get("pid", 0) > 0, "App did not terminate normally")
    require(report.get("permissionSettingsModified") is False, "Unexpected privacy settings change")
    luminance = report.get("screenshots", {}).get("main", {}).get("canvasLuminance", -1)
    require(0 <= luminance < 0.4 if theme == "dark" else 0.65 < luminance <= 1, "Rendered canvas does not match theme")
    for scene in SCENES:
        evidence = report.get("screenshots", {}).get(scene, {})
        require(evidence.get("windowId", 0) > 0 and evidence.get("uniqueColors", 0) >= 24,
                f"Missing or blank native scene: {scene}")
        name = evidence.get("file", "")
        require(name and Path(name).name == name and name.endswith(".png"), f"Unsafe screenshot path: {scene}")
        path = root / name
        require(path.is_file(), f"Missing screenshot: {scene}")
        header = path.read_bytes()[:24]
        require(len(header) == 24 and header[:8] == b"\x89PNG\r\n\x1a\n" and header[12:16] == b"IHDR", f"Invalid PNG: {scene}")
        width, height = struct.unpack(">II", header[16:24])
        require(width == evidence.get("width") and height == evidence.get("height") and width >= 300 and height >= 250,
                f"Incorrect screenshot dimensions: {scene}")


def run(*args, **kwargs):
    return subprocess.run([str(arg) for arg in args], check=True, **kwargs)


def cleanup_owned(probe, app, settings, temporary, owned_settings):
    # A timed-out subprocess cannot execute its Swift defer. Independently stop
    # apps matching this unique bundle path before removing any owned files.
    if app is not None:
        run(probe, "--cleanup", app, timeout=30)
    if owned_settings:
        shutil.rmtree(settings)
    shutil.rmtree(temporary)


def verify_ui(package, arch, probe, output):
    require_ci_host(arch)  # Before any native command, user-data access or app launch.
    package, probe, output = package.resolve(), probe.resolve(), output.resolve()
    require(probe.is_file(), "Missing compiled UI probe")
    verify_package(package, arch)
    settings = Path.home() / "Library/Application Support/SnapBerry"
    require(not os.path.lexists(settings), "Refusing to overwrite existing SnapBerry settings or history")
    output.mkdir(parents=True, exist_ok=True)
    owned_settings = False
    temporary = Path(tempfile.mkdtemp(prefix="snapberry-native-ui-"))
    app = None
    try:
        with zipfile.ZipFile(package) as archive:
            app = extract_app(archive, temporary)
        relocate_payload(app)
        executable = app / "Contents/MacOS/SnapBerry.Desktop"
        for index, path in enumerate(native_code_files(app)):
            path.chmod(0o755)
            if path != executable:
                run("codesign", "--force", "--sign", "-", "--options", "runtime", "--timestamp=none",
                    "--identifier", f"com.ynsim.snapberry.native.{index}", path)
        run("codesign", "--force", "--sign", "-", "--options", "runtime", "--timestamp=none",
            "--entitlements", BUILD / "macos-entitlements.plist", app)
        verify_signature(app)
        settings.mkdir(parents=True); owned_settings = True
        for theme in ("light", "dark"):
            (settings / "desktop-settings.json").write_text(json.dumps({
                "Dark": theme == "dark", "Hotkeys": {"Enabled": False}, "HistoryVisible": True}), encoding="utf-8")
            if (settings / "Library").exists():
                shutil.rmtree(settings / "Library")
            run(probe, app, output, theme, timeout=240)
            report = json.loads((output / f"native-ui-{theme}.json").read_text(encoding="utf-8"))
            verify_report(report, output, theme, os.environ.get("GITHUB_SHA", ""))
            print(f"Native {theme} UI: all {len(SCENES)} windows/scenes passed", flush=True)
    finally:
        cleanup_owned(probe, app, settings, temporary, owned_settings)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package", type=Path)
    parser.add_argument("--arch", required=True, choices=("arm64", "x64"))
    parser.add_argument("--probe", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    verify_ui(args.package, args.arch, args.probe, args.output)


if __name__ == "__main__":
    main()
