# QuickCapture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Windows에서 화면을 캡처하고 주석을 편집·출력하며, 캡처 이력을 자동 보관해 다시 열어 편집하거나 삭제한다.

**Architecture:** C#/WPF 데스크톱 앱과 순수 편집 모델을 분리한다. Win32 캡처와 전역 단축키는 서비스로 감싸고, 편집 표시와 출력은 동일한 합성 규칙을 공유한다. GUID별 ZIP 컨테이너에 원본 PNG와 주석 JSON을 보관하고, 이력 목록과 자동 저장을 연결한다.

**Tech Stack:** .NET 10 LTS, C#, WPF, Win32 P/Invoke, System.Text.Json, xUnit. SDK는 설치된 10.0.401을 global.json에서 고정하고 rollForward는 latestPatch로 제한한다. 앱은 net10.0-windows, 코어와 코어 테스트는 net10.0으로 구성한다. NuGet 버전은 구현 시작 시 복원 가능한 안정 버전을 확인해 정확한 버전과 packages.lock.json으로 고정한다.

**Spec:** ../specs/2026-10-14-quickcapture-design.md
원본 설계서: https://chatgpt.com/space/page_0104e12d50f881918f048744eed2e834

## Global Constraints

- 프로젝트 루트: D:\01. Dev\_Code\SnapBerry. QuickCapture는 개발용 이름이다.
- 지원 목표는 Windows 10 22H2 및 Windows 11의 x64 환경이다.
- 영역, 창, 전체 화면 캡처; 화살표, 텍스트, 번호, 스티커, 사각형, 모자이크; 선택·이동·삭제·크기 조정; 실행 취소·다시 실행; 클립보드 복사·PNG 저장을 구현한다.
- 전체 화면은 가상 데스크톱의 모든 모니터이며 빈 공간은 불투명 검정이다. 창 캡처는 현재 보이는 창 영역이다.
- 원본 이미지와 주석의 기준 좌표는 캡처 이미지의 실제 픽셀이다.
- 앱은 Per Monitor V2 DPI 인식을 사용하도록 구성한다.
- 여러 캡처를 이력으로 보관하되 동시에 편집하는 문서와 주석 선택은 각각 한 개다.
- 성공한 캡처는 자동 보관하고 앱 재시작 후 원본과 마지막 주석 상태로 재편집한다. 선택한 이력은 확인 후 삭제하며 외부 PNG와 클립보드는 유지한다.
- 이력 경로는 LocalAppData/QuickCapture/Library이다. 사용자 삭제 전까지 보관하고 자동 삭제하지 않는다.
- 초기 단축키는 영역 Ctrl+Shift+1, 창 Ctrl+Shift+2, 전체 화면 Ctrl+Shift+3이다.
- 화면 이미지, 텍스트 주석, 클립보드 내용은 진단 로그에 기록하지 않는다.
- 스크롤 캡처, 녹화, OCR, 클라우드, 외부 프로젝트 가져오기·내보내기, 자동 업데이트는 첫 버전에 포함하지 않는다.
- .NET 런타임을 포함한 win-x64 폴더 배포 ZIP을 제공한다.
- 각 작업의 검증 후 관련 파일만 커밋한다. 기존 저장소를 발견하면 기존 변경을 보존한다. 새 빈 프로젝트면 최초 구현 단계에서 독립 Git 저장소를 초기화한다.
- 단계별 완료 상태와 실제 테스트 결과를 docs/verification.md에 기록한다. 계획에 적힌 기대 결과는 검증 결과가 아니다.

.NET 10의 LTS 선택 근거: [Microsoft .NET 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet).
Windows 10에서의 앱 호환 검증과 운영체제 자체의 지원 상태는 별개로 취급한다.

## Review Focus

1. 마우스 드래그 중 모니터 구성이 바뀌면 선택을 취소하고 기존 문서를 보존한다. Task 3의 DisplayChangeCancelsSelection.
2. 저장 또는 복사 중 새 캡처 요청이 오면 명령을 차단해 출력과 문서가 섞이지 않는다. Task 9의 BusyExportRejectsNewCapture.
3. 출력과 영속 저장의 완료 상태를 분리하고 실행 취소 후 자동 저장할 상태를 정확히 판단한다. Task 2의 ExportDoesNotMarkPersisted와 UndoToPersistedCheckpointIsClean.
4. 모자이크가 서로 겹치면 더 큰 블록 크기를 우선하며 일반 주석은 위에 그린다. Task 5의 OverlappingMosaicUsesLargerBlocks.
5. 클립보드 잠금이나 저장 실패 시 편집 문서와 이전 파일이 유지된다. Task 6의 ClipboardFailurePreservesDocument 및 FailedReplacePreservesExistingFile.

## 파일 구조

아래 경로는 프로젝트 루트 기준이며 아직 생성할 제품 파일이다.

