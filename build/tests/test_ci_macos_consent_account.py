from pathlib import Path
from types import SimpleNamespace
import sys
import unittest
from unittest.mock import Mock, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import ci_macos_consent_account as fixture


class ConsentAccountSafetyTests(unittest.TestCase):
    def test_partially_created_account_is_cleaned_after_creation_timeout(self):
        account = SimpleNamespace(pw_name="sbconsentci", pw_uid=505, pw_gecos="SnapBerry Consent CI marker", pw_dir="/Users/sbconsentci")
        records = SimpleNamespace(getpwnam=Mock(side_effect=[KeyError(), account, account, KeyError()]))
        with patch.object(fixture, "require_fixture_host"), patch.dict(sys.modules, {"pwd": records}), patch.object(fixture.uuid, "uuid4", return_value=SimpleNamespace(hex="marker")), patch.object(fixture.secrets, "token_urlsafe", return_value="dummy-test-password"), patch.object(fixture.subprocess, "run", side_effect=[fixture.subprocess.TimeoutExpired("create", 90), SimpleNamespace(returncode=0), SimpleNamespace(returncode=36, stdout=b"", stderr=b"eDSRecordNotFound")]) as run:
            with self.assertRaisesRegex(ValueError, "could not finish") as error:
                with fixture.consent_account():
                    self.fail("Timed out creation must not continue")
            self.assertNotIn("dummy-test-password", str(error.exception))
            self.assertEqual(3, run.call_count)
            self.assertIn("-deleteUser", run.call_args_list[1].args[0])
            self.assertEqual("/usr/bin/dscl", run.call_args.args[0][0])

    def test_local_windows_and_self_hosted_machines_are_rejected(self):
        for platform, environment in [("win32", "github-hosted"), ("darwin", "self-hosted")]:
            with self.subTest(platform=platform, environment=environment), patch.object(fixture.sys, "platform", platform), patch.dict(fixture.os.environ, {"GITHUB_ACTIONS": "true", "RUNNER_OS": "macOS", "RUNNER_ENVIRONMENT": environment}), patch.object(fixture.subprocess, "run") as run:
                with self.assertRaisesRegex(ValueError, "GitHub-hosted"):
                    with fixture.consent_account():
                        self.fail("Fixture must not be created")
                run.assert_not_called()

    def test_changed_account_identity_is_never_deleted(self):
        account = dict(pw_name="sbconsentci", pw_uid=505, pw_gecos="test marker", pw_dir="/Users/sbconsentci")
        for field, value in [("pw_uid", 506), ("pw_gecos", "another owner"), ("pw_dir", "/Users/other")]:
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, "not owned"):
                fixture.verify_owned_identity(SimpleNamespace(**(account | {field: value})), "sbconsentci", 505, "test marker")


if __name__ == "__main__":
    unittest.main()
