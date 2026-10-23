#!/bin/bash
# macOS에서 실행한다. 서명은 로컬 시험용이며 Developer ID 공증을 대신하지 않는다.
set -euo pipefail
script_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
app="${1:-$script_dir/SnapBerry.app}"
if [[ "$(uname -s)" != "Darwin" ]]; then echo "macOS에서 실행해 주세요." >&2; exit 1; fi
if [[ ! -f "$app/Contents/MacOS/SnapBerry.Desktop" ]]; then echo "SnapBerry.app 경로를 확인해 주세요." >&2; exit 1; fi
chmod +x "$app/Contents/MacOS/SnapBerry.Desktop"
find "$app/Contents/MacOS" -name '*.dylib' -type f -exec chmod +x {} +
codesign --force --deep --sign - "$app"
codesign --verify --deep --strict --verbose=2 "$app"
echo "로컬 시험용 서명 확인 완료: $app"
echo "앱을 직접 열어 캡처 권한·선택·취소·PNG 저장을 확인해 주세요."