| 경로 | 역할 |
| --- | --- |
| SnapBerry.sln, global.json, Directory.Build.props, .gitignore | 솔루션과 공통 빌드 설정 |
| src/SnapBerry.Core/Geometry/ | PixelPoint, PixelRect, ViewTransform |
| src/SnapBerry.Core/Documents/ | AnnotationDocument, Annotation, AnnotationStyle, EditorSession |
| src/SnapBerry.Core/History/ | IEditCommand, CommandHistory, 주석 변경 명령 |
| src/SnapBerry.App/App.xaml, App.xaml.cs, app.manifest | WPF 진입점과 DPI 선언 |
| src/SnapBerry.App/Shell/ | AppHost, CapturePanel, TrayService, SingleInstanceService |
| src/SnapBerry.App/Capture/ | CaptureService, CaptureFrame, CaptureOverlay, CaptureCoordinator |
| src/SnapBerry.App/Interop/ | NativeMethods, GdiBitmapHandle, GdiDcHandle |
| src/SnapBerry.App/Editor/ | EditorWindow, EditorViewModel, EditorCanvas, TextAnnotationEditor |
| src/SnapBerry.App/Stickers/ | StickerCatalog, StickerAssetStore, KeycapRenderer, StickerPicker |
| src/SnapBerry.App/Assets/Stickers/ | 48개 투명 PNG와 catalog.json |
| THIRD-PARTY-NOTICES.md | 사용 자산의 출처와 재배포 라이선스 |
| src/SnapBerry.App/Rendering/ | CompositeRenderer, MosaicRenderer, ImageSurface |
| src/SnapBerry.App/Export/ | ExportService, PngFileWriter, ClipboardWriter |
| src/SnapBerry.Core/Library/ | CaptureRecord, CaptureMetadata, CaptureLibraryPage |
| src/SnapBerry.App/Library/ | CaptureLibraryStore, CaptureRecordSerializer, ThumbnailCache, AutosaveCoordinator, CaptureLibraryViewModel, CaptureLibraryView |
| src/SnapBerry.App/Settings/ | AppSettings, SettingsService, HotkeyService, SettingsWindow, StartupService |
| tests/SnapBerry.Core.Tests/ | 순수 로직 테스트 |
| tests/SnapBerry.Windows.Tests/ | STA 렌더링·네이티브 경계와 주입 가능한 어댑터 테스트 |
| docs/verification.md, README.md | 실제 검증 기록과 실행·배포 안내 |

WPF View는 .xaml과 .xaml.cs 쌍으로 만든다. 전체 편집 기능을 하나의 코드비하인드 파일에 넣지 않는다.

## 인터페이스 규약

Core는 WPF와 Win32에 의존하지 않는다. PixelPoint(double X, double Y), PixelRect(int X, int Y, int Width, int Height)를 정의한다. 캡처 영역은 정수 물리 픽셀이며 주석은 double 좌표를 사용한다. ViewTransform은 줌과 표시 원점으로 이미지 좌표와 DIP 사이를 변환한다.

Annotation은 ID, 종류, 시작점, 끝점, 스타일, Text, MosaicBlockSize를 가진 불변 레코드다. 종류는 Arrow, Text, StepMarker, Sticker, Rectangle, Mosaic다. Sticker는 StickerId, AssetHash, 표시 사각형과 키캡 Text를 보관한다. StepMarker의 표시값은 Text에 저장한다. AnnotationDocument는 StepSequenceState(Mode, NextNumeric, NextAlphabetic)를 추가로 보관한다. Mode는 Numeric 또는 Alphabetic이며 두 카운터는 양의 long, 초기값은 각각 1이다. StepLabelFormatter.Format(long value, StepSequenceMode mode) -> string은 1→1/a, 26→26/z, 27→27/aa로 변환한다. 상한 도달 후 추가 삽입은 안내와 함께 차단하고 다음 값을 낮춰 재개할 수 있게 한다. AnnotationDocument는 폭·높이, 주석 목록과 StepSequenceState를 보관한다. 스티커 PNG는 App의 문서별 자산 사전이 소유하며 Core에는 해시 참조만 둔다. 원본 BGRA32 버퍼는 App의 ImageSurface가 소유한다.

IEditCommand.Apply(AnnotationDocument document), Undo(AnnotationDocument document).
CommandHistory.Execute(IEditCommand command), Undo(), Redo(), CurrentStateToken, MarkExported(Guid stateToken), MarkPersisted(Guid stateToken), IsDirty, IsPersistenceDirty.
각 이력 상태에 고유한 상태 토큰을 두어 편집 분기·출력·영속 저장 시점을 구분한다. IsDirty는 현재 상태와 마지막 출력 상태, IsPersistenceDirty는 현재 상태와 마지막 영속 저장 상태를 비교한다. 비동기 저장 완료는 저장 요청 당시 토큰만 기록하므로 이후 편집 상태를 저장 완료로 오인하지 않는다. 재개한 문서는 저장 상태를 현재 상태로 초기화하고 실행 취소 스택은 비운다.

CaptureFrame(ImageSurface Image, PixelRect DesktopBounds)는 원본 픽셀 버퍼와 가상 데스크톱 원점을 가진다.
ICaptureService.CaptureDesktop() -> CaptureFrame.
IWindowLocator.FindAt(PixelPoint desktopPoint) -> PixelRect?.
ICompositeRenderer.Render(ImageSurface original, IReadOnlyList<Annotation> annotations, IStickerAssetSource stickerAssets) -> ImageSurface. IStickerAssetSource.GetPng(string assetHash) -> ReadOnlyMemory<byte>는 App의 자산 사전이 구현한다. 없는 자산은 오류를 발생시켜 불완전한 이미지 출력과 저장을 막는다.
IExportService.CopyAsync(ImageSurface image, CancellationToken token) -> Task.
IExportService.SavePngAsync(ImageSurface image, string path, bool overwrite, CancellationToken token) -> Task.
작업 버퍼 소유권은 생성자에게 있고 사용 후 Dispose한다. 서비스는 입력 버퍼를 파괴하지 않는다.

