using Xunit;
// WPF Dispatcher·클립보드·전역 입력 등 공유 데스크톱 자원의 테스트 간 충돌을 막는다.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
