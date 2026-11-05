using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using WangZip.Core.Safety;

namespace WangZip.Core.Zip;

/// <summary>
/// WinZip AES-256(AE-2)로 암호화된 ZIP을 만듭니다.
///
/// 항목 하나의 저장 구조:
///   salt(16) + 비밀번호 확인 바이트(2) + AES-CTR 암문(압축 결과와 같은 길이) + HMAC-SHA1(10)
///
/// 압축을 끝내기 전에는 크기를 알 수 없으므로, 로컬 헤더를 자리만 잡아 두었다가
/// 데이터를 다 쓴 뒤 파일 위치를 되돌려 실제 크기로 채웁니다(데이터 기술자 미사용).
///
/// ZIP64(4GB 이상 항목/압축 파일, 65,535개 초과 항목)는 아직 지원하지 않습니다.
/// </summary>
internal static class EncryptedZipWriter
{
    private const uint LocalHeaderSignature = 0x04034B50;
    private const uint CentralHeaderSignature = 0x02014B50;
    private const uint EndOfCentralDirectorySignature = 0x06054B50;

    private const ushort AesMethod = 99;
    private const ushort AesVersionNeeded = 51;   // 5.1 (AES)
    private const ushort PlainVersionNeeded = 20; // 2.0
    private const ushort VersionMadeBy = 20;

    private const byte AesStrength = 3;           // 1=128, 2=192, 3=256
    private const int AesSaltLength = 16;
    private const int AesKeyLength = 32;
    private const int AesVerifyLength = 2;
    private const int AesAuthLength = 10;

    private const ushort Utf8Flag = 0x0800;
    private const ushort EncryptedFlag = 0x0001;

    private sealed record EntryRecord(
        byte[] Name,
        byte[] Extra,
        ushort Flags,
        ushort Method,
        ushort Time,
        ushort Date,
        uint CompressedSize,
        uint UncompressedSize,
        uint LocalOffset);