## Task 1: 앱 실행과 설정 기반

**Files:** Create 솔루션·공통 빌드 파일, Core/App/두 테스트 프로젝트의 .csproj, App.xaml(.cs), app.manifest, Shell/AppHost.cs, CapturePanel.xaml(.cs), TrayService.cs, SingleInstanceService.cs, Settings/AppSettings.cs, SettingsService.cs, tests/SnapBerry.Windows.Tests/SettingsServiceTests.cs.

**Interfaces:** SettingsService.Load() -> AppSettings, Save(AppSettings settings) -> void. AppSettings에 SchemaVersion=1, 세 가지 단축키, LastSaveDirectory, StartWithWindows=false, 기본 도구 속성을 정의한다. AppHost는 이후 단계에서 캡처·편집 서비스를 연결할 소유자다.

- [ ] 설정 기본값과 손상 복구 테스트를 먼저 작성한다. CorruptSettingsAreBackedUp은 잘못된 JSON을 주입하고 백업 파일 존재, SchemaVersion=1, StartWithWindows=false를 검증한다.
- [ ] dotnet test tests/SnapBerry.Windows.Tests --filter SettingsServiceTests 실행: 구현 전 컴파일 또는 테스트 실패를 확인한다.
- [ ] global.json과 프로젝트를 구성하고 SettingsService를 구현한다. 설정 위치는 LocalAppData/QuickCapture/settings.json, 손상 백업은 덮어쓰지 않는 이름을 사용한다.
- [ ] 트레이·단일 인스턴스·Per Monitor V2 매니페스트를 구현한다. 트레이 UI에는 Windows Forms NotifyIcon만 사용하고 캡처·편집 화면은 WPF로 유지한다.
- [ ] 위 테스트 PASS와 dotnet build SnapBerry.sln -c Debug 성공을 확인한다. 앱을 두 번 실행해 창 하나만 활성화되는지, 패널 닫기 후 트레이 복원과 종료가 되는지 확인한다.
- [ ] 관련 파일을 커밋한다: chore: establish Windows application shell.

## Task 2: 주석 문서와 명령 이력

**Files:** Create Geometry/PixelPoint.cs, PixelRect.cs, ViewTransform.cs; Documents/Annotation.cs, AnnotationStyle.cs, AnnotationDocument.cs, EditorSession.cs; History/IEditCommand.cs, CommandHistory.cs, AddAnnotationCommand.cs, RemoveAnnotationCommand.cs, ReplaceAnnotationCommand.cs, InsertStepMarkerCommand.cs, SetStepSequenceCommand.cs; Documents/StepSequenceState.cs, StepLabelFormatter.cs; tests/SnapBerry.Core.Tests/StepSequenceTests.cs; tests/SnapBerry.Core.Tests/CommandHistoryTests.cs, ViewTransformTests.cs.

**Interfaces:** 위 인터페이스 규약을 구현한다. EditorSession은 현재 AnnotationDocument와 CommandHistory, 선택 ID를 소유하고 새로운 문서로 교체할 때 선택·실행 취소 이력을 초기화한다. 영속 캡처 ID는 Core의 CaptureMetadata로 관리하고 현재 문서 전환과 캡처 목록을 구분한다.

- [ ] UndoRedoRestoresAnnotation, NewEditClearsRedo, UndoToExportCheckpointIsClean, UndoToPersistedCheckpointIsClean, ExportDoesNotMarkPersisted, NewBranchAtSameDepthIsDirty 테스트를 작성한다. 마지막 테스트는 이력 깊이가 같아도 다른 편집 분기이면 IsDirty=true를 검증한다.
- [ ] StepSequenceTests에 NumericSequenceStartsAtOne, AlphabeticSequenceContinuesAfterZ, SwitchingModesKeepsIndependentCounters, ManualLabelEditDoesNotChangeNextValue, UndoInsertionRestoresCounter, DeleteDoesNotRenumberOtherMarkers를 추가한다. 숫자 [1,2,3], 영문 [a,b,c,z,aa,ab], 모드 전환과 Undo/Redo 후 다음 값을 검증한다.
- [ ] ViewTransformRoundTrip은 zoom=0.5, 1, 2에서 변환 후 원좌표 오차가 0.000001 미만임을 검증한다.
- [ ] dotnet test tests/SnapBerry.Core.Tests 실행: 구현 전 실패를 확인한다.
- [ ] 불변 주석과 변경 명령을 구현하고, 한 드래그는 ReplaceAnnotationCommand 한 개로 확정한다. 실행 중 프리뷰는 이력에 추가하지 않는다.
- [ ] 위 테스트 PASS를 확인하고 커밋한다: feat: add annotation document and edit history.

## Task 3: 영역·창·전체 화면 캡처

**Files:** Create src/SnapBerry.Core/Library/CaptureMode.cs; Interop/NativeMethods.cs, GdiBitmapHandle.cs, GdiDcHandle.cs; Capture/CaptureFrame.cs, CaptureService.cs, WindowLocator.cs, CaptureOverlay.xaml(.cs), CaptureCoordinator.cs; Rendering/ImageSurface.cs; tests/SnapBerry.Core.Tests/CaptureCoordinateTests.cs; tests/SnapBerry.Windows.Tests/CaptureCoordinatorTests.cs.

**Interfaces:** ICaptureService와 IWindowLocator를 구현한다. CaptureCoordinator.BeginAsync(CaptureMode mode, CancellationToken token) -> Task<CaptureFrame?>; CaptureMode는 Region, Window, Desktop. null은 취소다. 선택 상태와 좌표 클리핑은 Core의 Geometry에서 계산한다.

