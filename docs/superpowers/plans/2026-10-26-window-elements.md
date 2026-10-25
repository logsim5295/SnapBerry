# 창 내부 요소 캡처 구현 계획

**Goal:** 창 캡처에서 내부 요소를 자동 선택하고 휠로 부모 영역을 선택한다.
**Architecture:** 창 핸들 및 경계 스냅샷, 순수 선택 상태, 네이티브/UIA 탐색, 오버레이 연결을 분리한다.
**Tech Stack:** C# / WPF / Win32 / System.Windows.Automation / xUnit.
**Spec:** ../specs/2026-10-26-window-elements-design.md

## 제약 및 검토 초점

- UI를 표시하거나 실제 화면을 캡처하는 테스트는 실행하지 않는다.
- 음수 모니터 좌표, 부분 노출된 요소, 겹친 창의 앞뒤 순서를 보존한다.
- 늦은 탐색 결과, 탐색 실패/지연, 종료 후 응답을 안전하게 처리한다.
- 기존 영역 캡처, 전체 화면 캡처, Release 및 Portable 빌드를 유지한다.

## 작업

- [x] `WindowCaptureSelectionTests.cs`: 작은 요소/부모 선택, 경계 보정, 오래된 결과, 초기 클릭을 헤드리스 검증한다. 실패 확인 후 `WindowCaptureSelection.cs`를 구현한다.
- [x] `WindowElementProbeTests.cs`: 합성 요소 트리로 버튼 내부 텍스트 제외, 앞쪽 형제 우선, 탐색 제한과 실패를 검증한다. 실패 확인 후 `WindowElementProbe.cs` 및 Win32 어댑터를 구현한다.
- [x] `CaptureCoordinator.cs`: 최초 커서 위치, 이동, 휠, 클릭, 취소와 탐색 타이머를 연결한다. 창 핸들을 보존하도록 `NativeMethods.cs`를 수정한다.
- [x] 관련 헤드리스 Windows 테스트와 Core/Desktop 테스트를 실행한다. 일반 Release 빌드와 지정 Portable 게시를 실행하고 결과를 기록한다.

리뷰에서 지적된 지연 조회 재시도 및 조회 전 휠 조작을 회귀 테스트로 보완했다. Windows 관련 37개, Core 54개, Desktop 헤드리스 154개가 통과했다. 일반 Release 빌드와 Portable 게시 및 번들 검증도 통과했다. 상세 범위는 `docs/verification.md`에 기록했다.

후속 사용자 보고에 따라 실제 창 메타데이터와 Thunderbird 접근성 경로를 진단했다. DWM cloaked 창 제외, 원본 HWND의 MSAA hit-test, 느린 조회 결과 보존 및 세션 격리를 추가했다. 후속 검증은 Windows 56개, Core 54개, Desktop Headless 154개 통과이며 실제 화면 캡처는 실행하지 않았다.
