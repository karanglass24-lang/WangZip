using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using WangZip.Core.Safety;

namespace WangZip.Core.Tests;

/// <summary>
/// 테스트용으로 암호화된 ZIP을 만드는 헬퍼입니다.
/// PKWARE ZipCrypto와 WinZip AES(AE-1)를 직접 기록하므로, 이 파일이 올바르게 풀리면
/// 실제 도구(알집/반디집/7-Zip)가 만든 암호화 ZIP과 호환된다는 의미입니다.
/// </summary>
internal static class EncryptedZipWriter
{
    internal sealed record TestEntry(
        string Name,
        byte[] Data,
        ushort Method,
        string Password,
        byte? AesStrength = null);

    public static void Write(string path, params TestEntry[] entries)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        var locals = new List<(TestEntry Entry, uint Crc, byte[] Payload, byte[] Extra, long Offset)>();

        foreach (TestEntry entry in entries)
        {
            uint crc = Crc32.Compute(entry.Data);
            byte[] compressed = entry.Method == 8 ? CompressDeflate(entry.Data) : entry.Data;
            byte[] payload;
            byte[] extra = [];

            if (entry.AesStrength is byte strength)
            {
                payload = EncryptAes(compressed, entry.Password, strength);
                extra = AesExtraField(strength, entry.Method);
            }
            else
            {
                payload = EncryptZipCrypto(compressed, entry.Password, crc);
            }

            long offset = fs.Position;
            ushort method = entry.AesStrength is not null ? (ushort)99 : entry.Method;

            fs.Write(LocalHeader(string.Empty, entry.Name, method, crc, payload.Length, entry.Data.Length, extra));
            fs.Write(payload);

            locals.Add((entry, crc, payload, extra, offset));
        }

        long cdOffset = fs.Position;
        foreach (var (entry, crc, payload, extra, offset) in locals)
        {
            ushort method = entry.AesStrength is not null ? (ushort)99 : entry.Method;
            fs.Write(CentralHeader(entry.Name, method, crc, payload.Length, entry.Data.Length, extra, offset));
        }
        long cdSize = fs.Position - cdOffset;

        Span<byte> eocd = stackalloc byte[22];
        BinaryPrimitives.WriteUInt32LittleEndian(eocd, 0x06054B50);
        BinaryPrimitives.WriteUInt16LittleEndian(eocd[4..], 0);
        BinaryPrimitives.WriteUInt16LittleEndian(eocd[6..], 0);
        BinaryPrimitives.WriteUInt16LittleEndian(eocd[8..], (ushort)entries.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(eocd[10..], (ushort)entries.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(eocd[12..], (uint)cdSize);
        BinaryPrimitives.WriteUInt32LittleEndian(eocd[16..], (uint)cdOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(eocd[20..], 0);
        fs.Write(eocd);
    }

    private static byte[] CompressDeflate(byte[] data)
    {
        var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionMode.Compress))
        {
            deflate.Write(data);
        }
        return output.ToArray();
    }