    public static CreationResult Create(
        string outputPath,
        List<FileItem> items,
        CreationOptions options,
        long totalBytes,
        List<string> warnings,
        IProgress<ProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        string password = options.Password!;
        var throttle = new ProgressThrottle(progress);
        long processed = 0;

        try
        {
            using var file = new FileStream(
                PathGuard.ToExtended(outputPath), FileMode.Create, FileAccess.ReadWrite,
                FileShare.None, bufferSize: 128 * 1024);

            var records = new List<EntryRecord>(items.Count);
            byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(128 * 1024);

            try
            {
                foreach (FileItem item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    (byte[] name, ushort nameFlags) = EncodeName(item.EntryName, options.NameEncoding);
                    (ushort dosTime, ushort dosDate) = ToDosDateTime(item.LastWriteTime);

                    long localOffset = file.Position;
                    if (localOffset > uint.MaxValue)
                    {
                        throw new ArchiveException("암호 ZIP은 4GB를 넘는 압축 파일을 아직 지원하지 않습니다.");
                    }

                    // 폴더 항목은 암호화하지 않고 빈 항목으로 기록합니다.
                    if (item.EntryName.EndsWith('/'))
                    {
                        WriteLocalHeader(file, name, [], PlainVersionNeeded, nameFlags, 0, dosTime, dosDate, 0, 0, 0);
                        records.Add(new EntryRecord(name, [], nameFlags, 0, dosTime, dosDate, 0, 0, (uint)localOffset));
                        continue;
                    }

                    ushort method = options.Level == CompressionLevel.NoCompression ? (ushort)0 : (ushort)8;
                    byte[] extra = AesExtraField(method);
                    ushort flags = (ushort)(nameFlags | EncryptedFlag);

                    // 크기를 모르는 상태로 헤더를 먼저 씁니다. 데이터를 다 쓴 뒤 되돌아와 채웁니다.
                    WriteLocalHeader(file, name, extra, AesVersionNeeded, flags, AesMethod, dosTime, dosDate, 0, 0, 0);

                    byte[] salt = new byte[AesSaltLength];
                    RandomNumberGenerator.Fill(salt);
                    byte[] derived = Rfc2898DeriveBytes.Pbkdf2(
                        Encoding.UTF8.GetBytes(password), salt, 1000, HashAlgorithmName.SHA1,
                        AesKeyLength * 2 + AesVerifyLength);

                    file.Write(salt);
                    file.Write(derived, AesKeyLength * 2, AesVerifyLength);

                    long compressedPlainBytes;
                    long uncompressedBytes;

                    using (var encryptor = new AesCtrHmacWriteStream(
                               file,
                               derived.AsSpan(0, AesKeyLength),
                               derived.AsSpan(AesKeyLength, AesKeyLength)))
                    {
                        using var source = new FileStream(
                            PathGuard.ToExtended(item.FullPath), FileMode.Open, FileAccess.Read,
                            FileShare.Read, bufferSize: 128 * 1024, FileOptions.SequentialScan);

                        if (method == 8)
                        {
                            using var deflate = new DeflateStream(encryptor, options.Level, leaveOpen: true);
                            uncompressedBytes = CopyThrough(source, deflate, buffer, ref processed, totalBytes,
                                item.EntryName, throttle, cancellationToken);
                        }
                        else
                        {
                            uncompressedBytes = CopyThrough(source, encryptor, buffer, ref processed, totalBytes,
                                item.EntryName, throttle, cancellationToken);
                        }

                        compressedPlainBytes = encryptor.PlainBytesWritten;
                        encryptor.Finish();
                    }

                    long payloadSize = AesSaltLength + AesVerifyLength + compressedPlainBytes + AesAuthLength;
                    if (payloadSize > uint.MaxValue || uncompressedBytes > uint.MaxValue)
                    {
                        throw new ArchiveException($"4GB를 넘는 항목은 암호 ZIP으로 만들 수 없습니다: {item.EntryName}");
                    }

                    long end = file.Position;
                    file.Position = localOffset;
                    WriteLocalHeader(file, name, extra, AesVersionNeeded, flags, AesMethod, dosTime, dosDate,
                        0, (uint)payloadSize, (uint)uncompressedBytes);
                    file.Position = end;

                    records.Add(new EntryRecord(
                        name, extra, flags, AesMethod, dosTime, dosDate,
                        (uint)payloadSize, (uint)uncompressedBytes, (uint)localOffset));
                }
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
            }

            if (records.Count > ushort.MaxValue)
            {
                throw new ArchiveException("항목이 65,535개를 넘는 암호 ZIP은 아직 지원하지 않습니다.");
            }

            long centralOffset = file.Position;
            foreach (EntryRecord record in records)
            {
                WriteCentralHeader(file, record);
            }

            long centralSize = file.Position - centralOffset;
            if (centralOffset > uint.MaxValue || centralSize > uint.MaxValue)
            {
                throw new ArchiveException("암호 ZIP은 4GB를 넘는 압축 파일을 아직 지원하지 않습니다.");
            }

            byte[]? comment = string.IsNullOrEmpty(options.Comment)
                ? null
                : EncodeText(options.Comment!, options.NameEncoding);
            if (comment is { Length: > ushort.MaxValue })
            {
                throw new ArchiveException("압축 파일 설명이 너무 깁니다.");
            }

            WriteEndOfCentralDirectory(
                file, (ushort)records.Count, (uint)centralSize, (uint)centralOffset, comment);
        }
        catch
        {
            ExtractionHelpers.TryDelete(outputPath);
            throw;
        }

        throttle.Report(totalBytes, totalBytes, null, force: true);

        long outputBytes = new FileInfo(outputPath).Length;
        return new CreationResult
        {
            OutputPath = outputPath,
            EntryCount = items.Count,
            SourceBytes = totalBytes,
            OutputBytes = outputBytes,
            Warnings = warnings,
        };
    }

    /// <summary>평문을 읽어 대상 스트림(압축기 또는 암호화 스트림)에 그대로 넘깁니다.</summary>
    private static long CopyThrough(
        Stream source,
        Stream destination,
        byte[] buffer,
        ref long processed,
        long totalBytes,
        string entryName,
        ProgressThrottle throttle,
        CancellationToken cancellationToken)
    {
        long uncompressedBytes = 0;

        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            destination.Write(buffer, 0, read);

            processed += read;
            uncompressedBytes += read;
            throttle.Report(processed, totalBytes, entryName);
        }

