"""Malformed-package regression checks; no macOS or native calls required."""

import contextlib
import io
import json
from pathlib import Path
import plistlib
import struct
import sys
import tempfile
import unittest
from unittest.mock import patch
import warnings
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import verify_macos as verifier


class PackageVerificationTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="snapberry-package-test-")
        self.addCleanup(self.directory.cleanup)
        self.path = Path(self.directory.name) / "package.zip"
        self.files = {
            verifier.MACOS + name: b"fixture" for name in (
                "SnapBerry.Desktop.dll", "SnapBerry.Core.dll", "System.Private.CoreLib.dll",
                "libcoreclr.dylib", "libhostpolicy.dylib", *verifier.LIBRARIES,
            )
        }
        self.files[verifier.MACOS + "SnapBerry.Desktop"] = struct.pack("<II", 0xFEEDFACF, 0x0100000C)
        self.files[verifier.MACOS + "SnapBerry.Desktop.runtimeconfig.json"] = json.dumps({
            "runtimeOptions": {"tfm": "net10.0", "includedFrameworks": [{"name": "Microsoft.NETCore.App"}]},
        }).encode()
        self.files["SnapBerry.app/Contents/Info.plist"] = plistlib.dumps({
            "CFBundleExecutable": "SnapBerry.Desktop", "CFBundlePackageType": "APPL",
            "CFBundleShortVersionString": "1.1.0", "LSMinimumSystemVersion": "14.0",
        })
        self.files["SnapBerry.app/Contents/Resources/App.icns"] = b"icns" + struct.pack(">I", 16) + b"ic08\x00\x00\x00\x08"
        self.files["Finalize-MacOS.sh"] = b"#!/bin/bash\n"
        self.files["README.md"] = b"Package instructions"

    def write_package(self, non_executable=None, duplicate=None):
        with zipfile.ZipFile(self.path, "w") as archive:
            for name, data in self.files.items():
                entry = zipfile.ZipInfo(name)
                executable = name == verifier.MACOS + "SnapBerry.Desktop" or name.endswith((".dylib", ".sh"))
                entry.external_attr = (0o100755 if executable and name != non_executable else 0o100644) << 16
                archive.writestr(entry, data)
            if duplicate:
                with warnings.catch_warnings():
                    warnings.simplefilter("ignore", UserWarning)
                    entry = zipfile.ZipInfo("placeholder")
                    entry.filename = duplicate
                    archive.writestr(entry, b"duplicate")

    def verify(self, arch="arm64", native=False):
        with contextlib.redirect_stdout(io.StringIO()):
            verifier.verify_package(self.path, arch, native)

    def test_both_cpu_architectures_are_accepted(self):
        for arch, cpu in (("arm64", 0x0100000C), ("x64", 0x01000007)):
            with self.subTest(arch=arch):
                self.files[verifier.MACOS + "SnapBerry.Desktop"] = struct.pack("<II", 0xFEEDFACF, cpu)
                self.write_package()
                self.verify(arch)

    def test_wrong_cpu_is_rejected(self):
        self.write_package()
        with self.assertRaisesRegex(ValueError, "CPU"):
            self.verify("x64")

    def test_non_macho_or_truncated_executable_is_rejected(self):
        for content in (b"MZ", struct.pack("<II", 0, 0x0100000C)):
            with self.subTest(content=content):
                self.files[verifier.MACOS + "SnapBerry.Desktop"] = content
                self.write_package()
                with self.assertRaisesRegex(ValueError, "Mach-O"):
                    self.verify()

    def test_missing_native_runtime_is_rejected(self):
        del self.files[verifier.MACOS + "libhostpolicy.dylib"]
        self.write_package()
        with self.assertRaisesRegex(ValueError, "libhostpolicy"):
            self.verify()

    def test_lost_executable_permissions_are_rejected(self):
        for name in (verifier.MACOS + "SnapBerry.Desktop", verifier.MACOS + "libSkiaSharp.dylib", "Finalize-MacOS.sh"):
            with self.subTest(name=name):
                self.write_package(non_executable=name)
                with self.assertRaisesRegex(ValueError, "permissions"):
                    self.verify()

    def test_framework_dependent_package_is_rejected(self):
        self.files[verifier.MACOS + "SnapBerry.Desktop.runtimeconfig.json"] = b'{"runtimeOptions":{"tfm":"net10.0"}}'
        self.write_package()
        with self.assertRaisesRegex(ValueError, "self-contained"):
            self.verify()

    def test_wrong_bundle_executable_is_rejected(self):
        self.files["SnapBerry.app/Contents/Info.plist"] = plistlib.dumps({"CFBundleExecutable": "other"})
        self.write_package()
        with self.assertRaisesRegex(ValueError, "bundle executable"):
            self.verify()

    def test_invalid_icon_length_is_rejected(self):
        self.files["SnapBerry.app/Contents/Resources/App.icns"] = b"icns" + struct.pack(">I", 999) + b"invalid!"
        self.write_package()
        with self.assertRaisesRegex(ValueError, "ICNS"):
            self.verify()

    def test_duplicate_or_unsafe_entries_are_rejected_before_extraction(self):
        self.write_package(duplicate="README.md")
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            self.verify()
        for name in ("../escape", "/absolute", "C:/absolute", "folder\\escape"):
            with self.subTest(name=name):
                self.write_package(duplicate=name)
                with self.assertRaisesRegex(ValueError, "Unsafe"):
                    self.verify()

    def test_native_probe_on_windows_is_rejected_without_loading_libraries(self):
        self.write_package()
        with patch.object(verifier.sys, "platform", "win32"), patch.object(verifier.ctypes, "CDLL") as load:
            with self.assertRaisesRegex(ValueError, "requires macOS"):
                self.verify(native=True)
            load.assert_not_called()

    def test_native_probe_rejects_a_mismatched_host_architecture(self):
        self.write_package()
        with patch.object(verifier.sys, "platform", "darwin"), patch.object(verifier.platform, "machine", return_value="x86_64"), patch.object(verifier.ctypes, "CDLL") as load:
            with self.assertRaisesRegex(ValueError, "CPU architecture"):
                self.verify(native=True)
            load.assert_not_called()


if __name__ == "__main__":
    unittest.main()
