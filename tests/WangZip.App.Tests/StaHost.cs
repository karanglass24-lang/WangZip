using System.Collections.Concurrent;
using System.Windows;

namespace WangZip.App.Tests;

/// <summary>WPF 는 STA 스레드와 단일 Application 이 필요하므로, 전용 스레드 하나에서 모든 UI 테스트를 실행합니다.</summary>
internal static class StaHost
{
    private static readonly BlockingCollection<Action> Queue = new();
    private static readonly Thread Worker;

    static StaHost()
    {
        Worker = new Thread(() =>
        {
            // pack://application:,,,/ 경로가 테스트 호스트가 아니라 왕집 어셈블리를 가리키게 합니다.
            // (테스트 호스트에서는 이미 설정되어 공개 속성으로는 바꿀 수 없어 리플렉션을 씁니다.)
            typeof(Application)
                .GetField("_resourceAssembly", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .SetValue(null, typeof(App).Assembly);
            var app = new App();
            app.InitializeComponent();
            foreach (Action work in Queue.GetConsumingEnumerable()) work();
        })
        {
            IsBackground = true,
            Name = "WangZip-UI-tests",
        };
        Worker.SetApartmentState(ApartmentState.STA);
        Worker.Start();
    }

    public static T Run<T>(Func<T> func)
    {
        var done = new TaskCompletionSource<T>();
        Queue.Add(() =>
        {
            try { done.SetResult(func()); }
            catch (Exception exception) { done.SetException(exception); }
        });
        return done.Task.GetAwaiter().GetResult();
    }

    public static void Run(Action action) => Run(() => { action(); return 0; });

    public static Application Current => Application.Current;
}
