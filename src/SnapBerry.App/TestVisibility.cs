using System.Runtime.CompilerServices;
// 네이티브 입력·경로 등 내부 구현을 공개 API로 만들지 않고 테스트에서 검증할 수 있게 한다.
[assembly:InternalsVisibleTo("SnapBerry.Windows.Tests")]
