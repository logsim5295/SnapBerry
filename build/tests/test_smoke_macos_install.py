"""Ensure native installation/GUI tests cannot run on an unintended host."""

from pathlib import Path
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import smoke_macos_install as smoke


class InstallSafetyTests(unittest.TestCase):
    def test_preapproved_or_skipped_consent_does_not_pass(self):
        with self.assertRaisesRegex(ValueError, "initialPromptObserved"):
            smoke.verify_consent({"success": True, "initialPromptObserved": False})

    def test_consent_without_ui_approval_does_not_pass(self):
        report = {name: True for name in smoke.CONSENT_CHECKS}
        report.update(tccDatabaseEdited=False, launchMode="LaunchServices")
        with self.assertRaisesRegex(ValueError, "approval action"):
            smoke.verify_consent(report)

    def test_complete_real_ui_consent_is_accepted(self):
        report = {name: True for name in smoke.CONSENT_CHECKS}
        report.update(tccDatabaseEdited=False, launchMode="LaunchServices", settingsSwitchPressed=True)
        smoke.verify_consent(report)

    def test_incomplete_authentication_is_not_approval(self):
        report = {name: True for name in smoke.CONSENT_CHECKS}
        report.update(tccDatabaseEdited=False, launchMode="LaunchServices", settingsSwitchPressed=True, authenticationRequired=True)
        with self.assertRaisesRegex(ValueError, "authentication"):
            smoke.verify_consent(report)

    def test_missing_native_case_is_not_success(self):
        with self.assertRaisesRegex(ValueError, "native"):
            smoke.verify_features({"success": True, "cases": {}})

    def test_blocked_native_case_is_not_success(self):
        cases = {name: {"status": "passed"} for name in smoke.FEATURE_CASES}
        cases["global-display"] = {"status": "blocked", "error": "Permission denied"}
        with self.assertRaisesRegex(ValueError, "global-display"):
            smoke.verify_features({"success": True, "cases": cases})

    def test_all_real_cases_are_required(self):
        smoke.verify_features({"success": True, "cases": {
            name: {"status": "passed"} for name in smoke.FEATURE_CASES}})

    def test_windows_is_rejected_before_platform_commands(self):
        with patch.object(smoke.sys, "platform", "win32"), patch.object(smoke.subprocess, "run") as run:
            with self.assertRaisesRegex(ValueError, "macOS"):
                smoke.require_ci_host("arm64")
            run.assert_not_called()

    def test_mac_outside_github_actions_is_rejected(self):
        with patch.object(smoke.sys, "platform", "darwin"), patch.dict(smoke.os.environ, {}, clear=True):
            with self.assertRaisesRegex(ValueError, "GitHub Actions"):
                smoke.require_ci_host("arm64")

    def test_wrong_host_cpu_is_rejected_before_installation(self):
        with patch.object(smoke.sys, "platform", "darwin"), patch.dict(smoke.os.environ, {"GITHUB_ACTIONS": "true", "RUNNER_OS": "macOS"}), patch.object(smoke.platform, "machine", return_value="x86_64"):
            with self.assertRaisesRegex(ValueError, "CPU"):
                smoke.require_ci_host("arm64")


if __name__ == "__main__":
    unittest.main()
