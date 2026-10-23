"""Create and inspect a pre-signed trial DMG on macOS without opening Finder or the app.

Ad-hoc signing prepares the bundle; it is not Developer ID signing or notarization.
"""

import argparse
from pathlib import Path, PurePosixPath
import plistlib
import shutil
import subprocess
import sys
import tempfile
import unicodedata
import zipfile

from verify_macos import require, verify_package


BUILD = Path(__file__).resolve().parent
MACHO_MAGICS = (b"\xcf\xfa\xed\xfe", b"\xfe\xed\xfa\xcf", b"\xca\xfe\xba\xbe",
                b"\xbe\xba\xfe\xca", b"\xca\xfe\xba\xbf", b"\xbf\xba\xfe\xca")


def extract_app(archive, volume):
    for entry in archive.infolist():
        name = entry.orig_filename
        if not name.startswith("SnapBerry.app/"):
            continue
        relative = PurePosixPath(name)
        require(".." not in relative.parts and "\\" not in name and ":" not in name,
                f"Unsafe app path: {name}")
        target = volume.joinpath(*relative.parts)
        if entry.is_dir():
            target.mkdir(parents=True, exist_ok=True)
            continue
        target.parent.mkdir(parents=True, exist_ok=True)
        with archive.open(entry) as source, target.open("wb") as destination:
            shutil.copyfileobj(source, destination)
        mode = (entry.external_attr >> 16) & 0o777
        target.chmod(mode or 0o644)
    app = volume / "SnapBerry.app"
    require(app.is_dir(), "Missing SnapBerry.app")
    return app


def native_code_files(app):
    result = []
    for path in sorted(app.rglob("*")):
        if path.is_file() and not path.is_symlink():
            with path.open("rb") as stream:
                if stream.read(4) in MACHO_MAGICS:
                    result.append(path)
    return result


def relocate_payload(app):
    # Apple TN2206: non-Mach-O files belong in Resources. Compatibility links
    # preserve apphost/hostfxr lookup paths without altering the native executable.
    macos = app / "Contents/MacOS"
    payload = app / "Contents/Resources/RuntimePayload"
    payload.mkdir(parents=True)
    for source in list(macos.iterdir()):
        if source.name == "SnapBerry.Desktop":
            continue
        target = payload / source.name
        source.rename(target)
        source.symlink_to(Path("../Resources/RuntimePayload") / source.name,
                          target_is_directory=target.is_dir())


def run(*args, **kwargs):
    return subprocess.run([str(arg) for arg in args], check=True, **kwargs)


def verify_signature(app):
    run("codesign", "--verify", "--deep", "--strict", "--verbose=2", app)
    result = run("codesign", "--display", "--entitlements", ":-", app, stdout=subprocess.PIPE)
    actual = plistlib.loads(result.stdout)
    expected = plistlib.loads((BUILD / "macos-entitlements.plist").read_bytes())
    require(all(actual.get(key) == value for key, value in expected.items()), "Missing .NET runtime entitlements")
    require(not actual.get("com.apple.security.get-task-allow"), "Unexpected debug entitlement")


def create_dmg(package, arch, output):
    require(sys.platform == "darwin", "DMG creation requires macOS")
    # Validate before extracting or invoking signing tools.
    verify_package(package, arch)
    output = output.resolve()
    require(output.suffix.lower() == ".dmg", "Output must be a .dmg file")
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="snapberry-dmg-", dir=output.parent) as temporary:
        root = Path(temporary)
        volume = root / "volume"
        volume.mkdir()
        with zipfile.ZipFile(package) as archive:
            app = extract_app(archive, volume)
        relocate_payload(app)
        executable = app / "Contents/MacOS/SnapBerry.Desktop"
        # Sign native components inside-out; --deep is only used for verification.
        for index, path in enumerate(native_code_files(app)):
            path.chmod(0o755)
            if path != executable:
                run("codesign", "--force", "--sign", "-", "--options", "runtime", "--timestamp=none",
                    "--identifier", f"com.ynsim.snapberry.native.{index}", path)
        run("codesign", "--force", "--sign", "-", "--options", "runtime", "--timestamp=none",
            "--entitlements", BUILD / "macos-entitlements.plist", app)
        verify_signature(app)
        (volume / "Applications").symlink_to("/Applications", target_is_directory=True)
        shutil.copyfile(BUILD / "macos-install.txt", volume / "설치 안내.txt")
        image = root / "SnapBerry.dmg"
        run("hdiutil", "create", "-volname", "SnapBerry", "-srcfolder", volume,
            "-fs", "HFS+", "-format", "UDZO", "-ov", image)
        run("hdiutil", "verify", image)
        mount = root / "mounted"
        mount.mkdir()
        run("hdiutil", "attach", "-readonly", "-nobrowse", "-noautoopen",
            "-mountpoint", mount, image)
        try:
            require((mount / "Applications").is_symlink()
                    and (mount / "Applications").readlink() == Path("/Applications"),
                    "Missing Applications folder link")
            require(sorted(unicodedata.normalize("NFC", item.name) for item in mount.iterdir() if not item.name.startswith("."))
                    == ["Applications", "SnapBerry.app", "설치 안내.txt"], "Unexpected installer contents")
            verify_signature(mount / "SnapBerry.app")
        finally:
            run("hdiutil", "detach", mount)
        image.replace(output)
    print(f"DMG OK: {output.name}; signature / entitlements / read-only mount / Applications link verified")
    print("Trial build: Developer ID signing and Apple notarization are not included.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package", type=Path)
    parser.add_argument("--arch", choices=("arm64", "x64"), required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    try:
        create_dmg(args.package, args.arch, args.output)
    except (ValueError, OSError, KeyError, zipfile.BadZipFile, subprocess.CalledProcessError) as error:
        print(f"DMG creation failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