    private static byte[] LocalHeader(string _, string name, ushort method, uint crc, int compressedSize, int uncompressedSize, byte[] extra)
    {
        var ms = new MemoryStream();

        void F(ushort value) { var b = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(b, value); ms.Write(b); }
        void U(uint value) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, value); ms.Write(b); }

        U(0x04034B50);                    // 시그니처
        F(20);                            // 버전
        F(0x0001);                        // 플래그: 암호화
        F(method);
        F(45620); F(20000);               // 시간/날짜 고정값
        U(crc);
        U((uint)compressedSize);
        U((uint)uncompressedSize);
        F((ushort)Encoding.UTF8.GetByteCount(name));
        F((ushort)extra.Length);
        ms.Write(Encoding.UTF8.GetBytes(name));
        ms.Write(extra);
        return ms.ToArray();
    }

    private static byte[] CentralHeader(
        string name, ushort method, uint crc, int compressedSize, int uncompressedSize, byte[] extra, long offset)
    {
        var ms = new MemoryStream();

        void F(ushort value) { var b = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(b, value); ms.Write(b); }
        void U(uint value) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, value); ms.Write(b); }

        U(0x02014B50);
        F(20); F(20);
        F(0x0001);
        F(method);
        F(45620); F(20000);
        U(crc);
        U((uint)compressedSize);
        U((uint)uncompressedSize);
        F((ushort)Encoding.UTF8.GetByteCount(name));
        F((ushort)extra.Length);
        F(0);
        F(0);
        F(0);
        U(0);
        U((uint)offset);
        ms.Write(Encoding.UTF8.GetBytes(name));
        ms.Write(extra);
        return ms.ToArray();
    }

    /// <summary>WinZip AES 확장 필드(0x9901). 강도와 실제 압축 방식을 담습니다.</summary>
    private static byte[] AesExtraField(byte strength, ushort method)
    {
        byte[] data = new byte[7];
        data[0] = 0x01;  // 버전 하위: AE-1
        data[1] = 0x01;
        data[2] = 0x41;  // 'A'
        data[3] = 0x45;  // 'E'
        data[4] = strength;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(5), method);
        return _Extra(0x9901, data);
    }

    private static byte[] _Extra(ushort id, byte[] data)
    {
        var buffer = new byte[4 + data.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, id);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(2), (ushort)data.Length);
        data.CopyTo(buffer, 4);
        return buffer;
    }

    // ---- PKWARE ZipCrypto ----

    /// <summary>PKWARE 전통 암호화로 평문(STORE/DEFLATE 결과)을 암호화합니다.</summary>
    internal static byte[] EncryptZipCrypto(byte[] plain, string password, uint originalCrc)
    {
        uint key0 = 0x12345678, key1 = 0x23456789, key2 = 0x34567890;

        void Update(byte b)
        {
            key0 = Crc32.UpdateByte(key0, b);
            key1 = unchecked((uint)(key1 + (byte)key0) * 134775813 + 1);
            key2 = Crc32.UpdateByte(key2, (byte)(key1 >> 24));
        }

        foreach (byte b in Encoding.UTF8.GetBytes(password)) Update(b);

        // 복호화 쪽: mask = ((key2 | 2) * ((key2 | 2) ^ 1)) >> 8의 하위 1바이트
        byte Mask() => (byte)(((uint)(((uint)key2 | 2) * (((uint)key2 | 2) ^ 1)) >> 8) & 0xFF);

        var output = new byte[plain.Length + 12];

        // 12바이트 헤더: 마지막 바이트는 비밀번호 확인 값(CRC 상위 1바이트)
        var header = new byte[12];
        Random.Shared.NextBytes(header);
        header[11] = (byte)((originalCrc >> 24) & 0xFF);

        for (int i = 0; i < header.Length; i++)
        {
            output[i] = (byte)(header[i] ^ Mask());
            Update(header[i]);
        }

        for (int i = 0; i < plain.Length; i++)
        {
            output[12 + i] = (byte)(plain[i] ^ Mask());
            Update(plain[i]);
        }

        return output;
    }

    /// <summary>
    /// WinZip AES(AE-1/AE-2)로 암호화합니다.
    /// CTR 모드(128비트 리틀엔디언 카운터, 초기값 1) — 결과: salt + 확인2바이트 + 암문(길이 동일) + HMAC10바이트.
    /// HMAC 대상은 암문만입니다.
    /// </summary>
    internal static byte[] EncryptAes(byte[] plain, string password, byte strength)
    {
        int keySize = strength switch { 1 => 16, 2 => 24, 3 => 32, _ => 32 };
        int saltLength = strength switch { 1 => 8, 2 => 12, 3 => 16, _ => 16 };

        var salt = new byte[saltLength];
        Random.Shared.NextBytes(salt);

        byte[] derived = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            1000,
            HashAlgorithmName.SHA1,
            keySize * 2 + 2);

        byte[] aesKey = derived[..keySize];
        byte[] hmacKey = derived[keySize..(keySize * 2)];
        byte[] verify = derived[(keySize * 2)..];

        using var aes = Aes.Create();
        aes.KeySize = keySize * 8;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = aesKey;

        byte[] cipher = new byte[plain.Length];
        var counter = new byte[16];
        counter[0] = 1;   // 카운터 초기값 1
        int keystreamOffset = 16;
        var keystream = new byte[16];

        for (int i = 0; i < plain.Length; i++)
        {
            if (keystreamOffset >= 16)
            {
                aes.EncryptEcb(counter, keystream, System.Security.Cryptography.PaddingMode.None);
                keystreamOffset = 0;
                for (int b = 0; b < 16; b++)
                {
                    if (++counter[b] != 0) break;
                }
            }
            cipher[i] = (byte)(plain[i] ^ keystream[keystreamOffset++]);
        }

        using var hmac = new HMACSHA1(hmacKey);
        byte[] authCode = hmac.ComputeHash(cipher)[..10];

        var output = new byte[saltLength + 2 + cipher.Length + 10];
        salt.CopyTo(output, 0);
        verify.CopyTo(output, saltLength);
        cipher.CopyTo(output, saltLength + 2);
        authCode.CopyTo(output, saltLength + 2 + cipher.Length);
        return output;
    }
}
