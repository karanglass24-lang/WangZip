using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WangZip.App;

// Each operation owns a directory containing metadata and a separate archive payload.
// A named mutex serializes reservations across WangZip processes, while the per-entry
// lock remains held until the drag has finished.
internal sealed class DragTempCache
{
    internal const long DefaultLimit = 8L * 1024 * 1024 * 1024;
    internal static readonly TimeSpan MinimumAge = TimeSpan.FromDays(2);
    private readonly string _root;
    private readonly long _limit;
    private readonly TimeSpan _minimumAge;
    private readonly string _mutexName;

    internal DragTempCache(string root, long limit = DefaultLimit, TimeSpan? minimumAge = null)
    {
        _root = Path.GetFullPath(root);
        _limit = limit;
        _minimumAge = minimumAge ?? MinimumAge;
        _mutexName = "WangZipDragCache-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_root.ToUpperInvariant())));
    }

    internal Lease Reserve(long bytes)
    {
        if (bytes < 0 || bytes > _limit) throw new DragCacheFullException();
        using var mutex = new Mutex(false, _mutexName);
        try { mutex.WaitOne(); } catch (AbandonedMutexException) { }
        try
        {
            Directory.CreateDirectory(_root);
            long used = 0;
            foreach (string directory in Directory.EnumerateDirectories(_root))
            {
                bool old = DateTime.UtcNow - Directory.GetLastWriteTimeUtc(directory) >= _minimumAge;
                if (old && TryRemoveOld(directory)) continue;
                long size = DirectorySize(directory);
                string reservation = Path.Combine(directory, "reservation");
                if (File.Exists(reservation) && long.TryParse(File.ReadAllText(reservation), out long reserved))
                    size = Math.Max(size, Math.Max(0, reserved));
                if (size > _limit - used) throw new DragCacheFullException();
                used += size;
            }
            if (bytes > _limit - used) throw new DragCacheFullException();

            string path = Path.Combine(_root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            FileStream? active = null;
            try
            {
                active = new FileStream(Path.Combine(path, "lock"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                File.WriteAllText(Path.Combine(path, "reservation"), bytes.ToString(System.Globalization.CultureInfo.InvariantCulture));
                string payload = Path.Combine(path, "payload");
                Directory.CreateDirectory(payload);
                return new Lease(path, payload, active, bytes);
            }
            catch
            {
                active?.Dispose();
                TryDelete(path);
                throw;
            }
        }
        finally { mutex.ReleaseMutex(); }
    }

    private static bool TryRemoveOld(string path)
    {
        try
        {
            // Also respect the lock used by older, flat-layout builds.
            foreach (string name in new[] { "lock", ".active" })
            {
                string file = Path.Combine(path, name);
                if (File.Exists(file))
                    using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            }
            Directory.Delete(path, true);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static long DirectorySize(string path)
    {
        long total = 0;
        foreach (string entry in Directory.EnumerateFileSystemEntries(path))
        {
            FileAttributes attributes = File.GetAttributes(entry);
            // Never follow a link outside the cache.
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Unexpected link in drag cache.");
            long size = (attributes & FileAttributes.Directory) != 0
                ? DirectorySize(entry) : new FileInfo(entry).Length;
            total = checked(total + size);
        }
        return total;
    }

    private static void TryDelete(string path)
    {
        try { Directory.Delete(path, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    internal sealed class Lease : IDisposable
    {
        private readonly string _path;
        private readonly FileStream _active;
        private readonly long _reserved;
        private bool _retain;
        private bool _disposed;

        internal Lease(string path, string payload, FileStream active, long reserved)
        {
            _path = path;
            Payload = payload;
            _active = active;
            _reserved = reserved;
        }

        internal string Payload { get; }

        internal void ValidateSize()
        {
            // Archive size metadata must not allow an extraction to exceed its reservation.
            if (DirectorySize(Payload) > _reserved) throw new DragCacheFullException();
        }

        internal void Retain()
        {
            Directory.SetLastWriteTimeUtc(_path, DateTime.UtcNow);
            _retain = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _active.Dispose();
            if (!_retain) TryDelete(_path);
        }
    }
}

internal sealed class DragCacheFullException : Exception;