- [ ] NegativeDesktopOriginTranslatesCrop에서 desktop=(-1920,-1080,3840,2160), selection=(-1900,-1000,100,50)이 image=(20,80,100,50)인지 검증한다. ReverseDragNormalizesBounds와 ZeroSizeSelectionIsRejected를 추가한다.
- [ ] DisplayChangeCancelsSelection에서 선택 중 디스플레이 변경을 주입하고 결과 null 및 이전 문서 유지 여부를 검증한다.
- [ ] dotnet test SnapBerry.sln --filter "CaptureCoordinateTests|CaptureCoordinatorTests" 실행: 구현 전 실패 확인.
- [ ] 화면 DC 복사, 프레임 경계, 모니터별 오버레이와 공동 선택 상태를 구현한다. 스냅샷 전에 앱 창을 숨기고 표시 반영 후 캡처한다. 창 조회는 오버레이를 제외한 대상 창 목록에서 선택해 오버레이 자체가 잡히지 않게 한다.
- [ ] 각 모니터의 물리 좌표에서 오버레이 DIP로 변환한다. 화면 사이 빈 공간은 검정으로 초기화하고 유효 모니터 영역만 복사한다. Esc·실패 경로에서 창 표시와 핸들을 복원한다.
- [ ] 테스트 PASS 후 실제 영역·창·전체 화면, 125%/150% 혼합 모니터, 음수 좌표, 모니터 경계 드래그를 검증한다. 사용 가능한 장비가 없으면 해당 항목을 미검증으로 기록한다.
- [ ] 커밋한다: feat: capture desktop regions and visible windows.

## Task 4: 편집 도구와 한글 입력

**Files:** Create Editor/EditorWindow.xaml(.cs), EditorViewModel.cs, EditorCanvas.cs, TextAnnotationEditor.xaml(.cs), AnnotationHitTester.cs; Stickers/StickerCatalog.cs, StickerAssetStore.cs, KeycapRenderer.cs, StickerPicker.xaml(.cs), Assets/Stickers/catalog.json과 투명 PNG 48개, THIRD-PARTY-NOTICES.md; tests/SnapBerry.Windows.Tests/StickerTests.cs; tests/SnapBerry.Core.Tests/AnnotationHitTesterTests.cs; tests/SnapBerry.Windows.Tests/EditorInteractionTests.cs.

**Interfaces:** EditorViewModel.Open(CaptureFrame frame) -> void; BeginGesture(PixelPoint point), UpdateGesture(PixelPoint point), CommitGesture(), CancelGesture(); UpdateSelected(Annotation replacement); InsertStepMarker(PixelPoint point); SetStepSequence(StepSequenceState state); InsertSticker(string stickerId, PixelPoint point); UpdateKeycapLabel(Guid annotationId, string label). StickerCatalog.Load() -> IReadOnlyList<StickerDefinition>; StickerDefinition은 ID, Category, Name, PngResourcePath, IsKeycap, DefaultLabel을 가진다. StickerAssetStore.GetPng(string assetHash) -> ReadOnlyMemory<byte>; AddPng(ReadOnlyMemory<byte> png) -> string은 SHA-256을 반환한다. KeycapRenderer.Render(string label) -> byte[]은 투명 PNG를 반환한다. 이력 변경은 Task 2의 명령만 사용한다.

- [ ] TopmostAnnotationIsSelected, DragCreatesOneHistoryEntry, EscapeRestoresOriginalGeometry를 작성하고 선택 순서·이력 수·취소 후 좌표를 검증한다.
- [ ] dotnet test SnapBerry.sln --filter "AnnotationHitTesterTests|EditorInteractionTests" 실행: 구현 전 실패 확인.
- [ ] 선택·화살표·텍스트·번호·스티커·사각형·모자이크 모드와 종류별 속성, 이동·크기 핸들·삭제를 구현한다. 텍스트의 크기 변경 핸들은 글자 크기를 조절하며 줄바꿈은 입력한 줄바꿈을 보존한다.
- [ ] 번호 도구는 Numeric/Alphabetic 세그먼트, 다음 값 입력, 배경·글자색·크기 속성으로 구성한다. 클릭 시 원형 배지를 넣고 InsertStepMarkerCommand로 주석과 카운터를 함께 변경한다. 더블클릭 표시값 수정은 기존 주석 교체만 수행한다. 빈 문자열·잘못된 다음 값은 거부하고 긴 배지 값은 글꼴을 축소해 맞춘다.
- [ ] StickerTests에 CatalogHas48UniqueAssets, EveryAssetLoadsWithTransparency, StickerKeepsAspectRatio, KeycapLabelUpdatesOnlySelectedSticker, UndoKeycapEditRestoresPreviousAsset를 작성한다. 분류별 개수 12/14/6/8/8, PNG 로딩, 비율, 긴 키캡 문구·빈 입력 거부와 이전 이미지 복원을 검증한다.
- [ ] 48개 자산을 자체 제작하거나 허용된 라이선스로 준비하고 출처·라이선스를 기록한다. 이모지는 컬러 PNG, 나머지는 고해상도 투명 PNG로 제공한다. 내장 이미지는 앱 리소스로 포함하며 재편집에 필요한 원본 해상도의 PNG를 자산 사전에 등록한다.
- [ ] 스티커 분류 탭·미리보기 격자·이름·툴팁을 제공한다. 선택 후 클릭 삽입을 반복하고 Esc로 배치를 취소한다. 기본 긴 변은 64 원본 픽셀이고 크기 조정은 비율을 유지한다. 키캡 문구 변경 시 내용에 맞는 폭으로 다시 렌더링하고 새 해시를 주석 교체 명령으로 적용한다. 실행 취소 이력에서 참조하는 자산은 문서를 닫기 전까지 메모리 자산 사전에 보존한다.
- [ ] 줌 10%~400%, 창 맞춤, 스크롤을 구현한다. hit test 허용 범위는 화면 기준 6 DIP로 두어 낮은 줌에서도 선택한다.
- [ ] 텍스트 입력은 WPF TextBox를 사용한다. 도구 전환·출력 시 확정, Esc 시 이전 내용 복원, 빈 내용 제거, IME 조합 중 편집 명령 차단을 구현한다. 텍스트 입력 내부의 Ctrl+Z/Ctrl+Y/Ctrl+C는 TextBox에 전달한다.
- [ ] EditorInteractionTests에 StepMarkerDoubleClickEditsLabel, InvalidNextValueKeepsSequence, LongLabelFitsBadge를 추가하고 번호 삽입·모드 전환·다음 값 변경·표시값 수정을 검증한다.
- [ ] 테스트 PASS와 한글 IME·여러 줄·재편집·줌 변경·경계 밖 주석 수동 검증을 확인한다. 아이콘 버튼에 툴팁·접근성 이름을 부여하고 작은 창에서도 도구막대가 잘리지 않도록 한다.
- [ ] 커밋한다: feat: edit annotations with selection and Korean text input.

