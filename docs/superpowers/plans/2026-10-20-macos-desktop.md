# macOS 데스크톱 구현 계획

> **For agentic workers:** Use superpowers:executing-plans to implement this plan in this session. The user authorized proceeding; routine implementation choices are resolved within the approved scope.

**Goal:** Windows를 유지하며 맥용 캡처·주석 편집 앱과 두 CPU용 번들을 추가한다.

**Architecture:** Core와 저장 형식을 공유하고 Avalonia UI·Skia 렌더러·macOS 캡처 서비스를 분리한다. 첫 캡처는 macOS 기본 screencapture 도구를 사용한다.

**Tech Stack:** .NET 10, Avalonia 12.1.4, SkiaSharp, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-20-macos-desktop-design.md`

## Global Constraints

- 기존 Windows Release와 버전 1.0.1을 유지하고 Portable EXE를 갱신한다.
- 실제 창·앱 실행·포커스·OS 캡처는 테스트하지 않는다.
- 기존 `.qcapture` JSON과 자원 해시·용량 제한을 유지한다.
- macOS 실기기·서명 검증은 미실행으로 명시한다.

## Review Focus

- 캡처 취소·실패 시 기존 문서와 창 상태 보존.
- 파일 저장 실패 시 문서 전환·종료로 편집이 사라지지 않음.
- 자르기·배율·반대 방향 화살표·드래그 취소에서 좌표 보존.
- 깨진 이미지·거대한 크기·누락된 스티커를 열기 전에 검증.
- Windows 빌드·기존 문서·일반 Release 출력 보존.

### Task 1: 공통 문서·렌더링·이력

**Files:** Core/ImageDimensions.cs, App/Library/CaptureLibraryStore.cs, Desktop/Editing/*, Desktop/Rendering/*, Desktop.Tests/EditorTests.cs.

**Interfaces:** `DesktopEditor.Open(CaptureRecord)`, `Snapshot()`, `InsertImage(byte[])`, `Commit(Annotation)`, `RenderPng()`, `SaveAsync(CaptureLibraryStore)`; 렌더러 입력은 PNG·문서·이미지 자원이다.

- [x] 문서 열기 실패의 원자성, Undo/Redo·붙여넣기·자르기·PNG·저장 왕복 테스트를 작성하고 실패 확인.
- [x] Core 동일 크기 검사, 저장소 소스 링크, Skia 렌더러와 편집 상태 구현.
- [x] 테스트 통과 확인.

### Task 2: macOS 캡처와 Avalonia 편집기

**Files:** Desktop/Capture/*, Desktop/Views/*, Desktop/App.cs, Desktop/Program.cs, Desktop.Tests/CaptureTests.cs, Desktop.Tests/ViewTests.cs.

**Interfaces:** `ICaptureService.CaptureAsync(DesktopCaptureMode,CancellationToken)`는 PNG 또는 취소 시 null을 반환한다. MainWindow는 파일·캡처·저장·이력·테마를 연결한다.

- [x] 캡처 인수·취소·오류·임시 파일 정리와 비활성 명령 상태의 실패 테스트 작성.
- [x] 캡처 프로세스 경계와 창 복원, 편집기·이력·저장·테마 구현.
- [x] 캡처 대체 구현과 헤드리스 렌더링 테스트 통과 확인.

### Task 3: 두 CPU용 패키지와 Windows 검증

**Files:** build/Publish-MacOS.ps1, build/Finalize-MacOS.sh, Desktop/Packaging/Info.plist, README.md, docs/verification.md, solution.

- [x] osx-arm64·osx-x64 자체 포함 앱을 별도 출력으로 게시하고 번들 구조 검사.
- [x] 맥에서 실행 비트·서명·배포 압축을 마무리하는 스크립트와 안내 추가.
- [x] Desktop 전체·Core 전체·관련 Windows 헤드리스 테스트, 일반 Release, Portable 게시·번들 검사.
- [x] 독립 리뷰, 필요한 수정과 재검증, 한국어 커밋. 푸시하지 않음.

## 실행 기록

- 기준: `34e2aef`, Core 45/45 통과, 사용자 소유 ZIP은 변경하지 않는다.
- 결정: 별도 프로젝트로 기존 Windows UI를 유지하므로 현재 체크아웃에서 작업한다. 새 worktree에 대한 확인으로 작업을 중단하지 않는다.
- 결정: 첫 macOS 배포는 기본 캡처 도구 연결이며 ScreenCaptureKit 직접 브리지는 후속 교체 지점이다. 맥 권한·선택 UI는 실기기 확인이 필요하다.
- 검증: Desktop 25/25, Windows 관련 92/92, Core 45/45, 일반 Release 경고·오류 0개.
- 리뷰 수정: 긴 텍스트 입력 제한·포인터 오류 전달과 선택 속성 동기화·무변경 보호를 회귀 테스트로 검증했다.
- 패키지 검사: 두 ZIP 각각 224개 파일, CPU·실행 권한·버전·아이콘 확인. Windows Portable 번들 검사 통과. 맥의 실제 실행·서명·권한은 미검증이다.
