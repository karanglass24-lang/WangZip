using WangZip.App;

namespace WangZip.Core.Tests;

public sealed class DragTempCacheTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WangZip-cache-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void PayloadMayContainMetadataNamesAndUnretainedLeaseIsRemoved()
    {
        var cache = new DragTempCache(_root, 1024);
        string operation;
        using (var lease = cache.Reserve(10))
        {
            operation = Directory.GetParent(lease.Payload)!.FullName;
            File.WriteAllText(Path.Combine(lease.Payload, ".active"), "archive");
            File.WriteAllText(Path.Combine(lease.Payload, "lock"), "x");
            lease.ValidateSize();
            Assert.True(File.Exists(Path.Combine(lease.Payload, ".active")));
        }
        Assert.False(Directory.Exists(operation));
    }

    [Fact]
    public void YoungExportsAreProtectedAndFullCacheRejectsReservation()
    {
        var cache = new DragTempCache(_root, 1000);
        string path;
        using (var lease = cache.Reserve(800))
        {
            path = lease.Payload;
            File.WriteAllBytes(Path.Combine(path, "data"), new byte[800]);
            lease.Retain();
        }
        Assert.Throws<DragCacheFullException>(() => cache.Reserve(300));
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void OldInactiveExportIsRemovedButOldActiveOperationIsProtected()
    {
        var cache = new DragTempCache(_root, 1000);
        using var active = cache.Reserve(800);
        string activeRoot = Directory.GetParent(active.Payload)!.FullName;
        Directory.SetLastWriteTimeUtc(activeRoot, DateTime.UtcNow.AddDays(-3));
        Assert.Throws<DragCacheFullException>(() => cache.Reserve(300));
        Assert.True(Directory.Exists(activeRoot));
        active.Retain();
        active.Dispose();
        Directory.SetLastWriteTimeUtc(activeRoot, DateTime.UtcNow.AddDays(-3));
        using var replacement = cache.Reserve(300);
        Assert.False(Directory.Exists(activeRoot));
    }

    [Fact]
    public void OversizedPayloadFailsValidationAndIsCleaned()
    {
        var cache = new DragTempCache(_root, 1000);
        string path;
        using (var lease = cache.Reserve(2))
        {
            path = Directory.GetParent(lease.Payload)!.FullName;
            File.WriteAllBytes(Path.Combine(lease.Payload, "x"), new byte[3]);
            Assert.Throws<DragCacheFullException>(lease.ValidateSize);
        }
        Assert.False(Directory.Exists(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
