using Xunit;

// Loc은 정적 상태(언어)를 가지므로 테스트를 순차 실행합니다.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
