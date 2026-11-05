using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using WangZip.Core.Safety;

namespace WangZip.Core.Zip;

/// <summary>
/// ZIP에서 쓰이는 두 암호화 방식:
/// - PKWARE 전통 암호화(ZipCrypto, 일반적인 "압축 파일 암호")
/// - WinZip AES (AES-128/192/256, 확장 필드 0x9901)
/// </summary>
internal static class ZipCryptography
{
    /// <summary>WinZip AES 확장 필드(0x9901)를 찾으면 (강도, 실제 압축 방식)을 돌려줍니다.</summary>
    public static (byte Strength, ushort Method)? FindAesExtension(ReadOnlySpan<byte> extra)
    {
        int position = 0;
        while (position + 4 <= extra.Length)
        {
            ushort id = BinaryPrimitives.ReadUInt16LittleEndian(extra[position..]);
            ushort size = BinaryPrimitives.ReadUInt16LittleEndian(extra[(position + 2)..]);
            position += 4;
            if (position + size > extra.Length) break;

            if (id == 0x9901 && size >= 7)
            {
                // 0-1: 버전(AE-1/AE-2), 2-3: 벤더 "AE", 4: 강도(1/2/3), 5-6: 실제 압축 방식
                ReadOnlySpan<byte> data = extra.Slice(position, size);
                byte strength = data[4];
                ushort method = BinaryPrimitives.ReadUInt16LittleEndian(data[5..]);
                return (strength, method);
            }

            position += size;
        }

        return null;
    }

    public static int SaltSize(byte strength) => strength switch { 1 => 8, 2 => 12, 3 => 16, _ => 16 };
    public static int KeySize(byte strength) => strength switch { 1 => 16, 2 => 24, 3 => 32, _ => 32 };

    public static Stream Decrypt(ZipEntryKey key, Stream source, string password, long encryptedSize)
    {
        if (key.AesStrength is byte strength)
        {
            return new WinZipAesStream(key.Name, source, password, strength, encryptedSize);
        }

        return new PkwareCryptoStream(key.Name, source, password, key);
    }
}

/// <summary>암호화된 항목 하나를 푸는 데 필요한 정보 묶음.</summary>
internal sealed class ZipEntryKey
{
    public required string Name { get; init; }
    public required byte[] Extra { get; init; }
    public required ushort Method { get; init; }
    public required uint Crc { get; init; }
    public required ushort ModTime { get; init; }
    public required ushort Flags { get; init; }

    public static ZipEntryKey FromRaw(RawZipEntry entry) => new()
    {
        Name = entry.DecodedName,
        Extra = entry.Extra,
        Method = entry.Method,
        Crc = entry.Crc,
        ModTime = entry.ModTime,
        Flags = entry.Flags,
    };

    public byte? AesStrength => ZipCryptography.FindAesExtension(Extra)?.Strength;

    /// <summary>중앙 디렉터리의 Method(99 = AES)가 아니라 실제 압축 방식(STORE/DEFLATE).</summary>
    public ushort RealMethod => ZipCryptography.FindAesExtension(Extra) is { } aes ? aes.Method : Method;

    public bool IsAesEntry => AesStrength is not null;
}

/// <summary>PKWARE 전통 암호화(ZipCrypto) 스트림. 원본 형식(STORE/DEFLATE) 그대로 읽으면서 바이트 단위로 풉니다.</summary>
internal sealed class PkwareCryptoStream : Stream
{
    private const int HeaderLength = 12;

    private readonly Stream _source;
    private readonly string _name;
    private uint _key0 = 0x12345678;
    private uint _key1 = 0x23456789;
    private uint _key2 = 0x34567890;

    public PkwareCryptoStream(string name, Stream source, string password, ZipEntryKey key)
    {
        _source = source;
        _name = name;

        foreach (byte b in Encoding.UTF8.GetBytes(password))
        {
            UpdateKeys(b);
        }

        VerifyHeader(key);
    }

    private void UpdateKeys(byte plain)
    {
        _key0 = Crc32.UpdateByte(_key0, plain);
        _key1 = unchecked((uint)(_key1 + (byte)_key0) * 134775813 + 1);
        _key2 = Crc32.UpdateByte(_key2, (byte)(_key1 >> 24));
    }

    private void VerifyHeader(ZipEntryKey key)
    {
        Span<byte> header = stackalloc byte[HeaderLength];
        ReadExactly(_source, header);

        for (int i = 0; i < header.Length; i++)
        {
            byte plain = DecryptByte(header[i]);
            header[i] = plain;
            UpdateKeys(plain);
        }

        byte expected = (key.Flags & 0x0008) != 0
            ? (byte)((key.ModTime >> 8) & 0xFF)
            : (byte)((key.Crc >> 24) & 0xFF);

        if (header[^1] != expected)
        {
            throw new EncryptedArchiveException(
                Loc.F("암호가 올바르지 않습니다. 압축 파일에 걸린 비밀번호를 확인해 주세요.\n항목: {0}", _name));
        }
    }

    private byte DecryptByte(byte cipher) =>
        (byte)(cipher ^ (byte)(((uint)(((uint)_key2 | 2) * (((uint)_key2 | 2) ^ 1)) >> 8) & 0xFF));

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        if (read <= 0) return 0;

        for (int i = offset; i < offset + read; i++)
        {
            byte plain = DecryptByte(buffer[i]);
            buffer[i] = plain;
            UpdateKeys(plain);   // 키는 피 평문으로 갱신
        }

        return read;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _source.Length - HeaderLength;
    public override long Position { get => _source.Position - HeaderLength; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _source.Dispose();
        base.Dispose(disposing);
    }