        return uncompressedBytes;
    }

    private static void WriteLocalHeader(
        Stream target,
        byte[] name,
        byte[] extra,
        ushort versionNeeded,
        ushort flags,
        ushort method,
        ushort time,
        ushort date,
        uint crc,
        uint compressedSize,
        uint uncompressedSize)
    {
        Span<byte> header = stackalloc byte[30];
        BinaryPrimitives.WriteUInt32LittleEndian(header, LocalHeaderSignature);
        BinaryPrimitives.WriteUInt16LittleEndian(header[4..], versionNeeded);
        BinaryPrimitives.WriteUInt16LittleEndian(header[6..], flags);
        BinaryPrimitives.WriteUInt16LittleEndian(header[8..], method);
        BinaryPrimitives.WriteUInt16LittleEndian(header[10..], time);
        BinaryPrimitives.WriteUInt16LittleEndian(header[12..], date);
        BinaryPrimitives.WriteUInt32LittleEndian(header[14..], crc);
        BinaryPrimitives.WriteUInt32LittleEndian(header[18..], compressedSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header[22..], uncompressedSize);
        BinaryPrimitives.WriteUInt16LittleEndian(header[26..], (ushort)name.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(header[28..], (ushort)extra.Length);

        target.Write(header);
        target.Write(name);
        target.Write(extra);
    }

    private static void WriteCentralHeader(Stream target, EntryRecord record)
    {
        Span<byte> header = stackalloc byte[46];
        ushort versionNeeded = record.Method == AesMethod ? AesVersionNeeded : PlainVersionNeeded;

        BinaryPrimitives.WriteUInt32LittleEndian(header, CentralHeaderSignature);
        BinaryPrimitives.WriteUInt16LittleEndian(header[4..], VersionMadeBy);
        BinaryPrimitives.WriteUInt16LittleEndian(header[6..], versionNeeded);
        BinaryPrimitives.WriteUInt16LittleEndian(header[8..], record.Flags);
        BinaryPrimitives.WriteUInt16LittleEndian(header[10..], record.Method);
        BinaryPrimitives.WriteUInt16LittleEndian(header[12..], record.Time);
        BinaryPrimitives.WriteUInt16LittleEndian(header[14..], record.Date);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], 0);   // AE-2: CRC 미사용
        BinaryPrimitives.WriteUInt32LittleEndian(header[20..], record.CompressedSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header[24..], record.UncompressedSize);
        BinaryPrimitives.WriteUInt16LittleEndian(header[28..], (ushort)record.Name.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(header[30..], (ushort)record.Extra.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(header[32..], 0);   // 설명 없음
        BinaryPrimitives.WriteUInt16LittleEndian(header[34..], 0);   // 시작 디스크
        BinaryPrimitives.WriteUInt16LittleEndian(header[36..], 0);   // 내부 특성
        uint externalAttributes = record.Name.Length > 0 && (record.Flags & EncryptedFlag) == 0 &&
                                  record.CompressedSize == 0 && record.Method == 0
            ? 0x10u   // 빈 폴더 항목
            : 0u;
        BinaryPrimitives.WriteUInt32LittleEndian(header[38..], externalAttributes);
        BinaryPrimitives.WriteUInt32LittleEndian(header[42..], record.LocalOffset);

        target.Write(header);
        target.Write(record.Name);
        target.Write(record.Extra);
    }

    private static void WriteEndOfCentralDirectory(
        Stream target,
        ushort entryCount,
        uint centralSize,
        uint centralOffset,
        byte[]? comment)
    {
        Span<byte> trailer = stackalloc byte[22];
        BinaryPrimitives.WriteUInt32LittleEndian(trailer, EndOfCentralDirectorySignature);
        BinaryPrimitives.WriteUInt16LittleEndian(trailer[4..], 0);
        BinaryPrimitives.WriteUInt16LittleEndian(trailer[6..], 0);
        BinaryPrimitives.WriteUInt16LittleEndian(trailer[8..], entryCount);
        BinaryPrimitives.WriteUInt16LittleEndian(trailer[10..], entryCount);
        BinaryPrimitives.WriteUInt32LittleEndian(trailer[12..], centralSize);
        BinaryPrimitives.WriteUInt32LittleEndian(trailer[16..], centralOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(trailer[20..], (ushort)(comment?.Length ?? 0));

        target.Write(trailer);
        if (comment is not null)
        {
            target.Write(comment);
        }
    }

    /// <summary>WinZip AES 확장 필드(0x9901). 버전 AE-2, 강도 AES-256, 실제 압축 방식.</summary>
    private static byte[] AesExtraField(ushort method)
    {
        var extra = new byte[11];
        BinaryPrimitives.WriteUInt16LittleEndian(extra, 0x9901);
        BinaryPrimitives.WriteUInt16LittleEndian(extra.AsSpan(2), 7);

        extra[4] = 0x02;   // AE-2 (CRC 필드 미사용)
        extra[5] = 0x00;
        extra[6] = (byte)'A';
        extra[7] = (byte)'E';
        extra[8] = AesStrength;
        BinaryPrimitives.WriteUInt16LittleEndian(extra.AsSpan(9), method);
        return extra;
    }

    private static (byte[] Bytes, ushort Flags) EncodeName(string name, NameEncodingMode mode)
    {
        byte[] bytes = mode == NameEncodingMode.Cp949
            ? Encoding.GetEncoding(949).GetBytes(name)
            : Encoding.UTF8.GetBytes(name);

        if (bytes.Length > ushort.MaxValue)
        {
            throw new ArchiveException($"항목 이름이 너무 깁니다: {name}");
        }

        ushort flags = mode == NameEncodingMode.Cp949 ? (ushort)0 : Utf8Flag;
        return (bytes, flags);
    }

    private static byte[] EncodeText(string text, NameEncodingMode mode) =>
        mode == NameEncodingMode.Cp949
            ? Encoding.GetEncoding(949).GetBytes(text)
            : Encoding.UTF8.GetBytes(text);

    private static (ushort Time, ushort Date) ToDosDateTime(DateTime value)
    {
        DateTime local = value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;

        if (local.Year < 1980) local = new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Local);
        if (local.Year > 2107) local = new DateTime(2107, 12, 31, 23, 59, 58, DateTimeKind.Local);

        ushort date = (ushort)(((local.Year - 1980) << 9) | (local.Month << 5) | local.Day);
        ushort time = (ushort)((local.Hour << 11) | (local.Minute << 5) | (local.Second / 2));
        return (time, date);
    }

    /// <summary>AES-CTR로 암문을 만들면서 HMAC-SHA1을 갱신하는 쓰기 전용 스트림.</summary>
    private sealed class AesCtrHmacWriteStream : Stream
    {
        private readonly Stream _target;
        private readonly ICryptoTransform _ecb;
        private readonly IncrementalHash _hmac;
        private readonly byte[] _counter = new byte[16];
        private readonly byte[] _keystream = new byte[16];
        private readonly byte[] _cipher = new byte[16 * 1024];
        private int _keystreamOffset = 16;
        private bool _finished;

        public AesCtrHmacWriteStream(Stream target, ReadOnlySpan<byte> aesKey, ReadOnlySpan<byte> hmacKey)
        {
            _target = target;

            using var aes = Aes.Create();
            aes.Key = aesKey.ToArray();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            _ecb = aes.CreateEncryptor();

            _hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA1, hmacKey.ToArray());
            _counter[0] = 1;   // WinZip AES CTR 카운터 초기값 1 (128비트 리틀엔디언)
        }

        /// <summary>지금까지 암호화한 평문(압축 결과) 바이트 수.</summary>
        public long PlainBytesWritten { get; private set; }

        public override void Write(byte[] buffer, int offset, int count) =>
            Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (_finished)
            {
                throw new InvalidOperationException("이미 마무리된 암호화 스트림입니다.");
            }

            while (!buffer.IsEmpty)
            {
                int chunk = Math.Min(_cipher.Length, buffer.Length);
                for (int i = 0; i < chunk; i++)
                {
                    if (_keystreamOffset >= _keystream.Length)
                    {
                        NextKeystream();
                    }

                    _cipher[i] = (byte)(buffer[i] ^ _keystream[_keystreamOffset++]);
                }

                _hmac.AppendData(_cipher, 0, chunk);
                _target.Write(_cipher, 0, chunk);
                PlainBytesWritten += chunk;
                buffer = buffer[chunk..];
            }
        }

        /// <summary>HMAC-SHA1 앞 10바이트를 암문 뒤에 덧붙입니다.</summary>
        public void Finish()
        {
            if (_finished) return;

            Span<byte> hash = stackalloc byte[20];
            _hmac.GetHashAndReset(hash);
            _target.Write(hash[..AesAuthLength]);
            _finished = true;
        }

        private void NextKeystream()
        {
            _ecb.TransformBlock(_counter, 0, _counter.Length, _keystream, 0);
            _keystreamOffset = 0;

            for (int i = 0; i < _counter.Length; i++)
            {
                if (++_counter[i] != 0) break;
            }
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => !_finished;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _target.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _ecb.Dispose();
                _hmac.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
