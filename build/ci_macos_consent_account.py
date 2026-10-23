"""Disposable authentication fixture, exclusively on GitHub-hosted macOS VMs.

Creates an independent random-password admin for the actual macOS consent UI;
never reads/resets an existing user's password or changes authorization/TCC policy.
Credentials stay in a private temporary file outside uploaded test artifacts.
"""
from contextlib import contextmanager
import json
import os
from pathlib import Path
import secrets
import subprocess
import sys
import tempfile
import time
import uuid

from verify_macos import require


def require_fixture_host():
    require(sys.platform == "darwin" and os.environ.get("GITHUB_ACTIONS") == "true"
            and os.environ.get("RUNNER_OS") == "macOS"
            and os.environ.get("RUNNER_ENVIRONMENT") == "github-hosted",
            "Temporary consent accounts require an isolated GitHub-hosted macOS VM")


def verify_owned_identity(account, username, uid, marker):
    require(account.pw_name == username and account.pw_uid == uid and uid >= 501
            and account.pw_gecos == marker and account.pw_dir == f"/Users/{username}",
            "Refusing to modify an account whose identity is not owned by this test")


@contextmanager
def consent_account():
    require_fixture_host()
    import pwd
    username = "sbconsentci"
    try:
        pwd.getpwnam(username)
    except KeyError:
        pass
    else:
        raise ValueError("Refusing to overwrite an existing consent fixture account")
    require(not os.path.lexists(f"/Users/{username}"), "Refusing to reuse an existing consent fixture home")
    password = secrets.token_urlsafe(32)
    marker = "SnapBerry Consent CI " + uuid.uuid4().hex
    uid = None
    try:
        # Captured output is never logged: errors may contain command arguments.
        try:
            created = subprocess.run(["/usr/bin/sudo", "-n", "/usr/sbin/sysadminctl",
                                      "-addUser", username, "-fullName", marker,
                                      "-home", f"/Users/{username}", "-password", password, "-admin"],
                                     stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=90)
        except (OSError, subprocess.SubprocessError):
            raise ValueError("Temporary consent account creation could not finish") from None
        try:
            account = pwd.getpwnam(username)
        except KeyError:
            raise ValueError("macOS did not create the temporary consent account") from None
        verify_owned_identity(account, username, account.pw_uid, marker)
        uid = account.pw_uid
        require(created.returncode == 0, "macOS consent fixture creation failed")
        groups = subprocess.run(["/usr/bin/id", "-G", username], capture_output=True, text=True, check=True).stdout.split()
        require("80" in groups, "Consent fixture is not an administrator")
        with tempfile.TemporaryDirectory(prefix="snapberry-consent-auth-") as temporary:
            path = Path(temporary) / "credentials.json"
            descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
            with os.fdopen(descriptor, "w") as stream:
                json.dump({"username": username, "password": password}, stream)
            yield path
    finally:
        if uid is None:
            # sysadminctl may create the record and then stall before returning.
            try:
                partial = pwd.getpwnam(username)
            except KeyError:
                pass
            else:
                verify_owned_identity(partial, username, partial.pw_uid, marker)
                uid = partial.pw_uid
        if uid is not None:
            verify_owned_identity(pwd.getpwnam(username), username, uid, marker)
            removed = subprocess.run(["/usr/bin/sudo", "-n", "/usr/sbin/sysadminctl", "-deleteUser", username, "-keepHome"],
                                     stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=90)
            require(removed.returncode == 0, "Temporary consent account cleanup failed")
            # libc getpwnam may retain the record after sysadminctl deletes it.
            # Query the local directory node rather than accepting cached identity.
            deadline = time.monotonic() + 10
            while True:
                lookup = subprocess.run(["/usr/bin/dscl", ".", "-read", f"/Users/{username}", "UniqueID"],
                                        stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=10)
                if lookup.returncode != 0 and b"eDSRecordNotFound" in lookup.stdout + lookup.stderr:
                    print("Temporary consent authentication account removed", flush=True)
                    break
                if lookup.returncode != 0 or time.monotonic() >= deadline:
                    diagnostic = (removed.stdout + removed.stderr).decode(errors="replace").replace(password, "<redacted>")
                    print("Consent fixture cleanup diagnostic: " + diagnostic, file=sys.stderr)
                    raise ValueError("Temporary consent account deletion was not confirmed by the local directory")
                time.sleep(0.5)