    private static void ReadExactly(Stream source, Span<byte> target)
    {
        int total = 0;
        while (total < target.Length)
        {
            int read = source.Read(target[total..]);
            if (read <= 0) throw new EndOfStreamException(Loc.T("암호 항목 헤더를 읽지 못했습니다."));
            total += read;
        }
    }
}

/// <summary>
/// WinZip AES(AE-1/AE-2) 암호화 항목 스트림.
/// CTR 모드(128비트 리틀엔디언 카운터, 초기값 1)이며 암문만 HMAC-SHA1로 검증합니다.
/// </summary>
internal sealed class WinZipAesStream : Stream
{
    private const int AuthCodeLength = 10;
    private const int VerifyLength = 2;
    private const int BlockSize = 16;

    private readonly Stream _source;
    private readonly ICryptoTransform _blockEncryptor;   // AES-ECB (CTR 키스트림 생성용)
    private readonly byte[] _counterBlock = new byte[BlockSize];
    private readonly byte[] _keystream = new byte[BlockSize];
    private readonly HMACSHA1 _hmac;
    private readonly long _payloadLength;
    private readonly string _name;
    private readonly byte[] _cipherBuffer = new byte[16 * 1024];
    private long _cipherConsumed;
    private int _keystreamOffset = BlockSize;   // 새 키스트림 블록이 필요함
    private bool _authVerified;

    public WinZipAesStream(string name, Stream source, string password, byte strength, long encryptedSize)
    {
        _source = source;
        _name = name;

        int saltLength = ZipCryptography.SaltSize(strength);
        int keySize = ZipCryptography.KeySize(strength);

        var header = new byte[saltLength + VerifyLength];   // salt + 비밀번호 확인 바이트
        ReadExactly(_source, header);

        byte[] derived = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            header.AsSpan(0, saltLength).ToArray(),
            1000,
            HashAlgorithmName.SHA1,
            keySize * 2 + VerifyLength);

        var aesKey = derived.AsSpan(0, keySize).ToArray();
        var hmacKey = derived.AsSpan(keySize, keySize).ToArray();

        // 파생 마지막 2바이트(비밀번호 확인 값)가 스트림의 확인 바이트와 같아야 올바른 비밀번호입니다.
        if (!derived.AsSpan(keySize * 2).SequenceEqual(header.AsSpan(saltLength)))
        {
            throw new EncryptedArchiveException(
                Loc.T("비밀번호가 올바르지 않습니다. 압축 파일에 걸린 비밀번호를 확인해 주세요."));
        }

        // HMAC은 암문(데이터 부분)만 대상입니다 (salt/확인바이트 제외).
        _hmac = new HMACSHA1(hmacKey);

        // CTR 모드에서는 키스트림 생성에 AES 암호화 방향을 씁니다.
        using var aes = Aes.Create();
        aes.KeySize = keySize * 8;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = aesKey;
        _blockEncryptor = aes.CreateEncryptor();

        // 카운터 초기값 1 (128비트 리틀엔디언)
        _counterBlock[0] = 1;

        // payload = 암호화 전체 크기 - (salt+확인바이트) - HMAC 10바이트. CTR이므로 암문 길이 == 평문 길이.
        _payloadLength = encryptedSize - header.Length - AuthCodeLength;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_cipherConsumed >= _payloadLength)
        {
            VerifyAuth();
            return 0;
        }

        int toRead = (int)Math.Min(_cipherBuffer.Length, Math.Min((long)count, _payloadLength - _cipherConsumed));
        int read = 0;
        while (read < toRead)
        {
            int chunk = _source.Read(_cipherBuffer, read, toRead - read);
            if (chunk <= 0)
            {
                throw new ArchiveException(Loc.F("암호화된 데이터가 잘렸습니다: {0}", _name));
            }
            read += chunk;
        }

        _cipherConsumed += read;
        _hmac.TransformBlock(_cipherBuffer, 0, read, null, 0);

        // CTR: 평문 = 암문 XOR 키스트림
        for (int i = 0; i < read; i++)
        {
            if (_keystreamOffset >= BlockSize)
            {
                NextKeystream();
            }

            buffer[offset + i] = (byte)(_cipherBuffer[i] ^ _keystream[_keystreamOffset++]);
        }

        return read;
    }

    private void NextKeystream()
    {
        _blockEncryptor.TransformBlock(_counterBlock, 0, BlockSize, _keystream, 0);
        _keystreamOffset = 0;

        // 128비트 리틀엔디언 카운터 증가
        for (int i = 0; i < BlockSize; i++)
        {
            if (++_counterBlock[i] != 0) break;
        }
    }

    /// <summary>스트림을 끝까지 읽었을 때 항목 뒤의 인증 코드와 대조합니다.</summary>
    private void VerifyAuth()
    {
        if (_authVerified) return;
        _hmac.TransformFinalBlock([], 0, 0);
        byte[] hash = _hmac.Hash!;
        var authCode = new byte[AuthCodeLength];
        ReadExactly(_source, authCode);
        _authVerified = true;

        for (int i = 0; i < authCode.Length; i++)
        {
            if (hash[i] != authCode[i])
            {
                throw new ArchiveException(
                    Loc.F("비밀번호가 맞지만 내용이 깨진 파일처럼 보입니다: {0}", _name));
            }
        }
    }

    private static void ReadExactly(Stream source, Span<byte> target)
    {
        int total = 0;
        while (total < target.Length)
        {
            int read = source.Read(target[total..]);
            if (read <= 0) throw new EndOfStreamException(Loc.T("암호화 데이터를 읽지 못했습니다."));
            total += read;
        }
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _payloadLength;
    public override long Position { get => _cipherConsumed; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _blockEncryptor.Dispose();
            _hmac.Dispose();
            _source.Dispose();
        }
        base.Dispose(disposing);
    }
}