## Task 5: 합성 렌더러와 모자이크

**Files:** Create Rendering/CompositeRenderer.cs, MosaicRenderer.cs; Modify Editor/EditorCanvas.cs; Create tests/SnapBerry.Windows.Tests/CompositeRendererTests.cs.

**Interfaces:** ICompositeRenderer.Render를 구현하고 편집 화면·출력에서 동일한 렌더링 경로를 호출한다. 확대 표시 시 합성 이미지에 표시 변환만 적용한다. 선택 핸들은 별도의 UI 계층이다.

- [ ] ExportKeepsOriginalDimensions, SelectionHandlesAreExcluded, OriginalBufferIsUnchanged를 작성한다. 320×200 입력의 출력 크기와 원본 바이트 보존을 검증한다.
- [ ] OverlappingMosaicUsesLargerBlocks는 식별 가능한 합성 픽셀 패턴, blockSize=8과 16의 겹친 영역에서 큰 블록 규칙을 검증한다. AnnotationRendersAboveMosaic, StepMarkerAppearsInExport, StickerTransparencyCompositesCorrectly와 OutOfBoundsAnnotationsAreClipped를 추가한다.
- [ ] dotnet test tests/SnapBerry.Windows.Tests --filter CompositeRendererTests 실행: 구현 전 실패 확인.
- [ ] 원본 합성·모자이크·일반 주석 순서를 구현한다. 모자이크 블록은 이미지 원점에 정렬하고 각 블록의 평균 색을 사용한다. 겹침 영역의 각 출력 픽셀은 해당 위치를 덮는 최대 블록 크기로 원본에서 계산한다.
- [ ] 화면 프리뷰와 텍스트 합성을 같은 글꼴·레이아웃·픽셀 스케일로 처리한다. 이동 중 프리뷰 합성은 취소 가능한 최신 요청만 반영한다.
- [ ] 테스트 PASS와 줌 50%/100%/200%에서 내보내기 픽셀이 변하지 않는지 확인한다.
- [ ] 커밋한다: feat: render consistent images with editable mosaic regions.

## Task 6: PNG 저장과 클립보드

**Files:** Create Export/ExportService.cs, PngFileWriter.cs, ClipboardWriter.cs; Create tests/SnapBerry.Windows.Tests/ExportServiceTests.cs.

**Interfaces:** IExportService의 두 비동기 메서드를 구현한다. 실패는 호출자에게 예외로 전달하며 성공 여부를 임의로 반환하지 않는다. 파일 및 클립보드 어댑터는 테스트에서 실패를 주입할 수 있게 한다.

- [ ] PngRoundTripKeepsDimensionsAndPixels, FailedReplacePreservesExistingFile, ClipboardFailurePreservesDocument를 작성한다. 파일 교체 직전 오류와 클립보드 잠금을 주입하고 기존 바이트·문서 상태 보존을 검증한다.
- [ ] dotnet test tests/SnapBerry.Windows.Tests --filter ExportServiceTests 실행: 구현 전 실패 확인.
- [ ] PNG 인코딩은 같은 디렉터리의 임시 파일로 진행한다. overwrite=false에서 대상이 존재하면 오류로 종료하고, overwrite=true는 UI 확인 이후에만 사용한다. 성공·실패 후 임시 파일을 정리한다.
- [ ] 클립보드는 STA 디스패처에서 SetImage를 호출하고 잠금 오류만 최대 3회, 간격 100ms로 재시도한다. 다른 오류는 즉시 전달한다.
- [ ] 테스트 PASS와 PNG를 외부 이미지 뷰어에서 열기, 클립보드를 그림판에 붙여넣기를 검증한다.
- [ ] 커밋한다: feat: export PNG files and clipboard images.

## Task 7: 캡처 이력의 영속 저장과 복원

