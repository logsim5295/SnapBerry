# QuickCapture Verification

## Windows 캡처 시 편집기 잔상 방지 (2026-10-21)

- 캡처 전의 `Hide()` + 고정 120ms 대기를 제거했다. 작업 중 상태가 스티커 선택창을 숨기기 전부터 편집기와 소유 창의 DWM 전환 효과를 일시 억제하고, 창 숨김 → WPF 렌더 큐 대기 → `DwmFlush` → 숨김 상태 확인 → 화면 읽기 순서로 진행한다. 준비 실패 시 이미지를 읽지 않으며 성공·취소·오류 뒤에는 전환 효과 억제를 해제한다.
- 새 회귀 테스트는 렌더 완료 전 화면 읽기 차단, 숨김·렌더·합성·가시성 확인·캡처 실패 복구, 취소 시 편집기 숨김 유지, 여러 창의 설정·부분 실패·복구 오류 정리를 검사한다. 표시하지 않은 일반·투명 WPF HWND에서 실제 DWM 설정 API도 검사했다. 이 테스트에서 지원되지 않는 전환 속성 읽기가 실패하는 것을 확인한 뒤 문서화된 설정 API만 사용하도록 수정했다.
- 관련 Windows 비대화형 테스트 36개와 Core 전체 45개가 통과했다. 일반 Release 솔루션 빌드는 경고·오류 0개다. 코드 리뷰에서 추가 수정 사항은 발견되지 않았다.
- 실행 중인 SnapBerry가 없는 상태에서 Portable 단일 EXE를 갱신했고, 번들·난독화·내장 자원·네이티브 진입점·공개 API·기존 JSON 계약 검증을 통과했다. 게시 전후 일반 Release의 앱·Core DLL SHA-256이 동일함을 확인했다.
- SnapBerry 실행, 실제 창 표시·활성화, 화면 캡처 및 전체 Windows UI 테스트는 실행하지 않았다. 실제 화면의 잔상 제거 여부는 대화형 재현으로 검증하지 않았으며, 이번 자동 검증은 준비 순서·실패 복구·네이티브 호출 호환성을 대상으로 한다.

## GitHub 맥 환경의 실제 설치·GUI 실행 검사 추가 (2026-10-21)

