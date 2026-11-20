using System.IO.Compression;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.SevenZip;
using SharpCompress.Writers.Tar;
using WangZip.Core.Safety;
using WangZip.Core.Zip;

namespace WangZip.Core.SharpCompressSupport;

/// <summary>7z / TAR(.tar, .tar.gz, .tar.bz2) 만들기. ZIP은 <see cref="ZipWriter"/>가 처리합니다.</summary>
internal static class SharpCompressWriter
{
    public static CreationResult Create(
        string outputPath,
        IReadOnlyList<string> sources,
        CreationOptions options,
        IProgress<ProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        if (options.Password is { Length: > 0 })
        {
            throw new UnsupportedArchiveException(Loc.T("암호 걸기는 ZIP 형식에서만 지원합니다."));
        }

        if (options.SplitSizeBytes > 0)
        {
            throw new UnsupportedArchiveException(Loc.T("분할 압축은 ZIP 형식에서만 지원합니다."));
        }

        if (sources.Count == 0)
        {
            throw new ArchiveException(Loc.T("압축할 파일이나 폴더를 선택해 주세요."));
        }

        string fullOutput = Path.GetFullPath(outputPath);
        var warnings = new List<string>();
        List<FileItem> items = ZipWriter.CollectItems(sources, options, fullOutput, warnings);
        if (items.Count == 0)
        {
            throw new ArchiveException(Loc.T("압축할 파일이 없습니다."));
        }

        long totalBytes = items.Sum(item => item.Length);
        Directory.CreateDirectory(PathGuard.ToExtended(Path.GetDirectoryName(fullOutput)!));

        var throttle = new ProgressThrottle(progress);
        long processed = 0;
        string tempOutput = ZipWriter.TempSibling(fullOutput);

        try
        {
            using (var file = new FileStream(
                       PathGuard.ToExtended(tempOutput), FileMode.Create, FileAccess.ReadWrite,
                       FileShare.None, bufferSize: 128 * 1024))
            {
                using IWriter writer = Open(file, options);
                foreach (FileItem item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (item.EntryName.EndsWith('/'))
                    {
                        writer.WriteDirectory(item.EntryName.TrimEnd('/'), item.LastWriteTime);
                        continue;
                    }

                    using var source = new FileStream(
                        PathGuard.ToExtended(item.FullPath), FileMode.Open, FileAccess.Read,
                        FileShare.Read, bufferSize: 128 * 1024, FileOptions.SequentialScan);
                    using var tracked = new TrackingStream(source, cancellationToken, read =>
                    {
                        processed += read;
                        throttle.Report(processed, totalBytes, item.EntryName);
                    });
                    writer.Write(item.EntryName, tracked, item.LastWriteTime);
                }
            }

            File.Move(PathGuard.ToExtended(tempOutput), PathGuard.ToExtended(fullOutput), overwrite: true);
        }
        catch
        {
            ExtractionHelpers.TryDelete(tempOutput);
            throw;
        }

        throttle.Report(totalBytes, totalBytes, null, force: true);

        return new CreationResult
        {
            OutputPath = fullOutput,
            EntryCount = items.Count,
            SourceBytes = totalBytes,
            OutputBytes = new FileInfo(fullOutput).Length,
            Warnings = warnings,
        };
    }

    private static IWriter Open(Stream stream, CreationOptions options)
    {
        int level = options.Level switch
        {
            CompressionLevel.Fastest => 1,
            CompressionLevel.SmallestSize => 9,
            _ => 6,
        };
        bool store = options.Level == CompressionLevel.NoCompression;

        switch (options.Format)
        {
            case CreateFormat.SevenZip:
            {
                var sevenZip = new SevenZipWriterOptions(store ? CompressionType.None : CompressionType.LZMA2)
                {
                    CompressionLevel = level,
                    LeaveStreamOpen = true,
                };
                return WriterFactory.OpenWriter(stream, ArchiveType.SevenZip, sevenZip);
            }

            case CreateFormat.Tar:
            case CreateFormat.TarGz:
            case CreateFormat.TarBz2:
            {
                CompressionType type = options.Format switch
                {
                    CreateFormat.TarGz => CompressionType.GZip,
                    CreateFormat.TarBz2 => CompressionType.BZip2,
                    _ => CompressionType.None,
                };
                var tar = new TarWriterOptions(type, finalizeArchiveOnClose: true)
                {
                    CompressionLevel = level,
                    LeaveStreamOpen = true,
                };
                return WriterFactory.OpenWriter(stream, ArchiveType.Tar, tar);
            }

            default:
                throw new UnsupportedArchiveException(Loc.T("지원하지 않는 압축 형식입니다."));
        }
    }

    /// <summary>읽은 만큼 진행률을 알리고, 취소 요청이 오면 읽기를 중단합니다.</summary>
    private sealed class TrackingStream(Stream inner, CancellationToken token, Action<int> onRead) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            token.ThrowIfCancellationRequested();
            int read = inner.Read(buffer, offset, count);
            if (read > 0) onRead(read);
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            token.ThrowIfCancellationRequested();
            int read = inner.Read(buffer);
            if (read > 0) onRead(read);
            return read;
        }
    }
}
