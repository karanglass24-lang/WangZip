namespace WangZip.Core.Safety;

/// <summary>CRC-32 (IEEE 802.3, ZIP 표준) 계산기.</summary>
public static class Crc32
{
    private static readonly uint[] Table = CreateTable();

    private static uint[] CreateTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[i] = c;
        }

        return table;
    }

    public static uint Append(uint crc, ReadOnlySpan<byte> data)
    {
        uint value = crc ^ 0xFFFFFFFFu;
        foreach (byte b in data)
        {
            value = Table[(value ^ b) & 0xFF] ^ (value >> 8);
        }

        return value ^ 0xFFFFFFFFu;
    }

    public static uint Compute(ReadOnlySpan<byte> data) => Append(0, data);
}