**Files:** Create src/SnapBerry.Core/Library/CaptureMetadata.cs, CaptureRecord.cs, CaptureLibraryPage.cs; src/SnapBerry.App/Library/CaptureLibraryStore.cs, CaptureRecordSerializer.cs, ThumbnailCache.cs; tests/SnapBerry.Windows.Tests/CaptureLibraryStoreTests.cs.

**Interfaces:** CaptureMetadata(Guid Id, DateTimeOffset CapturedAtUtc, DateTimeOffset UpdatedAtUtc, int Width, int Height, CaptureMode Mode). CaptureMode는 Task 3에서 만든 Core의 Library 정의를 사용한다. CaptureRecord(CaptureMetadata Metadata, ImageSurface Original, IReadOnlyList<Annotation> Annotations, StepSequenceState StepSequence, IReadOnlyDictionary<string, byte[]> StickerAssets)는 App의 Library/CaptureRecord.cs에 두어 Core가 ImageSurface에 의존하지 않게 한다. Core/Library/CaptureRecord.cs에는 직렬화 가능한 CaptureRecordData(Metadata, Annotations, StepSequence)만 둔다.
ICaptureLibraryStore.CreateAsync(CaptureFrame frame, CaptureMode mode, CancellationToken token) -> Task<CaptureMetadata>.
LoadAsync(Guid id, CancellationToken token) -> Task<CaptureRecord>.
SaveAsync(CaptureRecord record, CancellationToken token) -> Task.
ListAsync(int offset, int limit, CancellationToken token) -> Task<CaptureLibraryPage>.
DeleteAsync(Guid id, CancellationToken token) -> Task.
CaptureLibraryPage는 Items, TotalCount를 가지며 항목은 메타데이터 또는 손상 상태를 가진다. 반환한 LoadAsync 원본 버퍼는 호출자가 해제한다.

- [ ] CaptureLibraryStoreTests에 CaptureWithoutExportSurvivesRestart, ReopenRestoresAnnotations, FailedSavePreservesPreviousRevision을 작성한다. PNG 출력 없이 저장 후 다른 Store 인스턴스로 재개하고 원본 바이트와 화살표·텍스트·번호·스티커·사각형·모자이크의 ID·좌표·스타일·순서를 검증한다. 임시 파일 교체 직전 실패 시 이전 저장 상태가 열리는지 확인한다.
- [ ] StickerAssetsSurviveCatalogRemoval, DuplicateStickerAssetsStoredOnce, MissingStickerAssetDoesNotOverwriteRecord를 추가한다. 앱 내장 목록을 제거한 테스트 환경에서도 저장된 스티커와 키캡의 외형을 복원하고 같은 PNG가 한 번만 저장되는지 검증한다.
- [ ] InterruptedCreateIsNotListed, UnknownSchemaIsNotOverwritten, SameTimestampCapturesHaveUniqueIds를 추가한다. 미완료 임시 파일은 제외하고 손상·알 수 없는 스키마는 목록에 오류 항목으로 남긴다.
- [ ] dotnet test tests/SnapBerry.Windows.Tests --filter CaptureLibraryStoreTests 실행: 구현 전 실패 확인.
- [ ] .qcapture ZIP에 original.png와 document.json을 함께 기록한다. schemaVersion=1, UTC 시각, 픽셀 크기, 캡처 모드와 주석, StepSequenceState 및 사용 중인 스티커 자산 참조를 저장하고 .tmp 작성 완료 후 원자 교체한다. 네이티브 이미지는 PNG로 인코딩하며 ZIP은 System.IO.Compression을 사용한다. 현재 주석이 참조하는 PNG를 assets/<sha256>.png로 한 번씩 저장한다. 삭제한 스티커는 다음 저장에서 제외하되 Undo로 다시 참조하면 메모리 자산에서 재포함한다.
- [ ] 외부 경로를 JSON에서 받아 파일 시스템에 적용하지 않는다. ZIP은 original.png, document.json과 주석이 참조한 assets/<sha256>.png만 읽고 비정상 이미지 크기·무한 좌표·잘못된 주석 종류·0 이하 모자이크 크기는 오류 처리한다. 읽기에도 Task 10의 버퍼 상한을 적용한다. 스티커 해시는 64자리 16진수로 검증하고 파일 내용의 SHA-256과 일치하는지 확인한다. 누락·손상 자산은 오류로 알리고 기존 파일을 덮어쓰지 않는다.
- [ ] ListAsync는 최신 CapturedAtUtc, 동일 시각이면 ID 순서로 정렬하고 UI 페이지 크기는 50개다. 메타데이터만 캐시하고 원본은 열 때 읽는다. 썸네일 캐시는 ID와 수정 상태로 무효화하고 최대 긴 변 240픽셀로 생성한다.
- [ ] 삭제는 해당 GUID의 정식 파일과 캐시로 경로를 제한한다. 현재 항목 저장과 삭제 직렬화는 Task 8에서 담당한다. 삭제 실패는 예외로 전달하고 외부 출력 경로에는 접근하지 않는다.
- [ ] 테스트 PASS와 앱 저장소에 캡처 원본·주석이 함께 남는지 확인하고 커밋한다: feat: persist editable capture records.

## Task 8: 자동 저장과 캡처 이력 화면

**Files:** Create src/SnapBerry.App/Library/AutosaveCoordinator.cs, CaptureLibraryViewModel.cs, CaptureLibraryView.xaml(.cs); Modify Editor/EditorWindow.xaml(.cs), EditorViewModel.cs; Create tests/SnapBerry.Windows.Tests/AutosaveCoordinatorTests.cs, CaptureLibraryViewModelTests.cs.

