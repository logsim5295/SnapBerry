"""Installer preparation checks without macOS, Finder, or signing tools."""

from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import package_macos_dmg as package


class DmgPreparationTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="snapberry-dmg-test-")
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)

    def test_install_volume_contains_app_without_consumer_terminal_scripts(self):
        archive_path = self.root / "input.zip"
        with zipfile.ZipFile(archive_path, "w") as archive:
            archive.writestr("SnapBerry.app/Contents/MacOS/SnapBerry.Desktop", b"app")
            archive.writestr("SnapBerry.app/Contents/Info.plist", b"plist")
            archive.writestr("Finalize-MacOS.sh", b"developer helper")
            archive.writestr("README.md", b"developer commands")
        volume = self.root / "volume"
        volume.mkdir()
        with zipfile.ZipFile(archive_path) as archive:
            app = package.extract_app(archive, volume)
        self.assertEqual(b"app", (app / "Contents/MacOS/SnapBerry.Desktop").read_bytes())
        self.assertEqual(["SnapBerry.app"], sorted(item.name for item in volume.iterdir()))

    def test_native_code_detection_includes_helpers_but_excludes_managed_assemblies(self):
        app = self.root / "SnapBerry.app"
        app.mkdir()
        for name, header in {
            "SnapBerry.Desktop": b"\xcf\xfa\xed\xfe",
            "libSkiaSharp.dylib": b"\xca\xfe\xba\xbe",
            "createdump": b"\xcf\xfa\xed\xfe",
            "SnapBerry.Desktop.dll": b"MZ\x00\x00",
            "notes.txt": b"text",
        }.items():
            (app / name).write_bytes(header)
        self.assertCountEqual(["SnapBerry.Desktop", "createdump", "libSkiaSharp.dylib"],
                              [item.name for item in package.native_code_files(app)])

    @unittest.skipIf(sys.platform == "win32", "macOS bundle requires Unix file symlinks")
    def test_resource_layout_preserves_runtime_files_and_host_lookup_paths(self):
        app = self.root / "SnapBerry.app"
        macos = app / "Contents/MacOS"
        macos.mkdir(parents=True)
        fixture = {
            "SnapBerry.Desktop": b"\xcf\xfa\xed\xfe",
            "SnapBerry.Desktop.dll": b"MZ managed",
            "SnapBerry.Desktop.runtimeconfig.json": b"runtime config",
            "libhostfxr.dylib": b"\xcf\xfa\xed\xfe",
            "ko/Example.resources.dll": b"Korean resources",
        }
        for name, data in fixture.items():
            target = macos / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        package.relocate_payload(app)
        self.assertFalse((macos / "SnapBerry.Desktop").is_symlink())
        for name, data in fixture.items():
            self.assertEqual(data, (macos / name).read_bytes())
            if name != "SnapBerry.Desktop":
                resource = app / "Contents/Resources/RuntimePayload" / name
                self.assertEqual(data, resource.read_bytes())
                self.assertTrue((macos / name).samefile(resource))
        self.assertCountEqual(["SnapBerry.Desktop", "libhostfxr.dylib"],
                              [item.name for item in package.native_code_files(app)])

    def test_app_extraction_rejects_parent_paths(self):
        archive_path = self.root / "bad.zip"
        with zipfile.ZipFile(archive_path, "w") as archive:
            archive.writestr("SnapBerry.app/../../escape", b"bad")
        with zipfile.ZipFile(archive_path) as archive:
            with self.assertRaisesRegex(ValueError, "Unsafe"):
                package.extract_app(archive, self.root / "volume")
        self.assertFalse((self.root / "escape").exists())

    def test_non_mac_host_is_rejected_before_running_platform_commands(self):
        with patch.object(package.sys, "platform", "win32"), patch.object(package.subprocess, "run") as run:
            with self.assertRaisesRegex(ValueError, "macOS"):
                package.create_dmg(self.root / "input.zip", "arm64", self.root / "output.dmg")
            run.assert_not_called()

    def test_jit_entitlements_allow_dotnet_without_debug_access(self):
        import plistlib
        entitlements = plistlib.loads((Path(__file__).resolve().parents[1] / "macos-entitlements.plist").read_bytes())
        self.assertTrue(entitlements["com.apple.security.cs.allow-jit"])
        self.assertTrue(entitlements["com.apple.security.cs.disable-library-validation"])
        self.assertNotIn("com.apple.security.get-task-allow", entitlements)


if __name__ == "__main__":
    unittest.main()
