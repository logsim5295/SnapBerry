"""Prepare and verify release files without launching SnapBerry. Standard library only."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import plistlib
import re
import shutil
import sys
import xml.etree.ElementTree as ET
import zipfile


ROOT = Path(__file__).resolve().parents[1]
LEGAL = ("LICENSE.txt", "DATA-PROCESSING.md", "THIRD-PARTY-NOTICES.md")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def parse_tag(tag):
    require(re.fullmatch(r"v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)", tag),
            "Expected a stable version tag such as v1.2.3")
    version = tag[1:]
    require(all(int(part) <= 65534 for part in version.split(".")), "Version exceeds .NET assembly limits")
    return version


def prepare_version(root, tag):
    version = parse_tag(tag)
    projects = []
    for app in ("SnapBerry.App", "SnapBerry.Desktop"):
        path = root / "src" / app / f"{app}.csproj"
        tree = ET.parse(path)
        node = tree.find("PropertyGroup/Version")
        require(node is not None, f"Missing Version property: {path}")
        node.text = version
        projects.append((path, tree))
    path = root / "src/SnapBerry.Desktop/Packaging/Info.plist"
    plist = plistlib.loads(path.read_bytes())
    for key in ("CFBundleShortVersionString", "CFBundleVersion"):
        require(key in plist, f"Missing bundle property: {key}")
        plist[key] = version
    for project, tree in projects:
        tree.write(project, encoding="utf-8")
    path.write_bytes(plistlib.dumps(plist))
    return version


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def asset_names(version):
    return {"SnapBerry.exe", f"SnapBerry-v{version}-win-x64.zip", *LEGAL,
            "SnapBerry-macos-arm64.dmg", "SnapBerry-macos-x64.dmg"}


def package_windows(source, output, version):
    parse_tag("v" + version)
    names = ("SnapBerry.exe", *LEGAL)
    for name in names:
        require((source / name).is_file() and (source / name).stat().st_size > 0, f"Missing file: {name}")
    with (source / "SnapBerry.exe").open("rb") as stream:
        require(stream.read(2) == b"MZ", "Windows executable is not a PE file (possibly an LFS pointer)")
    output.mkdir(parents=True, exist_ok=True)
    for name in names:
        shutil.copyfile(source / name, output / name)
    with zipfile.ZipFile(output / f"SnapBerry-v{version}-win-x64.zip", "w",
                         zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for name in names:
            archive.write(output / name, name)


def verify_files(directory, version):
    parse_tag("v" + version)
    expected = asset_names(version)
    actual = {path.name for path in directory.iterdir()}
    require(actual - {"SHA256SUMS.txt"} == expected,
            f"Incorrect release files: missing={sorted(expected - actual)}, unexpected={sorted(actual - expected - {'SHA256SUMS.txt'})}")
    for name in expected:
        require((directory / name).is_file() and (directory / name).stat().st_size > 0, f"Empty asset: {name}")
    for arch in ("arm64", "x64"):
        with (directory / f"SnapBerry-macos-{arch}.dmg").open("rb") as stream:
            stream.seek(-512, 2)
            require(stream.read(4) == b"koly", f"Invalid {arch} DMG trailer")
    with zipfile.ZipFile(directory / f"SnapBerry-v{version}-win-x64.zip") as archive:
        require(sorted(archive.namelist()) == sorted(("SnapBerry.exe", *LEGAL)), "Unexpected Windows ZIP contents")
        for name in archive.namelist():
            with archive.open(name) as stream:
                require(hashlib.file_digest(stream, "sha256").hexdigest() == digest(directory / name),
                        f"Windows ZIP does not match standalone asset: {name}")
    return expected


def write_manifest(directory, version):
    names = verify_files(directory, version)
    text = "".join(f"{digest(directory / name)}  {name}\n" for name in sorted(names))
    (directory / "SHA256SUMS.txt").write_text(text, encoding="utf-8", newline="\n")


def verify_upload(directory, version, metadata):
    expected = verify_files(directory, version) | {"SHA256SUMS.txt"}
    require(metadata["tag_name"] == "v" + version and metadata["draft"], "Expected the matching draft release")
    assets = metadata["assets"]
    require(len(assets) == len(expected) and {a["name"] for a in assets} == expected,
            "Uploaded release assets are incomplete or unexpected")
    for asset in assets:
        path = directory / asset["name"]
        require(asset["state"] == "uploaded" and asset["size"] == path.stat().st_size,
                f"Incomplete upload: {path.name}")
        require(asset["digest"] == "sha256:" + digest(path), f"Upload digest mismatch: {path.name}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    for name in ("version", "prepare"):
        command = commands.add_parser(name)
        command.add_argument("tag")
        if name == "prepare":
            command.add_argument("--root", type=Path, default=ROOT)
    command = commands.add_parser("windows")
    command.add_argument("--source", type=Path, required=True)
    command.add_argument("--output", type=Path, required=True)
    command.add_argument("--version", required=True)
    for name in ("manifest", "verify-upload"):
        command = commands.add_parser(name)
        command.add_argument("--directory", type=Path, required=True)
        command.add_argument("--version", required=True)
        if name == "verify-upload":
            command.add_argument("--metadata", type=Path, required=True)
    args = parser.parse_args()
    try:
        if args.command == "version":
            version = parse_tag(args.tag)
            if output := os.environ.get("GITHUB_OUTPUT"):
                with open(output, "a", encoding="utf-8") as stream:
                    stream.write(f"version={version}\n")
            print(version)
        elif args.command == "prepare":
            print(prepare_version(args.root, args.tag))
        elif args.command == "windows":
            package_windows(args.source, args.output, args.version)
        elif args.command == "manifest":
            write_manifest(args.directory, args.version)
        else:
            verify_upload(args.directory, args.version, json.loads(args.metadata.read_text(encoding="utf-8")))
    except (ValueError, OSError, KeyError, ET.ParseError, zipfile.BadZipFile) as error:
        print(f"Release preparation failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