**Interfaces:** AutosaveCoordinator.Queue(Guid captureId, CaptureRecord snapshot, Guid stateToken) -> void; FlushAsync(CancellationToken token) -> Task; SuspendAndDrainAsync(Guid captureId, CancellationToken token) -> Task; Resume(Guid captureId) -> void.
CaptureLibraryViewModel.RefreshAsync() -> Task; OpenAsync(Guid id) -> Task; DeleteSelectedAsync() -> Task. EditorViewModel.Open(CaptureRecord record) -> void를 기존 Open(CaptureFrame frame)에 추가한다.

- [ ] AutosaveStoresLatestCommittedState, SaveCompletionDoesNotMarkLaterEditPersisted, ReopenStartsWithEmptyUndoStack, ReopenContinuesBothStepSequences를 작성한다. 번호 1·2와 영문 a·b를 삽입해 다시 연 뒤 숫자 3·영문 c를 생성하고 기존 표시값 수정을 검증한다. 저장 중 추가 편집 후 이전 저장 완료가 현재 상태를 clean으로 만들지 않는지 검증한다.
- [ ] DeletingActiveCaptureCannotRecreateRecord, FailedDeletionKeepsItem, DeleteDoesNotRemoveExternalPng, MultiDeleteRemovesOnlySuccessfulItems를 작성한다. 저장 요청과 삭제가 겹쳐도 삭제된 ID가 재생성되지 않는지 확인한다.
- [ ] dotnet test tests/SnapBerry.Windows.Tests --filter "AutosaveCoordinatorTests|CaptureLibraryViewModelTests" 실행: 구현 전 실패 확인.
- [ ] 편집 명령 확정과 실행 취소·다시 실행 후 500ms debounce로 불변 스냅샷을 예약하고 항목별 저장을 직렬화한다. 저장 완료 시 요청 토큰만 MarkPersisted에 전달한다. 미확정 드래그·텍스트 IME 조합은 저장하지 않는다.
- [ ] 왼쪽 이력 목록과 접기, 썸네일·캡처 시각·픽셀 크기, 더블클릭·열기, Ctrl/Shift 다중 선택, 삭제 명령을 구현한다. 날짜는 로컬 시간으로 표시하고 UI 가상화와 50개 페이지 로딩을 적용한다.
- [ ] 항목 전환 전 텍스트를 확정하고 FlushAsync 완료를 기다린다. 저장 실패 시 재시도·취소로 현재 문서를 유지한다. 이미 열려 있는 같은 ID를 재선택하면 불필요한 재로딩을 하지 않는다.
- [ ] 삭제 확인에 항목 수와 복원 불가를 표시한다. 활성 항목은 예약 저장을 차단·배수한 후 삭제하고 성공 시 편집창을 비운다. 실패·취소 시 자동 저장을 재개하고 선택·문서를 유지한다. 일부 성공한 다중 삭제는 성공 항목만 제거한다.
- [ ] 테스트 PASS와 앱 재시작→이력 열기→텍스트·화살표 수정→재시작→수정 유지→다중 삭제를 수동 확인한다. 원본 보관과 이력 삭제 범위를 UI에서 명확히 알 수 있게 한다.
- [ ] 커밋한다: feat: browse reopen autosave and delete capture history.

## Task 9: 캡처와 편집 명령 통합

**Files:** Modify Shell/AppHost.cs, CapturePanel.xaml(.cs), Editor/EditorViewModel.cs, EditorWindow.xaml(.cs); Create tests/SnapBerry.Windows.Tests/AppWorkflowTests.cs.

**Interfaces:** AppHost.RequestCaptureAsync(CaptureMode mode) -> Task; RequestExitAsync() -> Task<bool>. EditorViewModel.ExportAsync(ExportKind kind) -> Task; ExportKind는 Clipboard, Png. 상태는 Idle, Selecting, Editing, Exporting, Persisting, Deleting, Closing으로 관리한다. CaptureMode는 Task 3의 Core 정의를 사용한다.

- [ ] BusyExportRejectsNewCapture, FailedAutosaveBlocksSwitch, FailedFirstSaveKeepsUnsavedCapture, CancelPngExportKeepsHistory, FailedExportDoesNotMarkClean, NewCaptureResetsSelectionAndHistory를 작성한다.
- [ ] dotnet test tests/SnapBerry.Windows.Tests --filter AppWorkflowTests 실행: 구현 전 실패 확인.
- [ ] 새 캡처·이력 열기·종료 전에 텍스트 확정과 FlushAsync를 수행한다. 저장 실패 시 재시도·취소를 제공한다. 출력 성공은 MarkExported, 내부 저장 성공은 MarkPersisted로 분리하고 PNG를 출력하지 않았다는 이유로 전환을 막지 않는다.
- [ ] 캡처 성공 후 CreateAsync로 이력에 보관하고 편집 문서를 전환한다. 첫 저장 실패 시 새 캡처는 메모리에 유지해 재시도 가능하게 하고, 기존 저장 항목은 보존한다. 캡처 취소·실패는 기존 문서를 유지한다. 출력·전환·삭제 중 재진입을 차단하고 끝나면 명령 상태를 복원한다.
- [ ] 복사·저장 결과와 실패 원인을 상태 표시줄에 표시하고 마지막 저장 폴더와 기본 파일명 Capture_yyyyMMdd_HHmmss.png를 연결한다.
- [ ] 테스트 PASS 후 캡처→주석→자동 저장→복사→수정→PNG 출력→이력 재개→삭제→새 캡처 취소 전체 흐름과 종료 확인을 검증한다.
- [ ] 커밋한다: feat: connect capture editing and export workflows.

