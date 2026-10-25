# SnapBerry macOS 시험 버전

Windows 버전과 함께 배포하는 별도 Avalonia 앱이다. macOS 14 이상을 대상으로 하며 Apple Silicon과 Intel용 패키지를 모두 제공한다. GitHub의 맥 환경에서 설치·실행과 실제 OS 기능을 검증하며, 사용자 맥의 최초 권한 승인 절차는 직접 확인해야 한다.

## 패키지 선택

- Apple Silicon: [SnapBerry-macos-arm64.dmg](https://github.com/logsim5295/SnapBerry/releases/latest/download/SnapBerry-macos-arm64.dmg)
- Intel: [SnapBerry-macos-x64.dmg](https://github.com/logsim5295/SnapBerry/releases/latest/download/SnapBerry-macos-x64.dmg)

새 저장소는 [logsim5295/SnapBerry](https://github.com/logsim5295/SnapBerry)다. 위 [최신 릴리스](https://github.com/logsim5295/SnapBerry/releases/latest)의 DMG는 GitHub 로그인 없이 받을 수 있으며 Actions Artifacts의 14일 보관 기한이 적용되지 않는다.

맥 터미널에서 `uname -m` 결과가 `arm64`이면 Apple Silicon, `x86_64`이면 Intel이다. macOS의 Rosetta 터미널은 x86_64를 표시할 수 있으므로 Apple 메뉴 → 이 Mac에 관하여의 칩·프로세서 표시가 가장 정확하다.

## 터미널 없이 설치하기

1. [최신 릴리스](https://github.com/logsim5295/SnapBerry/releases/latest) 또는 위의 링크에서 맥 종류에 맞는 DMG를 받는다. Apple 메뉴 → 이 Mac에 관하여에서 칩이 Apple M 시리즈이면 Apple Silicon, 프로세서가 Intel이면 Intel 파일을 선택한다. 릴리스에 첨부된 `SHA256SUMS.txt`에서 설치 파일의 SHA-256을 확인할 수 있다. GitHub 로그인이나 Actions Artifact ZIP 다운로드는 필요하지 않다.
2. DMG를 더블클릭한다. 안에 있는 SnapBerry 아이콘을 Applications(응용 프로그램) 폴더로 끌어 놓는다.
3. Finder의 응용 프로그램 폴더에서 SnapBerry를 더블클릭한다. 설치 디스크는 추출해도 된다. 별도 .NET 설치나 터미널 명령은 필요하지 않다.
4. 개발자를 확인할 수 없다는 이유로 차단되면, 신뢰하는 배포 파일인지 확인한 뒤 시스템 설정 → 개인정보 보호 및 보안에서 SnapBerry의 ‘그래도 열기’를 선택한다. 자세한 절차는 [Apple 안내](https://support.apple.com/ko-kr/102445)를 따른다.
5. 첫 캡처 시 macOS 화면 기록 요청에서 시스템 설정을 열고 SnapBerry를 허용한다. 앱을 다시 열어 캡처한다. 권한이 없으면 검은 이미지를 저장하지 않고 권한 안내를 표시한다.

앱의 로컬 시험용 서명과 실행 권한 준비는 GitHub의 맥 빌드 서버에서 끝낸다. DMG 안에는 사용자가 실행할 터미널 스크립트가 없다. 현재 Apple Developer Program에 가입하지 않아 Developer ID 서명·Apple 공증은 하지 않은 시험 버전이며, 첫 실행의 보안 승인은 남는다. 보안 설정이나 격리 속성은 자동으로 변경하지 않는다.

## 기능과 조작

- 배경 지우개: Windows 홈 리본과 macOS 편집 도구 모음에서 **배경 지우개**를 선택한 뒤 원본 이미지의 색상을 클릭합니다. 보라색 선택 미리보기를 확인하고 **색상 범위**를 조절합니다. 기본은 클릭 지점에 연결된 영역이며, **전체 색상 선택**을 켜면 현재 자르기 영역 전체의 같은 색을 선택합니다. **선택 지우기** 또는 Delete로 선택 픽셀을 투명하게 만들고 Esc나 다른 도구로 선택을 취소합니다. 선택·색상 범위 조절은 문서를 변경하지 않고, 지우기를 확정하면 실행 취소/다시 실행·자동 저장·복사·내보내기에 반영됩니다. 원본과 도형·텍스트·삽입 이미지는 보존하며, 투명 영역은 체크무늬로 표시합니다. AI 모델·인터넷·토큰 요금이 필요하지 않습니다. 투명 PNG로 저장할 수 있고, 결과 PNG의 자원 한도는 16MiB입니다.
- 영역 캡처: 영역을 드래그한다. 창 캡처: 대상 창을 선택한다. ESC는 캡처 취소다.
- 전체 화면은 연결된 모든 모니터를 실제 배치에 맞춰 한 이미지로 합성한다. 음수 위치와 서로 다른 Retina 배율을 처리하며, 미러링된 화면은 중복하지 않는다. 캡처 전에 SnapBerry 창을 숨기고 완료·취소·오류 시 복원한다.
- 캡처는 macOS 기본 `/usr/sbin/screencapture`를 캡처 버튼·메뉴·단축키 입력에 따라 실행한다. 직접 ScreenCaptureKit 브리지는 포함하지 않는다.
- 처음에는 시스템 설정 → 개인정보 보호 및 보안 → 화면 기록에서 앱의 권한을 확인한다. 권한 화면의 명칭은 macOS 버전에 따라 다를 수 있다.
- 이미지 열기, 화살표·텍스트·사각형·모자이크·번호, 선택·이동·우측 아래 핸들 크기 조절, 이미지 추가·복제·삭제, 드래그 자르기, 확대·가운데 버튼 팬, 실행 취소·다시 실행, PNG 저장을 제공한다.
- 상단 도구 모음에서 이미지 열기·붙여넣기, 영역·창·전체 화면 캡처, 복사·PNG 저장을 실행한다. 왼쪽에는 검색 가능한 이력 사이드바, 캔버스 위에는 주석·편집 도구를 표시한다. 보기 버튼에서 확대·맞춤·이력 표시·테마를 바꾸며, 우측 버튼으로 단축키·설정·정보 창을 연다. 좁은 창에서는 도구 모음을 가로로 스크롤할 수 있다. 속성 영역은 선택한 도구·개체에 필요한 항목만 보인다. 색상 선택창은 팔레트·색조·채도·밝기·RGB·HEX를 지원하며, 투명 색상을 편집할 때 기존 투명도를 유지한다.
- 실선·파선·점선·일점쇄선·이점쇄선과 선 두께를 설정한다. 모자이크 블록 크기는 4~64px로 조절한다. 숫자·영문 소문자·대문자 번호 형식과 다음 순번, 번호 글자색을 설정한다. 선택 개체 변경과 번호 순서는 실행 취소·다시 실행에 포함된다.
- 텍스트 입력란에 내용을 적고 텍스트 도구로 이미지를 클릭한다. 선택 텍스트는 입력란과 ‘선택 텍스트 변경’ 버튼으로 바꾸거나 더블클릭/내용 편집 창에서 여러 줄로 편집한다. 번호와 키보드 스티커의 라벨도 내용 편집을 지원한다.
- 하단 이미지 크기를 누르면 비율 유지·원래 크기 복원과 함께 출력 가로·세로 픽셀을 바꾼다. 휠은 확대·축소, 가운데 버튼 드래그는 팬 이동이다. PNG 기본 파일명에 이력 이름을 사용하고 최근 저장 폴더를 기억한다.
- Command+O: 열기, Command+S: PNG 저장, Command+Z/Shift+Z: Undo/Redo, Command+D: 선택 복제, Delete/Backspace: 선택 삭제, ESC: 제스처 취소. 텍스트 입력 중에는 텍스트 편집 단축키가 우선한다.
- Command+C/V와 복사·붙여넣기 버튼으로 이미지를 복사한다. 이력 목록에서 복사하면 선택 이력의 합성 이미지, 편집기에서 이미지 개체를 선택하면 해당 이미지와 표시 크기를 복사한다. 붙여넣기는 현재 이미지 위에 이동·크기 조절·복제·실행 취소 가능한 개체를 추가한다. 빈 편집기에서는 새 이미지로 연다. 다른 앱과 교환하는 표준 비트맵도 제공한다.
- 스티커는 Windows와 공유하는 80종과 맥 키보드 8종이다. 이모지·키보드·체크 / 엑스·기호·방향·업무 카테고리와 이름 검색을 제공한다. 선택창에서 연속 추가할 수 있으며 키보드 스티커를 선택한 뒤 텍스트 입력란과 ‘키 라벨 변경’으로 내용을 바꾼다.
- 전역 단축키 기본값은 Control+Shift+1(영역), Control+Shift+2(창), Control+Shift+3(전체 화면)이다. ‘단축키 설정’에서 사용 여부와 조합을 바꾼다. Ctrl/Control, Alt/Option, Cmd/Command, Shift와 A–Z·0–9·F1–F12를 지원한다. 문자·숫자 키는 미국식 키보드의 물리 위치를 사용한다. 중복이나 다른 앱과의 등록 충돌은 오류로 표시하며 이전 등록을 복구한다. 저장·캡처·설정 중에는 새 캡처 요청을 무시한다.
- 창 닫기는 저장 후 편집기를 숨기며 메뉴 막대 아이콘과 전역 단축키를 유지한다. 메뉴에서 편집기 표시·캡처·단축키 설정·종료를 선택한다. 종료나 Command+Q는 저장 성공 후 단축키를 해제한다.
- 이력 썸네일과 이름·날짜 검색, 이름 변경, 다중 선택·확인 후 일괄 삭제, 편집 가능한 이력 자체 복제를 제공한다. 메뉴 및 F2·Delete·Command+D로 처리하고, 삭제 뒤 인접 이력을 선택한다. 스크롤 또는 ‘더 보기’로 50개씩 불러오며, 썸네일은 화면에 필요한 항목만 제한된 캐시에 렌더링한다. 목록 접기·펼치기와 너비를 기억한다.
- 단축키 입력란에서 실제 키 조합을 누르면 입력된다. Esc는 포커스를 얻었을 때의 값으로 복원하고 ‘기본값’은 각 캡처 기본 조합으로 되돌린다. 입력 창을 여는 동안 기존 전역 등록을 잠시 해제하고 닫으면 저장된 설정으로 복원한다.
- 설정에서 화이트·다크 테마와 로그인 시 자동 실행을 바꾼다. 앱 컨트롤의 호버·눌림·선택·포커스 강조색은 두 테마 모두 보라색 계통을 사용한다. OS 기본 대화상자와 네이티브 메뉴는 macOS 표시를 유지하며, 이미지·색상 견본·스티커·오류 표시의 기능상 색상은 바꾸지 않는다. 자동 실행은 사용자별 `~/Library/LaunchAgents/com.ynsim.snapberry.plist`와 `launchctl enable/disable`로 관리하며 다음 로그인에 적용된다. 설정을 저장하는 즉시 앱을 새로 띄우거나 현재 프로세스를 종료하지 않는다.
- 정보 창은 버전·만든이·GitHub 링크, 진단 정보 복사, 저장 폴더 열기, 이용약관·데이터 처리·외부 구성 요소 안내를 제공한다. 외부 링크·메일·클립보드 명령은 사용자가 해당 항목을 눌렀을 때 실행한다.
- 데이터 위치: `~/Library/Application Support/SnapBerry`. Windows와 같은 `.qcapture` 형식이지만 자동 기기 동기화는 제공하지 않는다.

편집 기능은 Windows와 공유하지만, WPF 리본 대신 Avalonia 도구 모음·사이드바를 사용한다. PNG 합성은 공통 문서 좌표를 사용하지만 OS별 글꼴과 렌더러 차이로 글자 모양이 완전히 같지는 않을 수 있다. 일반 단위 테스트는 메모리 클립보드·가짜 OS 경계·Avalonia 헤드리스 화면을 사용한다. 기존 GitHub 맥 설치 테스트는 실제 클립보드·캡처·전역 키 입력과 최초 화면 기록 권한 승인을 검증한다. 새 다중 디스플레이 합성과 로그인 실행은 가짜 OS 경계로 검증하며, 실제 다중 모니터 맥 및 실제 로그인 뒤 실행은 추가 확인이 필요하다.

## 소스 빌드

macOS에 .NET 10 SDK를 설치한 뒤 저장소에서 실행한다. 앱 실행은 사용자가 직접 한다.

```bash
dotnet run --project src/SnapBerry.Desktop -c Release
```

Windows에서 두 CPU용 시험 ZIP 생성:

```powershell
powershell -NoProfile -File build/Publish-MacOS.ps1
```

맥에서 기본 앱 빌드와 UI 없는 테스트:

```bash
dotnet build src/SnapBerry.Desktop -c Release
dotnet test tests/SnapBerry.Desktop.Tests -c Release
```

전체 `SnapBerry.sln`에는 WPF 프로젝트가 포함되므로 맥에서는 위 Desktop 프로젝트를 지정한다. Windows의 교차 게시 ZIP은 개발·검사용 중간 산출물이다. 사용자 설치에는 GitHub Releases의 DMG를 사용한다. 버전 태그의 Release 워크플로가 GitHub의 맥 환경에서 두 CPU용 DMG를 빌드해 공개 릴리스에 첨부한다. 로컬 배포 파일은 `dist/macos/`에 보관할 수 있지만 EXE·DMG·설치 ZIP은 Git이나 Git LFS에 추가하지 않는다. Git에는 소스·빌드 스크립트·문서를 보관한다.

## GitHub Actions 자동 검증

### 실제 UI 화면 확인

Actions → `macOS verification` → Run workflow에서 검증할 브랜치를 고르고 `ui_only`를 켜면 Apple Silicon·Intel의 실제 macOS 창을 검증한다. UI 검증 브랜치 `codex/macos-ui-refresh`의 push에서도 이 모드를 자동 실행한다. 이 모드는 화면 기록 권한 설정을 바꾸거나 설치 파일을 배포하지 않는다. 빌드 서버에 이미 있는 접근성·화면 기록 권한이 없으면 실패한다.

각 CPU에서 라이트·다크 테마의 메인·정보·단축키·설정·이미지를 붙여넣은 편집 화면·작은 창을 확인한다. 실제 Launch Services로 앱을 열고 WindowServer의 해당 앱 창 ID를 지정해 PNG를 저장한다. 접근성 트리로 주요 버튼·입력란·창 내부 배치를 검사하고, 테마별 캔버스 밝기와 PNG의 크기·색상 다양성을 확인한다. 앱은 정상 종료까지 검사하며, 테스트가 만든 설정과 앱 번들만 정리한다.

`macos-native-ui-osx-arm64` / `macos-native-ui-osx-x64` 결과에는 12개 PNG, 화면별 접근성 JSON, `native-ui-light.json`·`native-ui-dark.json`, 테스트 결과와 로그가 들어간다. JSON의 `sourceSha`는 검증한 커밋, `headless: false`와 `nativeWindowServer: true`는 실제 창 검증 여부다. 헤드리스 렌더링 자료와 구분하며, 실제 실행에 성공했는지는 해당 Actions 결과로 확인한다.

### 전체 패키지·OS 기능 검사

`.github/workflows/macos.yml`의 `macOS verification`은 관련 소스·테스트·빌드 파일의 push와 pull request에서 실행된다. 기본 브랜치에 워크플로가 반영되면 GitHub의 Actions → macOS verification → Run workflow로 수동 실행할 수도 있다.

- Apple Silicon은 `macos-15`, Intel은 `macos-15-intel`에서 각각 실행한다. SDK는 저장소의 `global.json`을 따른다. WPF 솔루션 대신 Desktop 프로젝트를 소스에서 빌드하며 저장소의 배포 바이너리나 Git LFS에 의존하지 않는다.
- Core 테스트와 Desktop 헤드리스 테스트를 실행한다. 편집기·스티커·단축키 설정 화면은 메모리에서 렌더링하며 실제 앱이나 OS 창을 열지 않는다.
- 각 실행 환경과 같은 CPU의 자체 포함 ZIP을 생성한다. Mach-O CPU, 필수 파일·런타임, Info.plist, ICNS, ZIP 실행 권한을 검사한다.
- 패키지에 들어 있는 Avalonia·Skia·hostfxr 라이브러리를 실제로 로드하고 필요한 심볼을 조회한다. Carbon도 단축키 API 심볼이 있는지만 검사하며 함수를 호출하거나 전역 단축키를 등록하지 않는다.
- 네이티브 구성 요소를 안쪽부터 로컬 시험용으로 서명하고 .NET JIT 권한을 포함해 앱 번들을 서명한다. DMG를 만든 뒤 무결성·읽기 전용 마운트·응용 프로그램 링크·포함 파일·서명·JIT 권한을 검사한다. Finder나 SnapBerry를 실행하지 않는다.
- 사용자 승인에 따라 DMG 검사 뒤 실제 설치·GUI 실행 테스트도 수행한다. DMG에서 `/Applications/SnapBerry.app`으로 복사하고 서명을 확인한 뒤 디스크를 분리한다. Launch Services로 설치 앱을 열어 표시된 메인 창·5초 안정 실행·정상 종료를 확인한다. 기존 설치나 이력이 있으면 덮어쓰지 않고 실패하며 테스트가 만든 설치·설정만 정리한다. 이 단계는 GitHub 맥 환경으로 제한한다.
- 실제 OS 기능 검증은 네이티브 클라이언트의 PNG → 시스템 클립보드 → 앱 붙여넣기, 앱 복사 → 네이티브 클라이언트의 비트맵 읽기, 붙인 이미지의 Command+C/V 복제를 검사한다. 저장된 원본·추가 이미지의 픽셀과 크기를 비교한다.
- 최초 권한 검증은 Launch Services로 설치 앱을 열고 캡처 버튼을 눌러 실제 macOS 승인 요청을 관찰한다. 승인 전 저장된 이미지가 없는지 확인하고, 시스템 설정에서 SnapBerry의 화면 기록 스위치만 UI로 허용한다. macOS 인증에는 일회용 GitHub 호스팅 VM에 새로 만든 임시 관리자 계정을 사용하고 끝나면 계정을 삭제한다. 임의 생성 암호는 인증 테스트에 업로드 경로 밖의 권한 0600 임시 파일로 전달하며 접근성 로그의 암호 값은 가린다. 기존 사용자의 암호를 읽거나 변경하지 않는다. 앱을 정상 종료하고 Launch Services로 다시 연 뒤 9개 실제 기능을 검사한다. macOS 15가 추가 화면 접근 동의를 요청하면 SnapBerry의 해당 시스템 요청만 확인해 UI의 Allow를 누르고 기록한다. 이미 승인되어 최초 요청이 없거나 정상 인증을 완료하지 못하면 통과하지 않는다. TCC 데이터베이스 편집·권한 초기화·보안 설정 우회는 하지 않는다.
- 영역·창·전체 화면 버튼은 실제 macOS 캡처를 실행하고 테스트 창의 네 가지 색상이 저장된 이미지에 포함되는지 확인한다. 전역 단축키 Control+Shift+1/2/3은 테스트 창이 전면이고 SnapBerry가 비활성인 상태에서 CGEvent로 입력하며, 실제 캡처와 이력 저장까지 확인한다. OS 선택기에는 테스트 영역 드래그·테스트 창 클릭을 입력한다.
- `install-smoke/first-use-consent.json`은 실제 요청 관찰·승인 조작·스위치 상태·재실행·승인 후 실제 기능 결과를 기록한다. 각 단계의 `consent-*.png`와 접근성 트리도 보관한다. `native-features.json`은 9개 기능별 판정과 테스트 클라이언트의 권한 상태다. 누락·권한 거부·타임아웃은 성공으로 처리하지 않는다. `gui-launch.json`, 실제 캡처·클립보드 `.qcapture`, 실행·시스템 로그도 보관한다. UI 조작과 캡처는 일회용 GitHub 실행 환경에서만 수행하며 일반 .NET 검색 경로를 비워 자체 포함 실행도 검사한다.
- 실행 결과의 Artifacts에서 `macos-results-osx-arm64` / `macos-results-osx-x64`의 TRX·화면 PNG·ZIP/DMG 검사 로그를 내려받을 수 있다. 결과는 14일 보관하며 실패한 실행에서도 이미 생성된 테스트 자료를 보관한다. 검증 중 생성한 설치 파일은 업로드하지 않는다. 사용자 설치 파일은 버전 태그의 Release 워크플로가 GitHub Releases로만 배포한다.

패키지 검사만 Windows에서도 실행할 수 있다. Python 3 표준 라이브러리만 필요하다.

```powershell
python -m unittest discover -s build/tests -v
python build/verify_macos.py dist/macos/SnapBerry-macos-arm64-unsigned.zip --arch arm64
```

맥에서 같은 CPU용 패키지의 라이브러리 로딩까지 검사하려면 위 검사 명령에 `--native`를 더한다. 한 CPU만 게시하려면 `pwsh -File build/Publish-MacOS.ps1 -RuntimeIdentifier osx-arm64` 또는 `osx-x64`를 사용한다. 인수가 없으면 기존처럼 두 CPU 모두 게시한다.

빌드 담당자가 맥에서 DMG를 생성하는 명령은 다음과 같다. 설치하는 사용자는 이 명령을 실행하지 않는다.

```bash
python3 build/package_macos_dmg.py dist/macos/SnapBerry-macos-arm64-unsigned.zip --arch arm64 --output dist/macos/SnapBerry-macos-arm64.dmg
```

CI의 설치·실행 테스트는 빌드 서버에서 만든 파일을 대상으로 하며, 브라우저 다운로드의 격리 속성과 Gatekeeper의 첫 실행 보안 승인 절차를 재현하지 않는다. 해당 보안 경고가 발생하면 자동 승인하거나 우회하지 않고 실패한다. 화면 기록 권한 승인은 사용자의 별도 요청에 따라 실제 macOS UI로 시험한다. 사용자 맥의 관리 정책·메뉴 막대 조작·Developer ID 서명·공증은 검증 범위에 포함하지 않는다. 검증 코드 추가와 실제 실행 성공은 구분하며 해당 Actions 보고서를 확인한다.
