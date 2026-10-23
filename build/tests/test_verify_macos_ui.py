"""Validate native UI evidence and restrict interactive execution to hosted macOS CI."""
from pathlib import Path
import json
import struct
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import verify_macos_ui as ui


class NativeUiEvidenceTests(unittest.TestCase):
    def report(self, root):
        screenshots = {}
        for name in ui.SCENES:
            filename = name + "-light.png"
            (root / filename).write_bytes(b"\x89PNG\r\n\x1a\n" + struct.pack(">I", 13) + b"IHDR" + struct.pack(">II", 820, 560))
            screenshots[name] = dict(file=filename, windowId=9, width=820, height=560, uniqueColors=100)
        screenshots["main"]["canvasLuminance"] = 0.9
        return dict(success=True, nativeWindowServer=True, headless=False, theme="light", sourceSha="abc123",
                    pid=123, normalTermination=True, permissionSettingsModified=False, screenshots=screenshots)

    def test_headless_frames_are_not_native_ui_evidence(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder); report = self.report(root); report["headless"] = True
            with self.assertRaisesRegex(ValueError, "native"):
                ui.verify_report(report, root, "light", "abc123")

    def test_every_scene_must_be_present_and_nonblank(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder); report = self.report(root)
            report["screenshots"]["about"]["uniqueColors"] = 1
            with self.assertRaisesRegex(ValueError, "about"):
                ui.verify_report(report, root, "light", "abc123")
            del report["screenshots"]["about"]
            with self.assertRaisesRegex(ValueError, "about"):
                ui.verify_report(report, root, "light", "abc123")

    def test_wrong_commit_or_theme_is_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder); report = self.report(root)
            with self.assertRaisesRegex(ValueError, "commit"):
                ui.verify_report(report, root, "light", "new-commit")
            with self.assertRaisesRegex(ValueError, "theme"):
                ui.verify_report(report, root, "dark", "abc123")

    def test_screenshot_path_must_stay_inside_evidence_directory(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder); report = self.report(root)
            report["screenshots"]["main"]["file"] = "../outside.png"
            with self.assertRaisesRegex(ValueError, "path"):
                ui.verify_report(report, root, "light", "abc123")

    def test_complete_native_report_is_accepted(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            ui.verify_report(self.report(root), root, "light", "abc123")

    def test_rendered_canvas_must_match_requested_theme(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder); report = self.report(root)
            report["screenshots"]["main"]["canvasLuminance"] = 0.1
            with self.assertRaisesRegex(ValueError, "theme"):
                ui.verify_report(report, root, "light", "abc123")

    def test_failed_process_cleanup_retains_owned_files(self):
        with patch.object(ui, "run", side_effect=RuntimeError("cleanup failed")), patch.object(ui.shutil, "rmtree") as remove:
            with self.assertRaisesRegex(RuntimeError, "cleanup failed"):
                ui.cleanup_owned(Path("probe"), Path("app"), Path("settings"), Path("temporary"), True)
            remove.assert_not_called()

    def test_process_cleanup_precedes_owned_directory_removal(self):
        operations = []
        with patch.object(ui, "run", side_effect=lambda *a, **k: operations.append("process")), patch.object(ui.shutil, "rmtree", side_effect=lambda p: operations.append(str(p))):
            ui.cleanup_owned(Path("probe"), Path("app"), Path("settings"), Path("temporary"), True)
        self.assertEqual(["process", "settings", "temporary"], operations)

    def test_non_mac_host_rejected_before_native_commands(self):
        with patch.object(ui.sys, "platform", "win32"), patch.object(ui.subprocess, "run") as run:
            with self.assertRaisesRegex(ValueError, "macOS"):
                ui.require_ci_host("arm64")
            run.assert_not_called()

    def test_mac_outside_github_and_wrong_cpu_are_rejected(self):
        with patch.object(ui.sys, "platform", "darwin"), patch.dict(ui.os.environ, {}, clear=True):
            with self.assertRaisesRegex(ValueError, "GitHub"):
                ui.require_ci_host("arm64")
        with patch.object(ui.sys, "platform", "darwin"), patch.dict(ui.os.environ, {"GITHUB_ACTIONS":"true", "RUNNER_OS":"macOS"}), patch.object(ui.platform, "machine", return_value="x86_64"):
            with self.assertRaisesRegex(ValueError, "CPU"):
                ui.require_ci_host("arm64")


if __name__ == "__main__":
    unittest.main()
