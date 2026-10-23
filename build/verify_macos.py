"""Inspect a macOS ZIP; --native additionally loads libraries on a matching Mac.

Never starts SnapBerry, creates windows, registers hotkeys, accesses the clipboard,
or captures the screen. Uses only the Python standard library.
"""

import argparse
import ctypes
import json
from pathlib import Path, PurePosixPath
import platform
import plistlib
import struct
import sys
import tempfile
import zipfile


MACOS = "SnapBerry.app/Contents/MacOS/"
LIBRARIES = {
    "libAvaloniaNative.dylib": ("CreateAvaloniaNative",),
    "libSkiaSharp.dylib": ("sk_version_get_string",),
    "libhostfxr.dylib": ("hostfxr_initialize_for_runtime_config",),
}
CARBON = "/System/Library/Frameworks/Carbon.framework/Carbon"
CARBON_EXPORTS = (
    "GetApplicationEventTarget", "InstallEventHandler", "RemoveEventHandler",
    "RegisterEventHotKey", "UnregisterEventHotKey", "GetEventParameter",
)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def verify_package(path, arch, native=False):
    require(arch in ("arm64", "x64"), "Unsupported CPU architecture")
    with zipfile.ZipFile(path) as archive:
        entries = archive.infolist()
        names = [entry.filename for entry in entries]
        require(len(names) == len(set(names)), "Duplicate ZIP entries")
        for entry in entries:
            # ZipInfo normalizes backslashes on Windows; inspect the original header too.
            name = entry.orig_filename
            relative = PurePosixPath(name)
            require(not relative.is_absolute() and ".." not in relative.parts
                    and "\\" not in name and ":" not in name,
                    f"Unsafe ZIP path: {name}")
        required = [
            MACOS + name for name in (
                "SnapBerry.Desktop", "SnapBerry.Desktop.dll", "SnapBerry.Core.dll",
                "SnapBerry.Imaging.dll",
                "SnapBerry.Desktop.runtimeconfig.json", "System.Private.CoreLib.dll",
                "libcoreclr.dylib", "libhostpolicy.dylib", *LIBRARIES,
            )
        ] + ["SnapBerry.app/Contents/Info.plist", "SnapBerry.app/Contents/Resources/App.icns",
             "Finalize-MacOS.sh", "README.md"]
        for name in required:
            require(name in names and archive.getinfo(name).file_size > 0,
                    f"Missing or empty package file: {name}")
        for entry in entries:
            if entry.filename == MACOS + "SnapBerry.Desktop" or entry.filename.endswith((".dylib", ".sh")):
                require((entry.external_attr >> 16) & 0o777 == 0o755,
                        f"Missing executable permissions: {entry.filename}")
        with archive.open(MACOS + "SnapBerry.Desktop") as executable:
            header = executable.read(8)
        require(len(header) == 8, "Truncated Mach-O header")
        magic, cpu = struct.unpack("<II", header)
        require(magic == 0xFEEDFACF, "Executable is not a 64-bit Mach-O")
        require(cpu == {"arm64": 0x0100000C, "x64": 0x01000007}[arch],
                "Executable CPU does not match the expected architecture")
        plist = plistlib.loads(archive.read("SnapBerry.app/Contents/Info.plist"))
        require(plist.get("CFBundleExecutable") == "SnapBerry.Desktop", "Incorrect bundle executable")
        require(plist.get("CFBundlePackageType") == "APPL", "Incorrect bundle package type")
        require(plist.get("CFBundleShortVersionString"), "Missing bundle version")
        require(plist.get("LSMinimumSystemVersion"), "Missing minimum macOS version")
        icon = archive.read("SnapBerry.app/Contents/Resources/App.icns")
        require(len(icon) >= 16 and icon[:4] == b"icns"
                and struct.unpack(">I", icon[4:8])[0] == len(icon), "Invalid ICNS container")
        runtime = json.loads(archive.read(MACOS + "SnapBerry.Desktop.runtimeconfig.json"))["runtimeOptions"]
        require(runtime.get("tfm") == "net10.0", "Incorrect target framework")
        require(any(item.get("name") == "Microsoft.NETCore.App"
                    for item in runtime.get("includedFrameworks", [])), "Package is not self-contained")
        print(f"Package OK: {arch}, version {plist['CFBundleShortVersionString']}, {len(entries)} files")
        if native:
            verify_native(archive, arch)


def verify_native(archive, arch):
    require(sys.platform == "darwin", "Native verification requires macOS")
    require(platform.machine().lower() == {"arm64": "arm64", "x64": "x86_64"}[arch],
            "Native verification must run on the package CPU architecture")
    with tempfile.TemporaryDirectory(prefix="snapberry-native-") as directory:
        archive.extractall(directory)
        handles = []
        for name, exports in LIBRARIES.items():
            library = ctypes.CDLL(str(Path(directory) / MACOS / name))
            handles.append(library)
            for export in exports:
                getattr(library, export)
            print(f"Native load / exports OK: {name}")
        library = ctypes.CDLL(CARBON)
        handles.append(library)
        for export in CARBON_EXPORTS:
            getattr(library, export)
        print("Carbon exports OK (no event handler or hotkeys registered)")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package", type=Path)
    parser.add_argument("--arch", choices=("arm64", "x64"), required=True)
    parser.add_argument("--native", action="store_true")
    args = parser.parse_args()
    try:
        verify_package(args.package, args.arch, args.native)
    except (ValueError, KeyError, OSError, AttributeError, zipfile.BadZipFile,
            plistlib.InvalidFileException) as error:
        print(f"Verification failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
