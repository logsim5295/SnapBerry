"""Exercise release preparation without running SnapBerry or accessing GitHub."""

import hashlib
import json
from pathlib import Path
import plistlib
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
import zipfile


SCRIPT = Path(__file__).resolve().parents[1] / "release.py"
LEGAL = ("LICENSE.txt", "DATA-PROCESSING.md", "THIRD-PARTY-NOTICES.md")


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="snapberry-release-")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)

    def run_cli(self, *args, success=True):
        result = subprocess.run([sys.executable, str(SCRIPT), *map(str, args)],
                                capture_output=True, text=True)
        if success:
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        else:
            self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        return result

    def source_tree(self):
        for app in ("SnapBerry.App", "SnapBerry.Desktop"):
            path = self.root / "src" / app / f"{app}.csproj"
            path.parent.mkdir(parents=True)
            path.write_text('<Project><PropertyGroup><Version>1.1.0</Version>'
                            '<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
        plist = self.root / "src/SnapBerry.Desktop/Packaging/Info.plist"
        plist.parent.mkdir()
        plist.write_bytes(plistlib.dumps({"CFBundleShortVersionString": "1.1.0",
                                         "CFBundleVersion": "1.1.0", "CFBundleName": "SnapBerry"}))

    def windows_source(self):
        source = self.root / "portable"
        source.mkdir()
        (source / "SnapBerry.exe").write_bytes(b"MZ Windows fixture")
        (source / "debug.pdb").write_bytes(b"must not ship")
        for name in LEGAL:
            (source / name).write_bytes(name.encode())
        return source

    def complete_assets(self):
        source = self.windows_source()
        output = self.root / "assets"
        self.run_cli("windows", "--version", "1.2.3", "--source", source, "--output", output)
        for arch in ("arm64", "x64"):
            (output / f"SnapBerry-macos-{arch}.dmg").write_bytes(arch.encode() + b"koly" + bytes(508))
        return output

    def test_next_tag_updates_both_apps_and_both_bundle_versions(self):
        self.source_tree()
        self.run_cli("prepare", "v1.2.3", "--root", self.root)
        for app in ("SnapBerry.App", "SnapBerry.Desktop"):
            tree = ET.parse(self.root / "src" / app / f"{app}.csproj")
            self.assertEqual("1.2.3", tree.findtext("PropertyGroup/Version"))
            self.assertEqual("net10.0", tree.findtext("PropertyGroup/TargetFramework"))
        plist = plistlib.loads((self.root / "src/SnapBerry.Desktop/Packaging/Info.plist").read_bytes())
        self.assertEqual("1.2.3", plist["CFBundleShortVersionString"])
        self.assertEqual("1.2.3", plist["CFBundleVersion"])
        self.assertEqual("SnapBerry", plist["CFBundleName"])

    def test_invalid_tags_fail_before_changing_source(self):
        self.source_tree()
        before = {p: p.read_bytes() for p in self.root.rglob("*") if p.is_file()}
        for tag in ("1.2.3", "v01.2.3", "v1.2", "v1.2.3-rc.1", "v1.2.65536", "v1.2.3\nother=value"):
            with self.subTest(tag=tag):
                self.run_cli("prepare", tag, "--root", self.root, success=False)
        self.assertEqual(before, {p: p.read_bytes() for p in self.root.rglob("*") if p.is_file()})

    def test_windows_zip_has_matching_version_and_no_debug_symbols(self):
        source = self.windows_source()
        output = self.root / "assets"
        self.run_cli("windows", "--version", "1.2.3", "--source", source, "--output", output)
        with zipfile.ZipFile(output / "SnapBerry-v1.2.3-win-x64.zip") as archive:
            self.assertCountEqual(["SnapBerry.exe", *LEGAL], archive.namelist())
            for name in archive.namelist():
                self.assertEqual((source / name).read_bytes(), archive.read(name))
        self.assertEqual(5, len(list(output.iterdir())))

    def test_manifest_hashes_every_asset_and_is_stable_on_retry(self):
        output = self.complete_assets()
        self.run_cli("manifest", "--version", "1.2.3", "--directory", output)
        manifest = (output / "SHA256SUMS.txt").read_bytes()
        entries = [line.split("  ", 1) for line in manifest.decode().splitlines()]
        self.assertEqual(7, len(entries))
        self.assertNotIn("SHA256SUMS.txt", [name for _, name in entries])
        for digest, name in entries:
            self.assertEqual(hashlib.sha256((output / name).read_bytes()).hexdigest(), digest)
        self.run_cli("manifest", "--version", "1.2.3", "--directory", output)
        self.assertEqual(manifest, (output / "SHA256SUMS.txt").read_bytes())

    def test_missing_cpu_or_unexpected_pdb_cannot_create_manifest(self):
        output = self.complete_assets()
        intel = output / "SnapBerry-macos-x64.dmg"
        data = intel.read_bytes()
        intel.unlink()
        self.run_cli("manifest", "--version", "1.2.3", "--directory", output, success=False)
        self.assertFalse((output / "SHA256SUMS.txt").exists())
        intel.write_bytes(data)
        (output / "debug.pdb").write_bytes(b"symbols")
        self.run_cli("manifest", "--version", "1.2.3", "--directory", output, success=False)
        self.assertFalse((output / "SHA256SUMS.txt").exists())

    def test_corrupt_dmg_or_zip_is_rejected(self):
        output = self.complete_assets()
        dmg = output / "SnapBerry-macos-arm64.dmg"
        data = dmg.read_bytes()
        dmg.write_bytes(b"not a DMG")
        self.run_cli("manifest", "--version", "1.2.3", "--directory", output, success=False)
        dmg.write_bytes(data)
        (output / "SnapBerry.exe").write_bytes(b"MZ different EXE")
        self.run_cli("manifest", "--version", "1.2.3", "--directory", output, success=False)

    def test_uploaded_files_must_all_match_local_sizes_and_hashes(self):
        output = self.complete_assets()
        self.run_cli("manifest", "--version", "1.2.3", "--directory", output)
        metadata = {"tag_name": "v1.2.3", "draft": True, "assets": [
            {"name": p.name, "state": "uploaded", "size": p.stat().st_size,
             "digest": "sha256:" + hashlib.sha256(p.read_bytes()).hexdigest()}
            for p in output.iterdir()]}
        path = self.root / "release.json"
        path.write_text(json.dumps(metadata))
        self.run_cli("verify-upload", "--version", "1.2.3", "--directory", output, "--metadata", path)
        metadata["assets"][0]["digest"] = "sha256:" + "0" * 64
        path.write_text(json.dumps(metadata))
        self.run_cli("verify-upload", "--version", "1.2.3", "--directory", output, "--metadata", path, success=False)
        metadata["assets"].pop()
        path.write_text(json.dumps(metadata))
        self.run_cli("verify-upload", "--version", "1.2.3", "--directory", output, "--metadata", path, success=False)


if __name__ == "__main__":
    unittest.main()