## Task 10: 단축키 설정과 앱 안정성

**Files:** Create Settings/HotkeyService.cs, SettingsWindow.xaml(.cs), StartupService.cs; Modify Settings/AppSettings.cs, SettingsService.cs, Shell/AppHost.cs; Create tests/SnapBerry.Windows.Tests/HotkeyServiceTests.cs.

**Interfaces:** HotkeyService.TryReplace(CaptureMode mode, HotkeyBinding binding) -> HotkeyRegistrationResult; Dispose(); StartupService.SetEnabled(bool enabled) -> void. HotkeyBinding은 modifier와 가상 키로 구성한다.

- [ ] ConflictingReplacementKeepsOldBinding와 DuplicateBindingsAreRejected를 작성한다. 등록 실패 후 기존 단축키가 계속 등록되어 있는지 검증한다.
- [ ] dotnet test tests/SnapBerry.Windows.Tests --filter HotkeyServiceTests 실행: 구현 전 실패 확인.
- [ ] 초기 세 단축키와 사용자 변경, 등록 충돌 표시를 구현한다. 동일 조합은 재등록 없이 유지하고 다른 조합은 새 등록 성공 후 교체한다.
- [ ] HKCU Run의 QuickCapture 항목으로 선택적 자동 실행을 연결한다. 기본값 false이며 실패 시 설정값을 성공으로 저장하지 않는다. 실행 경로의 공백을 올바르게 인용한다.
- [ ] 캡처 크기는 checked 산술로 계산하고 필수 원본·합성·작업 버퍼의 합이 512MiB를 넘으면 오류와 작은 영역 선택 안내를 표시한다. 이 제한 아래의 메모리 부족도 복구 처리한다.
- [ ] 테스트 PASS와 단축키 충돌·설정 재시작 유지·트레이 종료 후 등록 해제를 확인한다.
- [ ] 커밋한다: feat: configure hotkeys and strengthen application recovery.

## Task 11: 전체 검증과 휴대형 배포

**Files:** Create README.md, docs/verification.md; 패키징 출력은 artifacts/SnapBerry-win-x64/와 artifacts/SnapBerry-win-x64.zip.

**Interfaces:** 사용자가 ZIP을 해제하고 SnapBerry.exe를 실행하면 Task 1~10의 흐름을 제공한다.

- [ ] dotnet restore SnapBerry.sln --locked-mode, dotnet build SnapBerry.sln -c Release --no-restore, dotnet test SnapBerry.sln -c Release --no-build 실행: 세 명령 성공과 테스트 실패 0을 확인한다.
- [ ] 설계서 완료 기준을 실제 Windows에서 검증한다. 100%/125%/150%/200%, 혼합 DPI, 음수 좌표, 한글 조합, 스티커 48개 삽입·키캡 수정·출력·재시작 복원, 취소와 실패, 보호된 콘텐츠의 처리 상태를 기록한다. PNG를 출력하지 않은 캡처의 재시작 후 복원, 저장 중 종료·중단, 활성 항목 삭제, 부분 삭제 실패, 오래된 스키마와 손상 항목을 검증한다. 사용할 수 없는 OS·모니터 환경은 미검증으로 명시한다.
- [ ] 1920×1080 영역 캡처 선택 확정→편집 준비 시간을 10회 측정해 값과 장비를 기록한다. 1초 목표 초과 시 원인을 조사한다. 4K 캡처 50회에서 종료 시점과 버퍼 해제 후 메모리·GDI 핸들 추세를 기록한다.
- [ ] dotnet publish src/SnapBerry.App/SnapBerry.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/SnapBerry-win-x64 실행: SnapBerry.exe와 런타임 파일 생성을 확인한다.
- [ ] 배포 폴더를 압축하고 공백이 있는 새 경로에 풀어 실행한다. 캡처·주석·붙여넣기·저장·종료를 검증하고 README에 실제 실행 방법과 첫 버전 제한을 적는다.
- [ ] 문서와 관련 소스 수정만 커밋한다: docs: record validation and portable distribution. 배포 산출물은 Git에 넣지 않는다.

## 계획 검토 결과와 실행 선택

캡처·편집·출력·설정·배포의 승인 범위를 11개 작업으로 연결했다. 캡처 영속 저장·자동 저장·목록·재편집·삭제를 Task 7~8에 추가하고 기존 통합과 배포 작업을 갱신했다. 보호된 콘텐츠, 모자이크 겹침, 출력 중 재진입, 변경 상태 복귀, 설정 손상과 단축키 교체 실패를 검증 항목으로 배치했다. 아직 제품 코드와 테스트는 실행하지 않았다.

실행 방식은 같은 세션에서 순차 구현하는 Native 방식을 권장한다. 공통 문서·좌표·렌더링 인터페이스에 의존하는 작업이 많아 직접 연결하며 검증하기 적합하다. Subagent-driven 방식은 작업마다 별도 구현자와 검토자를 사용하며 더 많은 컨텍스트 비용이 든다. 사용자에게 계획 검토와 실행 방식 선택을 받은 뒤 해당 실행 스킬을 적용한다.

