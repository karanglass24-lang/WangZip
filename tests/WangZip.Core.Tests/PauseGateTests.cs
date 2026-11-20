using WangZip.Core.Zip;

namespace WangZip.Core.Tests;

public class PauseGateTests
{
    [Fact]
    public async Task Paused_job_waits_until_resumed()
    {
        using var dir = new TempDir();
        dir.Write("src/a.txt", new string('x', 10_000));
        var gate = new PauseGate();
        gate.Pause();

        PauseGate.Current = gate;
        Task<CreationResult> job;
        try
        {
            job = Task.Run(() => ZipWriter.Create(
                dir.File("out.zip"), [dir.File("src/a.txt")], new CreationOptions(), null, CancellationToken.None));
        }
        finally
        {
            PauseGate.Current = null;
        }

        await Task.Delay(400);
        Assert.False(job.IsCompleted);

        gate.Resume();
        CreationResult result = await job.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(File.Exists(result.OutputPath));
    }

    [Fact]
    public async Task Job_without_gate_is_unaffected()
    {
        using var dir = new TempDir();
        dir.Write("src/a.txt", "hello");
        CreationResult result = await Task.Run(() => ZipWriter.Create(
            dir.File("out.zip"), [dir.File("src/a.txt")], new CreationOptions(), null, CancellationToken.None));
        Assert.True(File.Exists(result.OutputPath));
    }
}