- 사용자가 GitHub Actions에서 실제 설치·실행 테스트를 명시적으로 요청해, 기존 DMG 검사 다음에 앱을 실제로 여는 단계를 추가했다. Windows 로컬에서는 실행하지 않는다. 맥·GitHub Actions·CPU 검사를 통과해야 설치가 시작된다.
- 기존 설치와 사용자 데이터가 없는지 확인한 뒤 DMG에서 /Applications로 ditto 복사·서명 검사를 수행하고 설치 디스크를 분리한다. Launch Services로 해당 설치 경로의 새 앱 인스턴스를 열고 PID·번들 경로·화면에 표시된 메인 창·5초 안정 실행·정상 종료를 검사한다. 실패 시 테스트가 시작한 PID와 번들 경로를 확인해 정리한다.
- 창 표시는 WindowServer의 메타데이터로 확인한다. 화면 캡처·클립보드·키 입력은 사용하지 않고, 전역 단축키는 새 테스트 설정에서 비활성화한다. 기존 설치·이력을 덮어쓰지 않으며 테스트가 만든 설치와 설정만 삭제한다. 일반 .NET 검색 경로를 비우고 JSON 결과·앱 시스템 로그·생성된 크래시 보고서를 보관한다.
- Windows 로컬에서 Python 검사 19개 통과·Unix 링크 검사 1개 건너뜀과 actionlint 검사를 확인했다. 실제 Swift 컴파일과 앱 설치·실행은 양쪽 CPU의 macOS Actions에서 확인한다. 앱 소스와 Windows Portable EXE는 변경하지 않았다.
- [Actions 실행 39553879043](https://github.com/simlog1234/SnapBerry/actions/runs/39553879043)에서 Apple Silicon·Intel 모두 성공했다. 두 환경의 실제 /Applications 설치, DMG 분리 후 Launch Services 실행, 메인 창 표시, 5초 안정 실행, 정상 종료를 확인했다. JSON 보고서의 finishedLaunching·normalTermination·success는 모두 true이며 창 크기는 ARM 1024×681, Intel 1240×848이었다. CPU별 결과와 GUI 검사를 통과한 DMG를 보관했다.
- 브라우저 다운로드의 격리 속성·Gatekeeper 첫 실행 승인과 캡처 권한·앱 간 클립보드·전역 키 입력·Developer ID 공증은 이 테스트의 성공만으로 검증됐다고 판단하지 않는다.

## 터미널 명령 없이 설치하는 맥 DMG 추가 (2026-10-21)

- 사용자 설치 파일을 CPU별 DMG로 추가했다. SnapBerry.app·Applications 링크·GUI 설치 안내만 포함하며 사용자용 터미널 스크립트와 별도 .NET 설치 절차를 제거했다. DMG 열기 → 응용 프로그램 폴더로 옮기기 → 앱 실행으로 안내한다.
- 첫 macOS CI에서 Contents/MacOS의 관리형 DLL을 서명 도구가 미서명 코드로 판단하는 오류를 확인했다. Apple TN2206에 따라 주 실행 파일 외의 런타임 자료를 Resources/RuntimePayload로 옮기고 기존 탐색 경로는 상대 심볼릭 링크로 유지한다. 원본 DLL·JSON·라이브러리·한글 리소스가 두 경로에서 같은 파일로 열리는지 맥 전용 회귀 테스트를 추가했다.
- 빌드 서버에서 네이티브 Mach-O 구성 요소를 안쪽부터 로컬 시험용으로 서명하고, 앱에 Hardened Runtime과 .NET JIT·라이브러리 로딩 권한을 지정한다. --deep은 서명할 때 사용하지 않고 검증에만 사용한다. 디버그 접근 권한은 포함하지 않는다.
- DMG 무결성과 읽기 전용 마운트, Applications 링크, 포함 파일, 번들 서명·JIT 권한을 검사하도록 CI에 연결했다. 마운트는 -nobrowse/-noautoopen을 사용하고 앱·Finder 창은 열지 않는다. DMG 로그도 실패 시 보관한다. 한글 파일명은 HFS+ 정규화 차이를 고려한다.
- Windows 로컬의 설치 준비·패키지 회귀 테스트 16개와 actionlint가 통과했고 일반 Release 빌드는 경고·오류 0개다. 실제 서명·DMG 생성·마운트 검사는 같은 브랜치를 푸시해 두 CPU의 macOS Actions에서 수행한다. 앱 소스와 Windows Portable EXE는 변경하지 않았다.
- 수정 후 [macOS Actions 실행 39552250120](https://github.com/simlog1234/SnapBerry/actions/runs/39552250120)에서 Apple Silicon·Intel 모두 성공했다. 각 CPU에서 패키지 테스트 17개·Core 45개·Desktop 헤드리스 62개(총 124개), 네이티브 로딩, 서명·JIT 권한, DMG 무결성·읽기 전용 마운트·Applications 링크 검사를 통과하고 CPU별 DMG를 보관했다. 앱을 직접 실행하는 GUI 설치 테스트는 수행하지 않았다.
- 사용자는 Apple Developer Program 미가입 상태다. 로컬 시험용 서명은 Developer ID·Apple 공증을 대신하지 않으므로 첫 실행 시 ‘그래도 열기’ 승인이 필요할 수 있다. Gatekeeper·격리 속성을 자동 변경하지 않는다. 실제 GUI 설치·실행·캡처 권한은 별도 사용자 확인 대상이다.

## GitHub Actions 맥 자동 검증 추가 (2026-10-21)

- `.github/workflows/macos.yml`에 Apple Silicon(`macos-15`)·Intel(`macos-15-intel`) 작업을 추가했다. 관련 파일의 push·pull request와 수동 실행을 지원하며 `global.json` SDK를 사용한다. 권한은 contents:read이고 checkout 인증 정보를 남기지 않으며 Windows EXE의 LFS 본문을 내려받지 않는다. 외부 액션은 확인한 커밋 SHA로 고정했다.
- 각 작업은 Desktop Release 빌드, Core·Desktop 헤드리스 테스트, 해당 CPU의 자체 포함 ZIP 게시, ZIP 구조·CPU·권한·Info.plist·ICNS·런타임 검사와 Avalonia/Skia/hostfxr/Carbon 라이브러리 로딩·심볼 조회를 수행한다. 앱 실행·Carbon API 호출·전역 등록·클립보드·캡처·OS 창 표시는 포함하지 않는다.
- 실패하더라도 생성된 TRX·메모리 화면 PNG·패키지 검사 로그를 보관하고 성공한 시험 ZIP을 CPU별로 올리도록 구성했다. 보관 기간은 14일이다. 게시 스크립트는 CPU 선택 인수를 추가했고 인수가 없으면 기존처럼 두 CPU를 모두 만든다.
- Windows 로컬에서 actionlint 1.7.12와 PowerShell 7 문법 검사, 패키지 검사기 회귀 테스트 11개, Desktop 헤드리스 62개, Core 45개가 통과했다. 실제 단일 CPU 게시와 새 ZIP 검사·기존 두 CPU ZIP 검사, TRX·화면 PNG 생성을 확인했고 일반 Release 솔루션 빌드는 경고·오류 0개다. 잘못된 CPU·실행 권한 유실·런타임 누락·불완전 Mach-O·잘못된 번들/ICNS·중복/잘못된 ZIP 경로·호스트와 CPU가 다른 네이티브 검사를 거부하는지 검사했다.
- 독립 코드 리뷰에서 Critical/Important 결함은 발견되지 않았다. GitHub에 푸시하거나 워크플로를 원격 실행하지 않았으므로 실제 macOS 작업의 성공은 아직 미확인이다. 캡처 권한·앱 간 클립보드·전역 키 입력·메뉴 막대 조작·Developer ID 서명·공증은 이후 별도 확인 대상이다. 이번 변경은 CI·검사 스크립트와 문서이며 앱 소스·Windows Portable EXE는 변경하지 않았다.

## macOS 클립보드·스티커·전역 단축키 확장 (2026-10-20)

- Command+C/V와 버튼을 클립보드 서비스에 연결했다. 이력은 합성 이미지, 선택 이미지 개체는 원본 PNG·출력 표시 크기를 복사한다. 표준 비트맵과 앱 전용 PNG·크기 형식을 함께 제공한다. 현재 문서 위 붙여넣기·빈 편집기 열기·Undo/Redo·소수 크기와 자산 한도를 검증했다. 텍스트 입력란은 기존 복사·붙여넣기를 유지한다.
- Windows의 80개 스티커 정의를 순수 C# 소스로 분리해 공유하고 기존 WPF PNG 생성과 공개 API·ID를 유지했다. 맥은 Skia로 PNG를 생성하며 맥 키보드 8종을 더했다. 한국어 카테고리·검색·연속 삽입·키 라벨 변경·저장 재열기를 제공한다.
- Carbon 전역 단축키 기본값은 Control+Shift+1/2/3이다. 사용 여부·키 조합 설정, 중복 검사·등록 충돌과 설정 쓰기 실패 시 이전 등록 복구, 작업 중 요청 차단을 추가했다. 창 닫기는 저장 후 숨김이며 메뉴 막대에서 편집기·캡처·단축키 설정·종료를 제공한다. 명시 종료는 저장 성공 뒤 등록을 해제한다.
- 독립 코드 리뷰에서 발견한 Carbon의 64비트 ByteCount/ItemCount 선언을 nuint로 수정했다. 함수 매개변수 크기 회귀 테스트가 수정 전 uint/nuint 불일치로 실패하고 수정 후 통과했다. 이벤트 구조체·ID·수명·등록 정리는 가짜 백엔드로 검사한다.
- Desktop 전체 62개, 관련 Windows 비대화형 테스트 94개, Core 전체 45개가 통과했다. 일반 Release 솔루션 빌드는 경고·오류 0개다. 메모리 Avalonia 백엔드에서 편집기·화이트/다크 스티커 선택창·단축키 설정창을 렌더링하고 버튼·필터·메뉴·닫기/종료·저장 실패 경로를 검증했다.
- 실제 앱 실행·OS 창 표시·OS 클립보드·전역 단축키 등록·화면 캡처·물리 입력은 실행하지 않았다. Windows 테스트 일부는 자동 승인 검토에서 창 표시 가능성을 이유로 차단되어, 비대화형 카탈로그·PNG 테스트 두 메서드만 추가 실행했다. 맥 실기기의 앱 간 클립보드·Carbon 이벤트 전달·메뉴 막대·권한·CPU별 실행과 Developer ID 서명·공증은 추가 검증 대상이다.
- 실행 중인 SnapBerry 프로세스가 없는 상태에서 Windows Portable 단일 EXE를 갱신하고 번들·난독화·공개 API·리소스·기존 JSON 계약 검증을 통과했다. 일반 개발용 Release 출력을 보존했다. Apple Silicon·Intel용 자체 포함 macOS 시험 ZIP을 다시 생성하고 Mach-O CPU·라이브러리·Info.plist·ICNS·ZIP 실행 권한·최신 안내 포함 여부를 검사했다.

## macOS 캡처·편집 시험 버전 추가 (2026-10-20)

- 기존 WPF 앱을 유지하고 별도 `SnapBerry.Desktop`(.NET 10, Avalonia 12.1.4, SkiaSharp 3.119.4)을 추가했다. Core의 문서·도형 배치·선택·Undo/Redo와 기존 `.qcapture` 저장소 소스를 재사용한다. 저장소의 WPF 의존 크기 검사만 같은 조건의 Core 검사로 옮겼다.
- macOS 기본 screencapture 도구를 통해 영역·창·주 모니터 화면을 캡처한다. 프로세스 인수는 ArgumentList로 전달하고 사용자 명령에서만 실행한다. 캡처 전 저장 실패 시 창을 숨기지 않으며 취소·오류 시 기존 문서를 유지하고 창을 복원한다. ScreenCaptureKit 직접 브리지는 포함하지 않았다.
- 이미지 열기·추가, 화살표·텍스트·사각형·모자이크·번호, 선택·이동·크기 조절·복제·삭제, 자르기·확대·가운데 버튼 팬, PNG 저장, 이력·이름/날짜 검색·이름 변경·확인 후 삭제, 화이트·다크 테마와 앱 안의 Command 단축키를 제공한다. PNG는 같은 렌더러를 사용하고 임시 파일 완성 후 교체한다.
- 새 Desktop 테스트 25개, 관련 Windows 헤드리스 테스트 92개, Core 전체 45개가 통과했다. 전체 Release 솔루션 빌드는 경고·오류 0개다. Avalonia 화면 검증은 네이티브 창 대신 UseHeadless 메모리 백엔드의 레이아웃·렌더링을 사용했다. 실제 앱·OS 창·클립보드·화면 캡처·포커스 이동은 실행하지 않았다.
- 리뷰에서 발견한 텍스트 길이 제한 누락과 포커스 이동에 의한 스타일 변경을 수정했다. 8192자 입력 제한, 포인터 오류 전달, 선택 속성 동기화·무변경 보호·개별 속성 변경을 추가했고 회귀 테스트를 통과했다.
- 두 CPU용 자체 포함 `.app` 시험 ZIP을 생성하고 Mach-O 헤더·CPU·필수 네이티브 라이브러리·Info.plist·ICNS·ZIP 실행 권한을 검사한다. macOS 14 이상을 대상으로 한다. Developer ID 서명·공증과 맥 실기기의 권한·캡처·Retina·CPU별 실행은 검증하지 않았다. 맥에서 로컬 시험용 서명을 확인하는 스크립트와 docs/macos.md 안내를 제공한다.
- Windows 리본·전체 스티커 카탈로그·클립보드·전역 단축키·메뉴 막대 상주·자동 실행·기기 간 동기화는 이번 맥 시험 버전에 포함하지 않는다.
- 일반 Windows Release 출력을 보존하면서 Portable EXE를 갱신했고 실제 번들의 난독화·공개 API·리소스·기존 JSON 계약 검증을 통과했다. 실행 중인 SnapBerry가 없는 상태에서 게시했다.

## v1.0.1 버전 갱신 (2026-10-20)

- 앱의 제품 버전을 1.0.1로 지정하고 어셈블리·파일·Windows 매니페스트 버전을 1.0.1.0으로 맞췄다. 정보 창은 빌드 메타데이터에서 1.0.1을 표시한다.
- 정보 창 관련 헤드리스 테스트 5개가 통과했고 일반 Release 솔루션 빌드는 경고·오류 0개다. 일반 Release DLL과 Portable EXE의 제품·파일 버전을 앱 실행 없이 확인했다.
- 실행 중인 SnapBerry가 없는 상태에서 Portable 단일 EXE를 갱신하고 번들·난독화·리소스·공개 API·기존 JSON 계약 검증을 통과했다. 일반 개발용 Release 출력을 보존했으며 실제 앱 실행·정보 창 표시 테스트는 실행하지 않았다.

## 다크 모드에서 설정 진입 시 이력 목록의 흰색 배경 수정 (2026-10-20)

- 설정 진입의 SetBusy(true)가 이력 목록을 비활성화하면 WPF 기본 ListBox 템플릿이 배경을 흰색으로 덮어썼다. 이력 전용 템플릿에서 현재 테마의 Background를 유지하고 기존 항목 스타일의 비활성 표시를 사용한다. ListBox와 항목 컨테이너, PART_ScrollViewer, ItemsPresenter 및 논리 스크롤 구조를 유지한다.
- 창 없이 실제 설정 작업 경로와 목록을 렌더링해 수정 전 다크 모드의 빈 목록·항목이 있는 목록에서 #FF22252D 대신 #FFFFFFFF가 나오는 실패를 확인했다. 새 회귀 테스트 6개는 화이트·다크의 설정 진입·미리보기 취소·저장 중 테마 전환·오류 후 복귀, 다중 선택·스크롤·200개 항목 가상화를 검증한다. 수정 후 오프스크린 렌더링도 확인했다.
- 관련 Windows 헤드리스 테스트 92개와 Core 전체 45개가 통과했다. 일반 Release 솔루션 빌드는 경고·오류 0개다.
- SnapBerry를 트레이 메뉴에서 종료했다는 사용자 확인 후 실행 중인 프로세스가 없는 상태에서 Portable 단일 EXE를 갱신했다. 번들·난독화·리소스·공개 API·기존 JSON 계약 검증을 통과했고 일반 개발용 Release 출력을 보존했다.
- 실제 SnapBerry 실행, 설정창 표시·클릭·포커스 이동·물리 입력 테스트는 실행하지 않았다.

## 마우스 가운데 버튼 드래그로 화면 팬 이동 (2026-10-20)

- 이미지 영역에서 가운데 버튼을 누른 채 끌면 가로·세로 스크롤을 포인터 방향으로 이동한다. 화면 DIP 좌표와 요청 오프셋을 누적하고 스크롤 범위로 제한해 확대율과 무관하게 같은 화면 거리를 이동하며 가장자리에서 방향을 바꾸면 바로 반응한다. 스크롤 가능한 공간이 없는 이미지는 경계 밖으로 이동하지 않는다.
- 시작 시 확정되지 않은 그리기 제스처를 취소하고 뷰포트가 마우스를 캡처한다. 손 모양 커서를 표시하며 가운데 버튼을 놓거나 Esc·캡처 상실·창 숨김/비활성화/닫기·작업 시작·확대율/문서/뷰포트 크기 변경 시 종료한다. 원래 커서와 ForceCursor 상태를 복원하며 다른 컨트롤에 넘어간 캡처는 해제하지 않는다. 드래그 중 다른 마우스 버튼과 휠 확대 입력은 편집으로 전달하지 않는다.
- 문서·주석·선택·확대율·실행 취소 토큰은 팬 이동으로 변경하지 않는다. 빠른 이동은 스크롤 반영을 기다리지 않고 누적하며, 연속 팬을 다시 시작할 때는 이전 요청을 배치에 반영해 위치가 되돌아가는 문제를 방지한다.
- 팬 컨트롤러 부재와 연속 드래그의 이전 스크롤 유실을 수정 전 실패로 확인했다. 새 헤드리스 테스트 13개가 실제 ScrollViewer와 EditorCanvas에서 이동·빠른 연속 이동·경계·역방향·버튼 분기·스크롤바 제외·캡처 실패·해제·Esc·캡처 상실·중단·커서 복원·문서 보존을 검사한다. 실제 마우스 캡처/해제 경계만 대체 구현을 사용했다.
- 관련 Windows 헤드리스 테스트 126개와 Core 전체 45개가 통과했으며 일반 Release 솔루션 빌드는 경고·오류 0개다.
- 실행 중인 SnapBerry가 없는 상태에서 Portable 단일 EXE를 갱신했고 번들·난독화·리소스·공개 API·기존 JSON 계약 검증을 통과했다. 일반 개발용 Release 출력을 보존했다.
- 실제 SnapBerry 실행, 창 표시·활성화, 물리적인 가운데 버튼·드래그·캡처 상실·창 밖 버튼 해제 테스트는 실행하지 않았다.

## 다크 모드의 보기 리본 은색 배경 수정 (2026-10-20)

- 비활성 리본 탭은 템플릿이 존재해도 시각 자식을 노출하지 않아, 기존 시각 트리 순회가 보기 탭의 기본 은색 OverlayRect를 놓쳤다. 템플릿 이름 범위에서 배경 부품을 직접 찾아 테마를 적용하고 기존 순회는 이름 없는 테두리 처리에 유지한다.
- 좁은 창·탭 전환 중 리본 그룹의 템플릿이 다시 만들어지는 경로도 확인했다. 기본 RibbonGroup의 동작을 상속한 ThemedRibbonGroup에서 템플릿 적용 직후 같은 테마 처리를 호출한다. 기존 그룹 크기 정의·접힘·명령 동작은 유지한다.
- 변경 전 홈→보기 전환에서 은색 그라데이션으로 실패한 뒤 수정했다. 새 회귀 테스트 4개는 1240·760 DIP에서 홈/보기 각각 시작, 반복 탭 전환, 화이트·다크 왕복 전환, 탭·그룹 배경 부품과 리본 바탕을 검사한다. 관련 Windows 헤드리스 테스트 23개와 Core 전체 45개가 통과했다. 보기 리본의 오프스크린 렌더링도 확인했다.
- 실제 SnapBerry 실행, 탭 클릭·창 표시·팝업·물리 입력 테스트는 실행하지 않았다.
- 일반 Release 빌드는 경고·오류 0개다. 실행 중인 SnapBerry가 없는 상태에서 Portable 단일 EXE를 갱신했으며 번들·난독화·리소스·공개 API·기존 JSON 계약 검증을 통과했다. 일반 Release 출력을 보존한 별도 난독화 DLL 복사본에서도 탭 전환을 포함한 테마 테스트 19개가 통과했다.

## 다크 모드의 선택된 홈 탭 대비 수정 (2026-10-20)

- 선택된 리본 탭은 일반 Background 대신 CheckedBackground를 사용하므로 기본 흰색 그라데이션이 남아 밝은 글자와 구분되지 않았다. RibbonTabHeader의 선택·마우스 올림·키보드 포커스 배경과 테두리를 공통 테마 자원으로 지정했다.
- 창을 표시하거나 실제 입력을 사용하지 않고 리본이 전달하는 읽기 전용 선택 상태를 재현했다. 새 회귀 테스트 4개에서 수정 전 선택 탭의 기본 그라데이션으로 실패했고, 수정 후 화이트·다크의 선택/미선택 배경·글자 및 테마 왕복 전환을 확인했다. 선택된 다크 홈 탭의 오프스크린 렌더링도 검토했다.
- 관련 Windows 헤드리스 테스트 19개와 Core 전체 45개가 통과했다. 실제 앱 실행, 탭 클릭·마우스 올림·키보드 포커스 및 창 표시 테스트는 실행하지 않았다.
- 일반 Release 빌드는 경고·오류 0개다. 실행 중인 SnapBerry가 없는 상태에서 Portable 단일 EXE를 갱신했고 번들·난독화·리소스·공개 API·기존 JSON 계약 검증을 통과했다. 일반 Release 출력을 보존한 별도 난독화 DLL 복사본에서도 새 탭 회귀 테스트를 포함한 테마 테스트 15개가 통과했다.

## 화이트·다크 테마 선택 (2026-10-20)

- 설정에 ‘화면 테마’ 화이트 모드·다크 모드를 추가했다. 설정창에서만 미리 보고 저장하면 편집기와 열린 대화상자·스티커 창 및 트레이 메뉴에 적용한다. 취소는 앱 테마를 변경하지 않는다. 선택은 설정 파일에 보관하며 기존 설정과 알 수 없는 테마 값은 화이트로 복원하고 다른 설정은 유지한다.
- 창·리본·이력·빈 편집 영역·상태 표시줄·입력 컨트롤·스크롤바·각 편집 대화상자가 공통 색상 자원을 사용한다. UI 아이콘의 선과 내부 배경 대비도 테마에 맞춘다. 이미지·스티커 원본·주석 색상과 문서·실행 취소 상태는 변경하지 않는다. 일반 제목 표시줄은 기존 핸들이 있을 때 Windows 다크 표시를 요청한다.
- WPF가 공유 브러시를 동결하는 동작을 고려해 색상 바인딩을 사용하고, 난독화 후에도 바인딩 대상 이름을 보존한다. 기본 리본의 밝은 광택 층은 다크에서만 교체하고 화이트로 되돌리면 원래 템플릿 값을 복원한다.
- 새 테마 테스트 12개가 설정 선택·저장·구형 설정 호환·잘못된 값 복원·편집 내용 보존·리본 왕복 전환·트레이 팔레트·아이콘 내부 대비를 확인한다. 구현 전 선택 UI 부재와 다크 미적용, 수정 중 리본 그라데이션·화이트 복원·아이콘 대비 실패를 확인하고 수정했다. 관련 Windows 헤드리스 테스트 179개와 Core 전체 45개가 통과했으며 일반 Release 솔루션 빌드는 경고·오류 0개다.
- 창 없이 편집기와 설정·정보·스티커·텍스트·크기 변경·삭제·색상·트레이 안내 대화상자를 두 테마로 렌더링해 검토했다. 실제 SnapBerry 실행, 창·팝업 표시, 포커스 이동·물리 입력·화면 캡처 및 실제 제목 표시줄 전환 테스트는 실행하지 않았다.
- 실행 중인 SnapBerry가 없는 상태에서 Portable 단일 EXE를 갱신하고 번들·난독화·리소스·공개 API·기존 JSON 계약 검증을 통과했다. 일반 Release 출력을 보존한 별도 난독화 DLL 복사본에서도 테마 테스트 11개가 통과했다. 비공개 트레이 렌더러를 직접 참조하는 테스트는 일반 Release에서 검증했다.

## 전체 동작 일관성 7개 수정 (2026-10-20)

- 캡처 오버레이의 일반 닫기도 취소 완료로 연결한다. 완료 처리는 한 번만 수행하고 이미 닫힌 창은 다시 닫지 않아 Alt+F4 취소 후 작업 중 상태가 남지 않게 했다.
- 주석 삭제는 드래그·미리보기를 먼저 취소한다. Delete 키와 리본 삭제가 같은 경로를 사용하며 마우스를 놓을 때 삭제한 주석을 다시 교체하려는 오류를 막는다.
- 기존 주석의 드래그 미리보기를 원래 배열 위치에서 교체하여 이동 중·확정 후·PNG의 겹침 순서를 맞췄다.
- 텍스트 크기 변경은 글자 크기로 가로·세로 경계를 함께 측정하고 직접 입력과 같은 1~4096 범위를 적용한다. 최소 생성 크기는 새 주석에만 적용하여 최소 글자 크기의 짧은 텍스트도 이동·축소를 확정하고 Undo/Redo할 수 있다.
- 설정 읽기·단축키 복원·오류 보고를 포함한 전체 경로를 보호하여 실패해도 작업 중 상태를 해제한다. 설정 파일 잠금 실패와 오류 보고 실패도 검증했다.
- 설정 저장 실패 시 Windows 자동 실행 변경을 이전 값으로 복원한다. 자동 실행 값이 같으면 레지스트리를 갱신하지 않으며 복원마저 실패하면 두 오류를 함께 보고한다. 테스트에서는 대체 작업만 사용하여 실제 레지스트리와 전역 단축키를 변경하지 않는다.
- 이력 목록 메타데이터에 저장소 내부 갱신 번호를 부여해 저장·외부 파일 변경 시 썸네일을 갱신한다. 같은 크기에서 주석만 바뀐 경우에도 새 픽셀을 표시하고 변경 없는 파일은 기존 썸네일을 재사용한다. 이력 파일의 JSON 형식은 변경하지 않는다.
- 수정 전 실패를 확인한 뒤 새 헤드리스 회귀 테스트 19개가 통과했다. 관련 Windows 헤드리스 테스트 164개와 Core 전체 45개 통과. 일반 Release 빌드는 경고·오류 0개다.
- Portable 단일 EXE 게시와 실제 번들의 난독화·공개 API·리소스·JSON 계약 검증 통과. 별도 난독화 DLL 복사본에서도 썸네일·이력 이름·PNG 파일명 테스트 34개가 통과했다. 비공개 필드·메서드 이름에 의존하는 드래그·셸 진단은 난독화로 이름이 변경되므로 일반 Release에서 검증한다.
- 실제 SnapBerry 실행, 캡처창 표시·Alt+F4 물리 입력, 마우스 드래그·화면 캡처, 레지스트리 변경 테스트는 실행하지 않았다. 표시하지 않은 오버레이의 Close 이벤트와 메모리 렌더링·입력 경계 대체 구현으로 검증했다.

## PNG 기본 파일명에 이력 이름 적용 (2026-10-20)

- PNG 저장 대화상자에 현재 이력 이름을 전달한다. 이름 변경 후 다음 저장에는 새 이름을 사용하며 이름이 없으면 기존 `Capture_yyyyMMdd_HHmmss.png`를 제안한다. 기본 저장 폴더·덮어쓰기 확인·사용자가 직접 지정한 경로의 내보내기 동작을 유지한다.
- 파일명에서만 Windows 금지 문자를 `_`로 바꾸고 끝의 점·공백을 제거한다. 이미 `.png`로 끝나는 이름은 확장자를 중복하지 않으며 예약 장치 이름에는 `_`를 앞에 붙인다. 이력 이름·문서·실행 취소 상태는 변경하지 않는다.
- 새 헤드리스 테스트 16개가 기존 코드에서 실패한 뒤 수정 후 통과했다. 한글·공백·확장자·금지 문자·예약 이름·이름 없음 및 저장 직전 이름 변경의 전달을 확인한다. 관련 Windows 헤드리스 테스트 37개와 Core 전체 45개 통과. 일반 Release 빌드는 경고·오류 0개다.
- 실제 앱 실행·저장 대화상자 표시·키 입력 테스트는 실행하지 않았다.
- Portable 게시와 실제 EXE의 난독화·공개 API·리소스·JSON 계약 검증을 통과했다. 일반 Release와 분리한 난독화 DLL 복사본에서도 새 테스트 16개가 통과했다.

## 캡처 이력 이름·이름 검색 (2026-10-20)

- 이력의 우클릭 이름 변경 메뉴와 단일 선택 F2를 연결했다. 한 줄 100자까지 입력하고 앞뒤 공백을 제거하며, 비우면 날짜 표시로 복원한다. 이름은 `.qcapture` 메타데이터에 저장하고 기존 이름 없는 문서는 그대로 연다. 파일명·캡처 시각·원본 이미지는 유지한다.
- 목록 위 검색창은 전체 이력의 이름을 대소문자 구분 없이 부분 검색한 뒤 최신순으로 페이지를 나눈다. 검색어 변경 시 이전 검색의 목록·썸네일 작업을 중단하고 마지막 검색 결과를 반영한다. 결과가 없어도 편집 문서는 유지하며, 새 문서·복제본을 열면 검색을 해제한다.
- 이름 변경 전에 자동 저장을 완료하고, 저장 성공 후 활성 모델의 이름만 갱신한다. 주석 문서·실행 취소·다시 실행 스택은 교체하지 않으며 저장 실패 시 기존 이름을 유지한다. 손상 이력은 이름 변경을 비활성화한다.
- 기능 부재로 이름 저장·검색 테스트가 실패하는 것을 먼저 확인했다. 새 헤드리스 테스트 16개는 저장 후 재열기, 기존 형식, 105개 이력의 검색 후 페이지 구분, 활성·비활성 대상, 자동 저장과 Undo/Redo, 취소·이름 비우기·파일 교체 실패, 빠른 검색어 변경, 새 문서 선택, 입력 한도, 우클릭·F2 및 손상 항목 제한을 검증한다.
- 관련 Windows 헤드리스 테스트 120개와 Core 전체 45개 통과. 일반 Release 빌드는 경고·오류 0개다. 실제 SnapBerry 실행·창 표시·키보드 입력·화면 캡처 테스트는 실행하지 않았다.
- Portable 단일 EXE 게시와 실제 번들의 난독화·공개 API·리소스·기존 및 이름 포함 JSON 계약 검증을 통과했다. 일반 Release 출력을 보존한 별도 복사본의 난독화 DLL에서도 새 테스트 16개가 통과했다.

## 삭제 확인 기본 버튼 변경 (2026-10-20)

- 삭제 확인창의 기본 버튼과 초기 포커스를 삭제로 변경했다. Enter를 취소로 가로채던 처리를 제거하여 기본 버튼을 실행하도록 했고, 취소 버튼·Esc·창 닫기의 취소 동작은 유지한다.
- 기본 버튼과 초기 논리 포커스를 창 없이 확인하는 테스트가 기존 코드에서 실패한 뒤 수정 후 통과했다. 관련 Windows 헤드리스 테스트 19개와 Core 전체 45개 통과. 일반 Release 빌드는 경고·오류 0개다.
- 기존 대화형 테스트의 Enter 예상값과 기본 포커스 검증도 새 동작으로 갱신했으나, 실제 삭제창 표시·물리 키 입력 테스트는 실행하지 않았다.
- Portable 게시와 EXE 번들의 난독화·리소스·공개 API·JSON 계약 검증을 통과했다. 난독화 DLL 복사본에서도 삭제창의 헤드리스 테스트 2개가 통과했다.

## 선택한 이미지 객체의 복사·붙여넣기 복제 (2026-10-20)

- 캔버스에서 붙여넣은 이미지 객체를 선택하고 Ctrl+C 하면 해당 이미지의 원본 픽셀만 복사한다. 배경·겹친 주석은 제외하며 Ctrl+V는 표시 크기를 유지한 새 객체를 중앙에 추가한다. 이미지 객체 선택이 없으면 기존 합성 이미지 복사, 이력 목록에서는 선택 이력 복사, 텍스트 입력에서는 텍스트 복사를 유지한다. 리본 복사 버튼은 기존 전체 이미지 내보내기 동작을 유지한다.
- PNG와 Bitmap을 함께 제공하고 선택 이미지의 출력상 크기를 전용 클립보드 형식에 보관한다. 투명도·소수 크기·비균등 출력 배율을 유지하며, 캔버스보다 크게 늘린 이미지도 복제할 때 다시 축소하지 않는다. 반복 복제는 원본 자산을 사용해 리샘플링에 따른 화질 저하를 피한다. 외부 일반 이미지는 기존 캔버스 맞춤 동작을 유지한다.
- 크기 메타데이터는 불변 문화권으로 읽고 쓰며, 잘못된 값은 일반 이미지로 처리한다. 문서 좌표 한도를 넘는 값은 자산·문서 변경 전에 거부한다. 이미지 객체만 복사한 경우 전체 문서를 내보낸 상태로 표시하지 않는다.
- 수정 전 전체 캔버스를 복사하는 실패 2개와, 300×150 객체 및 소수 크기를 다르게 복제하는 실패 2개를 확인한 뒤 수정했다. 관련 Windows 헤드리스 테스트 103개와 Core 전체 45개가 통과했다. 이동 가능한 새 객체 ID, 크기·투명 픽셀·반복 복제·다른 문서에 붙여넣기·실행 취소·다시 실행·저장 후 재열기·잘못된 크기 거부를 확인했다. 클립보드 대체 구현도 실제 PNG·전용 메타데이터 DataObject 읽기·쓰기를 거친다.
- 일반 Release 솔루션 빌드는 경고·오류 0개다. 실행 중인 SnapBerry가 없는 상태에서 Portable 단일 EXE를 갱신했으며 번들·난독화·리소스·공개 API·기존 JSON 계약 검증을 통과했다. 일반 개발용 Release 출력을 보존했다. 실제 SnapBerry 실행, 창 표시·활성화, OS 클립보드 및 물리적인 키보드·마우스 조작 테스트는 실행하지 않았다.

## 이력 이미지 복사와 현재 이미지 위 붙여넣기 (2026-10-20)

- 이력 목록에서 이미지 하나를 선택하고 Ctrl+C 하면 주석·자르기·출력 크기를 반영한 합성 이미지를 복사한다. 현재 편집 문서를 교체하지 않으며 복사 전에 자동 저장을 완료한다. 텍스트 입력의 복사는 기존 동작을 유지한다.
- 이미지가 열려 있을 때 Ctrl+V 하면 현재 이미지 중앙에 이미지 객체를 추가한다. 출력 영역보다 크면 비율을 유지해 맞추고 이동·크기 조절·삭제·실행 취소·다시 실행 및 저장 후 재열기를 지원한다. 열린 이미지가 없으면 새 이미지로 연다.
- 기존 스티커 자산 저장 방식을 재사용하며, 개별 자산 16MiB·전체 이미지 256MiB 한도를 변경 전에 검사한다. 저장 시에도 같은 한도를 확인해 다시 열 수 없는 이력으로 기존 파일을 덮어쓰지 않는다. 확대된 문서의 소수 좌표와 얇은 이미지도 렌더링 크기·크기 조절 비율을 유지한다.
- 변경 전 실패한 회귀 테스트를 확인한 뒤 관련 Windows 헤드리스 테스트 87개와 Core 전체 45개가 통과했다. 선택 이력 복사, 현재 문서 보존, 자르기·비균등 크기 변경 좌표, 실제 합성 픽셀, 이동·크기 조절·삭제와 실행 취소, 저장·재열기, 저장 한도 및 소수 크기를 검증했다. 코드 리뷰에서 확인된 저장 한도와 소수 크기 관련 문제도 수정했다.
- 일반 Release 솔루션 빌드는 경고·오류 0개다. 실행 중인 SnapBerry가 없는 상태에서 Portable 단일 EXE를 갱신했고 번들·난독화·내장 자원·공개 API·기존 JSON 계약 검증을 통과했다. 일반 개발용 Release 출력을 보존했다.
- 실제 SnapBerry 실행, 창 표시·활성화, OS 클립보드 및 물리적인 키보드·마우스 조작 테스트는 실행하지 않았다. 클립보드 대체 구현과 표시하지 않은 편집기·메모리 렌더링으로 검증했다.

## 이미지 열기·저장 후 명령 버튼 활성화 복구 (2026-10-20)

- 이미지 열기나 저장 중에 WPF가 Open/Paste 명령을 조회하면, 작업 완료 후에도 리본 버튼이 비활성 상태를 캐시하는 문제를 수정했다. SetBusy에서 명령 재조회를 요청해 다음 사용자 입력 없이도 명령 버튼이 다시 활성화되도록 한다.
- 실제 파일 대화상자와 클립보드를 대체한 헤드리스 회귀 테스트 2개에서 작업 중 명령을 조회하고, 이미지 열기·저장 완료 후 실제 리본 버튼의 IsEnabled를 검사했다. 변경 전 두 경로에서 버튼 비활성 유지로 실패한 뒤 수정 후 통과했다. 가져오기 취소·실패·재진입 등을 포함한 관련 테스트 전체 20개가 통과했다.
- 일반 Release 솔루션 빌드는 경고·오류 0개다. 실행 중인 SnapBerry가 없는 상태에서 Portable 단일 EXE를 갱신했고 번들·난독화·내장 자원·공개 API·기존 JSON 계약 검증을 통과했다. 실제 앱·창 표시·OS 클립보드·화면 캡처 테스트는 실행하지 않았다.

## 이름 없는 이력의 날짜 검색 (2026-10-20)

- 이름 검색의 대상을 저장된 이름에서 목록에 표시되는 제목으로 바꿨다. 이름이 없는 이력은 현지 시각의 `MM.dd HH:mm:ss` 날짜 제목으로 검색되며, `10.20`, `12:34`, 전체 날짜·시간 및 앞뒤 공백이 있는 검색어를 지원한다. 검색창 안내도 ‘이름·날짜 검색’으로 수정했다.
- 날짜 검색 회귀 테스트 5개가 변경 전 빈 결과로 실패한 뒤 수정 후 통과했다. 관련 이름·검색·저장소 헤드리스 테스트 전체 25개가 통과해 기존 이름 검색, 대소문자 무시, 페이지 처리, 이름 변경과 구형 이력 호환성도 확인했다.
- 일반 Release 솔루션 빌드는 경고·오류 0개다. 사용자가 실행 중인 SnapBerry를 트레이 메뉴로 종료한 뒤 프로세스 부재를 확인하고 Portable 단일 EXE를 갱신했다. 번들·난독화·내장 자원·공개 API·기존 JSON 계약 검증을 통과했다. 실제 앱·창 표시 및 화면 캡처 테스트는 실행하지 않았다.

## 스티커 카테고리 한국어 표시 (2026-10-20)

- 스티커 선택 창의 카테고리를 이모지·키보드·체크 / 엑스·기호·방향·업무로 표시한다. 표시 이름과 내부 분류 키를 분리해 기존 카테고리 필터와 선택 스티커 복원, 스티커 ID 및 PNG 생성 동작을 유지한다.
- 기존 스티커 선택·분류·PNG 생성 헤드리스 테스트 9개가 통과했고, 선택 창의 오프스크린 렌더링에서 한국어 표시를 확인했다. 일반 Release 솔루션 빌드는 경고·오류 0개다. 실제 창 표시·활성화·화면 캡처 테스트는 실행하지 않았다.
- 실행 중인 SnapBerry를 감지한 첫 Portable 게시를 중단했고, 사용자가 트레이 메뉴로 종료한 뒤 실행 중인 프로세스가 없는 것을 확인하고 게시했다. 최신 단일 EXE의 번들·난독화·내장 자원·공개 API·기존 JSON 계약 검증을 통과했다.

## 속성 그룹 우측 여백 제거 (2026-10-20)

- 리본 속성 패널의 고정 너비 210 DIP를 최대 너비 제한으로 바꿔, 도구별 컨트롤이 차지하는 만큼만 너비를 사용한다. 기존 두 줄 배치, 리본 높이와 편집 영역 위치, 좁은 창의 그룹 접힘은 유지한다.
- 기존 배치 테스트에 도구·선택 종류별 우측 잔여 공간과 세로 잘림 검증을 추가했다. 변경 전 화살표 도구에서 불필요한 54 DIP 여백으로 실패했고, 변경 후 관련 Windows 헤드리스 테스트 36개가 통과했다. 1240·760·720 DIP 배치와 오프스크린 렌더링을 확인했다.
- 일반 Release 솔루션 빌드는 경고·오류 0개다. Portable 단일 EXE 게시와 번들·난독화·내장 자원·공개 API·기존 JSON 계약 검증을 통과했다. 실제 앱·창·메뉴 표시 및 화면 캡처 테스트는 실행하지 않았다.

## 도구 속성 리본 통합 (2026-10-20)

- 리본 아래에 항상 예약하던 60 DIP 속성 줄을 제거하고 홈 탭의 ‘속성’ 그룹으로 통합했다. 선택할 객체가 없거나 자르기 도구를 사용하면 그룹 전체를 숨긴다. 색상은 현재 색상 표시와 펼침 팔레트로, 선 두께는 현재 값과 펼침 슬라이더로 표시한다. 도구별 나머지 속성도 같은 그룹에서 제공하며 좁은 창에서는 리본의 기본 접힘 메뉴로 접근한다.
- 메뉴 내부 컨트롤은 원래 속성 영역의 활성화 상태를 명시적으로 연결한다. 도구·선택이 바뀌면 색상/두께 메뉴를 닫고 슬라이더 편집 그룹을 끝내며, 기존 선택 객체 편집과 실행 취소 동작을 유지한다.
- 변경 전 테스트에서 리본 아래 60 DIP가 남는 실패와 좁은 창의 속성 그룹이 접히지 않는 실패를 확인했다. 변경 후 Windows 헤드리스 테스트 74개 및 Core 전체 45개가 통과했다. 1240·760·720 DIP 너비에서 도구·선택 변경 후에도 편집 영역 위치가 유지되고, 좁은 창에서는 속성 그룹이 접히는 것을 확인했다. 창의 리소스를 유지한 독립 시각 트리를 오프스크린 렌더링해 기본/좁은 배치도 검토했다.
- 일반 Release 솔루션 빌드는 경고·오류 0개이며 Portable 단일 EXE를 갱신했다. 실제 EXE를 읽는 PortableVerification에서 난독화·내장 자원·공개 API·기존 JSON 계약 검증을 통과했다. 일반 개발용 Release 출력은 보존했다.
- SnapBerry 실행, 실제 창·메뉴 표시, 화면 캡처, 포커스 변경을 사용하는 테스트는 실행하지 않았다. 메뉴의 물리적인 마우스·키보드 조작과 접힌 그룹 안의 중첩 메뉴 조작은 대화형 확인이 남아 있다.

## 속성 바 높이 유지 (2026-10-20)

- 선택할 객체가 없거나 자르기 도구를 사용해 하위 컨트롤이 모두 숨겨져도 속성 바의 배경과 공간을 유지한다. 모든 도구의 바 높이는 60 DIP로 통일했다.
- 속성 컨트롤은 한 줄로 배치하고, 좁은 창에서는 가로 스크롤로 접근한다. 줄바꿈으로 바 높이와 편집 영역 위치가 바뀌지 않으며 기존 속성별 표시·활성화 조건은 유지한다.
- 창 없이 WPF 배치를 측정하는 회귀 테스트 3개를 추가했다. 수정 전에는 창 너비와 도구에 따라 바 높이가 13·45·77 DIP로 달라지는 실패를 확인했고, 수정 후 1240·760·720 DIP 너비에서 모든 도구·선택 종류의 바 높이와 편집 영역 위치가 같고 컨트롤이 세로로 잘리지 않는 것을 확인했다. 바만 오프스크린 렌더링한 이미지도 검토했다.
- 관련 Windows 헤드리스 테스트 102개와 Core 전체 45개 통과. 일반 Release 빌드는 경고·오류 0개다. 실제 SnapBerry 실행·창 표시·화면 캡처·물리적인 도구 전환 조작은 실행하지 않았다.
- Portable 게시와 EXE 번들의 난독화·리소스·공개 API·JSON 계약 검증 통과. 난독화 DLL 복사본에서도 속성 바 배치와 기존 선택 속성 테스트 11개가 통과했다.

## 기존 이미지 열기·클립보드 이미지 붙여넣기 (2026-10-20)

- 홈 리본에 이미지 열기(Ctrl+O)와 이미지 붙여넣기(Ctrl+V)를 추가했다. 문서가 없는 상태에서도 사용할 수 있으며 작업 중에는 비활성화한다. 이미지 붙여넣기 명령은 텍스트 입력 컨트롤에서 처리하지 않아 일반 텍스트 붙여넣기를 유지한다.
- PNG·JPEG·BMP·GIF·TIFF를 읽고, GIF·TIFF는 첫 프레임을 사용한다. 파일 크기와 디코더의 이미지 크기를 검사한 후 독립된 96 DPI BGRA 픽셀과 PNG로 보관한다. 사용자 원본 파일은 읽기 전용으로 열고 해제한다.
- 클립보드는 PNG 형식을 우선해 알파를 보존하고 일반 Bitmap 형식도 지원한다. 일시적인 클립보드 점유 오류는 최대 세 번 시도한다. 테스트는 실제 시스템 클립보드 대신 메모리 DataObject와 입력 경계 대체 구현을 사용한다.
- 읽기·검증, 기존 문서 저장, 새 이력 저장이 성공한 뒤 편집 문서를 교체한다. 취소·빈 클립보드·손상 파일·이력 저장 실패 시 현재 문서를 유지한다. 가져온 이미지는 새 이력 ID와 ImageFile/Clipboard 출처로 보관하며 기존 캡처 모드의 저장 숫자는 유지한다.
- 기능 부재로 최초 테스트가 실패하는 것을 확인한 뒤 새 테스트 18개가 통과했다. 관련 Windows 헤드리스 테스트 총 99개와 Core 전체 45개 통과. 일반 Release 빌드는 경고·오류 0개다.
- Portable 게시와 난독화 DLL 복사본의 새 테스트 18개가 통과했다. EXE 검증 도구는 Cecil의 리소스 정렬 여백 차이로 발생한 오탐을 수정하여 리소스 이름·개수·실제 데이터를 정확히 비교하고 CLR 리소스 영역의 범위도 검사한다. 실제 EXE의 공개 API·난독화·JSON 계약 검증을 통과했으며, 임시 참조 DLL의 리소스 한 바이트를 바꾸면 실패하는 것도 확인했다.
- SnapBerry 실행, 실제 파일 대화상자·OS 클립보드·키 입력 및 창 표시 테스트는 실행하지 않았다. 텍스트 입력 제외 조건과 명령 연결은 헤드리스로 검증했으며 실제 네이티브 붙여넣기 조작은 대화형 확인 대상이다.

## 편집·이력 일관성 수정 (2026-10-19)

- 일반 텍스트의 입력·모델·이력 저장 한도를 8192자로 통일했다. 한도를 넘는 입력은 편집창에서 안내하고 적용을 막으며, 모델도 문서 변경 전에 거부한다. 키보드 스티커는 기존 64자 한도를 편집창에서도 안내한다.
- 새 캡처가 열리면 해당 이력 항목을 선택한다. 겹치는 목록 갱신은 완료를 기다려 마지막 상태를 반영하며, 갱신 중 바뀐 선택과 Ctrl/Shift 다중 선택도 보존한다.
- 두께·모자이크 슬라이더의 한 번 드래그를 한 번의 실행 취소로 묶었다. 원래 값으로 돌아온 드래그는 상태 토큰과 실행 취소 이력을 유지한다. 중간 상태의 자동 저장과 최종 저장·실행 취소·다시 실행도 검증했다.
- 이력 썸네일은 최대 120px의 독립된 동결 비트맵으로 만들어 고해상도 합성 이미지를 붙잡지 않는다. 목록 메타데이터는 파일 크기·수정 시각으로 캐시하고 저장·외부 변경·삭제 때 갱신한다. 목록 새로고침은 파일 목록을 확인하되 변경 없는 ZIP을 다시 열지 않는다.
- 새 회귀 테스트에서 수정 전 실패를 확인한 뒤 수정 후 통과했다. Core 45개와 관련 Windows 헤드리스 테스트 78개가 통과했고, 일반 Release 빌드는 경고·오류 0개다. 입력창 내용의 오프스크린 렌더링으로 배치도 확인했다.
- Portable 게시와 실제 EXE 번들의 난독화·공개 API·기존 JSON 계약 검증이 통과했다. 게시된 난독화 DLL 복사본에서도 새 회귀 테스트 9개가 통과했다. 저장소 비공개 필드로 읽기 지연을 만드는 테스트 하나는 이름이 바뀌므로 일반 Release에서만 실행했다.
- SnapBerry 실행, 창 표시·활성화, 실제 마우스 드래그·화면 캡처 및 전체 Windows UI 테스트는 실행하지 않았다.

## Image dimensions and resize (2026-10-19)

- 상태 표시줄에 현재 이미지의 출력 크기를 `가로 × 세로 px`로 표시하며 클릭하면 비율 유지 옵션(기본 켜짐)이 있는 크기 변경창을 연다. 이미지 없음/작업 중에는 비활성화된다.
- 원본 좌표와 PNG는 보존하고 선택적인 출력 크기만 이력 상태와 JSON에 추가한다. 합성 후 자르기·리샘플링을 적용하므로 이미지와 모든 주석이 함께 확대/축소된다. 캔버스 크기, 포인터 역변환, 맞춤 확대율, PNG/클립보드, 자동 저장 및 이력 목록도 출력 크기를 사용한다.
- 회귀 테스트를 먼저 실패시킨 뒤 Core 45개, 관련 Windows 헤드리스 테스트 20개 통과. 크기 변경/자르기/실행 취소/다시 실행, 상태 토큰, 비율 계산, 입력 제한, PNG 실제 픽셀 크기와 주석 위치, 원본 보존, 저장 후 재열기, 비균등 크기 변경 후 좌표 변환, 잘못된 메타데이터 거부, 상태 표시줄 갱신을 확인했다. 비균등 배율의 주석/조절점/자르기 모서리 선택과 제스처 임계값은 축별 화면 거리로 검증하며, 원래 크기로 복원할 때 출력 크기 재정의가 해제되는 것도 확인했다.
- 크기 입력창은 표시하지 않고 입력 이벤트와 레이아웃을 검증했으며, 내용만 오프스크린 렌더링한 PNG를 확인했다. SnapBerry 실행, 실제 창 표시·활성화·화면 캡처를 사용하는 테스트는 실행하지 않았다.
- 최종 일반 Release 빌드와 Portable 게시, 실제 EXE의 난독화/JSON 계약 검증 통과. 난독화 DLL 복사본에서도 Core 45개와 새 기능의 공개 API·입력창·상태 표시줄 테스트 7개가 통과했다.

## Capture cancellation (2026-10-19)

- 영역/창 선택을 ESC로 취소해 선택 결과가 없으면 편집기를 숨긴 상태로 캡처만 종료한다. 성공한 캡처는 편집기를 표시한 뒤 열고, 캡처 또는 문서 열기 오류는 편집기를 복구하며 모든 경로에서 busy 상태를 해제한다.
- 앱의 실제 완료 흐름을 호출하는 헤드리스 회귀 테스트 4개를 추가했다. 기존 구현에서 취소 후 편집기가 표시되는 실패를 먼저 확인했고 수정 후 통과했다. 선택 대기 중 busy 유지, 취소 시 문서 미생성, 성공 시 표시 후 문서 열기, 선택/열기 오류 시 복구를 확인한다.
- 관련 Windows 헤드리스 테스트 10개와 Core 전체 36개 통과. 실제 ESC 입력, 화면 캡처 및 창 표시를 사용하는 대화형 테스트는 실행하지 않았다.

## Portable obfuscation (2026-10-19)

- Portable 게시에만 Obfuscar 2.2.50을 적용한다. 일반 Release 출력은 수정하지 않고 `obj`의 복사본을 난독화해 단일 EXE에 넣는다. 실패 시 게시를 중단하며 SnapBerry 실행 중에도 게시를 거부한다.
- `dotnet run --project tools/PortableVerification -- .`은 실제 배포 EXE에서 앱과 Core DLL을 읽어 일반 Release와 비교한다. 비공개 이름 변경(앱 144개, Core 16개), 공개 타입/메서드 이름, 네이티브 진입점, BAML을 포함한 내장 자원 보존과 디버그 정보 제거를 확인했다. 번들 내부 Core 메서드 실행, 기존 주석·캡처·설정 JSON 역직렬화/직렬화도 통과했다. 변경 전 EXE에서는 난독화 미적용으로 검증이 실패하는 것을 먼저 확인했다.
- 일반 Release: Core 36개와 UI를 사용하지 않는 설정/경로 테스트 3개 통과. 난독화 DLL 복사본: Core 36개와 설정 저장/복구 테스트 2개 통과.
- 난독화 DLL로 내부 경로 타입을 직접 참조하는 `AppDataPathsTests.CapturesSettingsAndLogsUseSnapBerryDirectory`를 실행하면 TypeLoadException이 발생한다. 내부 타입 이름이 변경되므로 원래 이름으로 컴파일된 테스트에는 연결할 수 없다. 이 테스트는 일반 Release에서 통과하며, 난독화 후 회귀 검증에는 공개 API 테스트를 사용한다.
- 앱 실행, 화면 표시, 캡처, 단축키 등록 및 전체 Windows UI 테스트는 실행하지 않았다. WPF 런타임 동작은 별도 승인 후 대화형 확인이 필요하다.

Date: 2026-10-14, Asia/Seoul. SDK: 10.0.401. Host: Windows NT 10.0.26300.0, 8 logical processors.

## Automated Results

- Locked dependency restore succeeded.
- Release solution build: 0 warnings, 0 errors.
- Core tests: 16 passed, 0 failed.
- Windows tests: 37 passed, 0 failed. Total: 53 passed.
- Tests exercise state tokens, sequence generation and undo, clipping, source-image preservation, mosaic overlap, sticker creation, archive persistence and deletion, autosave serialization, hotkey registration failure, PNG overwrite protection, editor reopen and actual desktop capture.
- Seven independent review findings were corrected. Regression tests cover undo-before-autosave, transactional document open, modal export reentry, selected-style preservation, updated text bounds, exporting when library storage fails, and cancelling a gesture on document change.
- The self-contained published EXE was started in hidden tray mode, reached input-idle and remained running. The test-owned process was then stopped. This is startup verification, not a manual walkthrough of every interactive command.
- The final ZIP was extracted to a path containing a space and its EXE reached input-idle in tray mode. The test-owned process was stopped afterward.
- Desktop and compact editor screenshots were generated from a synthetic project-checklist image and visually inspected. These are app-window renders, not recordings of private desktop content.

## Performance Measurements

For 1920×1080 input, selected-image PNG encoding, editor-model load and first composite took 68.5–168.0ms across 10 runs. This measurement excludes interactive region selection and disk persistence; it is not a complete hotkey-to-saved-record measurement.

For 3840×2160 input, 50 render iterations took 8.75 seconds total. Measured private-memory samples after forced collection were 216.0, 216.9, 168.3, 168.3 and 168.3 MB (decimal). Final private-memory delta was -55.5 MB and GDI-handle delta was -4. This checks repeated synthetic 4K composition, not 50 physical 4K screen captures.

## Native Checks

The current host's desktop capture returned a nonempty bitmap with dimensions matching virtual-desktop bounds. A real WPF editor window rendered and its annotations and next number were restored from the archive. Modal and storage-failure regressions run with a WPF Dispatcher synchronization context.

## Remaining Environment Checks

The reported intermittent black startup window was not consistently reproduced after rebuilding the stale normal Release output. Startup selects SoftwareOnly for WPF display compatibility instead of relying on the default hardware pipeline. This mitigates driver-dependent rendering failures; the exact original cause is not established. The normal Release path is refreshed along with the portable distribution.

- Windows 10 22H2 and additional Windows 11 versions.
- Physical 100/125/150/200% DPI combinations, negative-coordinate monitors, cross-monitor dragging and display disconnect while selecting.
- Native manual IME composition, clipboard contention and paste behavior in external applications.
- Protected-content capture and capture interactions with other applications' overlays.
- Physical 4K capture 50-cycle resource check and complete selection-to-editor latency.

These limits are environment coverage gaps. The app does not claim to recover protected or occluded window content.

## Interaction Update

History items now activate on a single plain click. Ctrl/Shift clicking keeps multi-selection for deletion. Arrow and rectangle tools support a first-click anchor followed by a second-click endpoint, live preview, Escape/tool-switch cancellation and the existing drag gesture. The standard WPF Ribbon supplies Home drawing/edit/export groups and a View tab. New tests cover two-click placement, retained dragging, cancellation and the single-click history event.

## Implementation Decisions

1. The new project uses its own feature branch in the requested directory rather than a linked worktree. No preexisting source changes were present.
2. Immutable annotation snapshots implement undo rather than separate command classes. Image buffers are not copied into history.
3. Sticker PNGs are generated from project drawing definitions and installed Windows symbols, then retained inside each capture; first rendering of some symbols depends on Windows font support.
4. Text and keycap edits use an owned WPF input dialog rather than an inline overlay.
5. Every committed document state is queued for persistence, including undo to a clean checkpoint. Opening a record may cause a redundant safe rewrite.
6. External export can proceed despite internal persistence failure; switching documents and exiting still require successful history storage.
7. Manual patches were staged in the chat's writable work directory and copied to the requested D: project; all builds and Git commits ran in that project. The synchronized source was checked after each batch.
8. Windows test commands and a persistent progress ledger replaced Unix-only skill task scripts. This changes task bookkeeping, not the shipped app.

No deferred minor review findings were raised. Native interaction limits remain as listed above.
