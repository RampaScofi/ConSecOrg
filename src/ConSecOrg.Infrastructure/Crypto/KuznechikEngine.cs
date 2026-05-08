namespace ConSecOrg.Infrastructure.Crypto;

// GOST R 34.12-2015 "Kuznechik" (Grasshopper) block cipher
// 128-bit block, 256-bit key, 10 rounds
// Reference: https://www.tc26.ru/standard/gost/GOST_R_3412-2015.pdf
public sealed class KuznechikEngine
{
    public const int BlockSize = 16;
    public const int KeySize = 32;

    // Pi substitution table (S-box)
    private static readonly byte[] Pi =
    [
        0xFC, 0xEE, 0xDD, 0x11, 0xCF, 0x6E, 0x31, 0x16,
        0xFB, 0xC4, 0xFA, 0xDA, 0x23, 0xC5, 0x04, 0x4D,
        0xE9, 0x77, 0xF0, 0xDB, 0x93, 0x2E, 0x99, 0xBA,
        0x17, 0x36, 0xF1, 0xBB, 0x14, 0xCD, 0x5F, 0xC1,
        0xF9, 0x18, 0x65, 0x5A, 0xE2, 0x5C, 0xEF, 0x21,
        0x81, 0x1C, 0x3C, 0x42, 0x8B, 0x01, 0x8E, 0x4F,
        0x05, 0x84, 0x02, 0xAE, 0xE3, 0x6A, 0x8F, 0xA0,
        0x06, 0x0B, 0xED, 0x98, 0x7F, 0xD4, 0xD3, 0x1F,
        0xEB, 0x34, 0x2C, 0x51, 0xEA, 0xC8, 0x48, 0xAB,
        0xF2, 0x2A, 0x68, 0xA2, 0xFD, 0x3A, 0xCE, 0xCC,
        0xB5, 0x70, 0x0E, 0x56, 0x08, 0x0C, 0x76, 0x12,
        0xBF, 0x72, 0x13, 0x47, 0x9C, 0xB7, 0x5D, 0x87,
        0x15, 0xA1, 0x96, 0x29, 0x10, 0x7B, 0x9A, 0xC7,
        0xF3, 0x91, 0x78, 0x6F, 0x9D, 0x9E, 0xB2, 0xB1,
        0x32, 0x75, 0x19, 0x3D, 0xFF, 0x35, 0x8A, 0x7E,
        0x6D, 0x54, 0xC6, 0x80, 0xC3, 0xBD, 0x0D, 0x57,
        0xDF, 0xF5, 0x24, 0xA9, 0x3E, 0xA8, 0x43, 0xC9,
        0xD7, 0x79, 0xD6, 0xF6, 0x7C, 0x22, 0xB9, 0x03,
        0xE0, 0x0F, 0xEC, 0xDE, 0x7A, 0x94, 0xB0, 0xBC,
        0xDC, 0xE8, 0x28, 0x50, 0x4E, 0x33, 0x0A, 0x4A,
        0xA7, 0x97, 0x60, 0x73, 0x1E, 0x00, 0x62, 0x44,
        0x1A, 0xB8, 0x38, 0x82, 0x64, 0x9F, 0x26, 0x41,
        0xAD, 0x45, 0x46, 0x92, 0x27, 0x5E, 0x55, 0x2F,
        0x8C, 0xA3, 0xA5, 0x7D, 0x69, 0xD5, 0x95, 0x3B,
        0x07, 0x58, 0xB3, 0x40, 0x86, 0xAC, 0x1D, 0xF7,
        0x30, 0x37, 0x6B, 0xE4, 0x88, 0xD9, 0xE7, 0x89,
        0xE1, 0x1B, 0x83, 0x49, 0x4C, 0x3F, 0xF8, 0xFE,
        0x8D, 0x53, 0xAA, 0x90, 0xCA, 0xD8, 0x85, 0x61,
        0x20, 0x71, 0x67, 0xA4, 0x2D, 0x2B, 0x09, 0x5B,
        0xCB, 0x9B, 0x25, 0xD0, 0xBE, 0xE5, 0x6C, 0x52,
        0x59, 0xA6, 0x74, 0xD2, 0xE6, 0xF4, 0xB4, 0xC0,
        0xD1, 0x66, 0xAF, 0xC2, 0x39, 0x4B, 0x63, 0xB6
    ];

    // Inverse Pi (S-box inverse for decryption)
    private static readonly byte[] PiInv;

    // Linear transform coefficients (vector l from standard)
    private static readonly byte[] L = [148, 32, 133, 16, 194, 192, 1, 251, 1, 192, 194, 16, 133, 32, 148, 1];

    // Precomputed L-transform tables
    private static readonly byte[,] LTable = new byte[16, 256];
    private static readonly byte[,] LInvTable = new byte[16, 256];

    // GF(2^8) polynomial: x^8 + x^7 + x^6 + x + 1 = 0x1C3
    private const int Poly = 0x1C3;

    static KuznechikEngine()
    {
        // Build inverse S-box
        PiInv = new byte[256];
        for (int i = 0; i < 256; i++)
            PiInv[Pi[i]] = (byte)i;

        // Build L-transform lookup tables
        BuildLTables();
    }

    private static byte GfMul(int a, int b)
    {
        byte result = 0;
        for (int i = 0; i < 8; i++)
        {
            if ((b & 1) != 0) result ^= (byte)a;
            bool hiBit = (a & 0x80) != 0;
            a = (a << 1) & 0xFF;
            if (hiBit) a ^= (Poly & 0xFF);
            b >>= 1;
        }
        return result;
    }

    private static void BuildLTables()
    {
        for (int i = 0; i < 16; i++)
        {
            for (int v = 0; v < 256; v++)
            {
                LTable[i, v] = GfMul((byte)v, L[i]);
                LInvTable[i, v] = GfMul((byte)v, L[15 - i]);
            }
        }
    }

    // S-transform: byte substitution
    private static void S(byte[] a)
    {
        for (int i = 0; i < 16; i++)
            a[i] = Pi[a[i]];
    }

    private static void SInv(byte[] a)
    {
        for (int i = 0; i < 16; i++)
            a[i] = PiInv[a[i]];
    }

    // R-transform: one step of L (LFSR)
    private static void R(byte[] a)
    {
        byte m = LTable[0, a[0]];
        for (int i = 1; i < 16; i++)
            m ^= LTable[i, a[i]];
        // Shift right and insert m at position 0
        for (int i = 15; i > 0; i--)
            a[i] = a[i - 1];
        a[0] = m;
    }

    private static void RInv(byte[] a)
    {
        // Save the leading byte (L[15]=1 so no multiplication needed for this term)
        byte m = a[0];
        for (int i = 0; i < 15; i++)
            a[i] = a[i + 1];
        // x_15 = m ⊕ L[0]*a_1 ⊕ L[1]*a_2 ⊕ ... ⊕ L[14]*a_15  (uses forward LTable)
        byte x = m;
        for (int i = 0; i < 15; i++)
            x ^= LTable[i, a[i]];
        a[15] = x;
    }

    // L-transform: 16 rounds of R
    private static void LTransform(byte[] a)
    {
        for (int i = 0; i < 16; i++) R(a);
    }

    private static void LTransformInv(byte[] a)
    {
        for (int i = 0; i < 16; i++) RInv(a);
    }

    // X-transform: XOR with round key
    private static void X(byte[] a, byte[] k)
    {
        for (int i = 0; i < 16; i++)
            a[i] ^= k[i];
    }

    // Key schedule — generates 10 round keys from 256-bit key
    public static byte[][] ExpandKey(byte[] key)
    {
        if (key.Length != KeySize)
            throw new ArgumentException("Key must be 32 bytes.", nameof(key));

        var rk = new byte[10][];
        for (int i = 0; i < 10; i++) rk[i] = new byte[16];

        Buffer.BlockCopy(key, 0, rk[0], 0, 16);
        Buffer.BlockCopy(key, 16, rk[1], 0, 16);

        var c = new byte[16];
        var temp = new byte[16];

        for (int i = 0; i < 4; i++)
        {
            byte[] a = (byte[])rk[2 * i].Clone();
            byte[] b = (byte[])rk[2 * i + 1].Clone();

            for (int j = 0; j < 8; j++)
            {
                // Generate iteration constant C[8i+j]
                Array.Clear(c, 0, 16);
                c[15] = (byte)(8 * i + j + 1);
                LTransform(c);

                // F function
                Buffer.BlockCopy(a, 0, temp, 0, 16);
                X(temp, c);
                S(temp);
                LTransform(temp);
                X(temp, b);
                Buffer.BlockCopy(a, 0, b, 0, 16);
                Buffer.BlockCopy(temp, 0, a, 0, 16);
            }

            Buffer.BlockCopy(a, 0, rk[2 * i + 2], 0, 16);
            Buffer.BlockCopy(b, 0, rk[2 * i + 3], 0, 16);
        }

        return rk;
    }

    public static void Encrypt(byte[] block, int offset, byte[][] roundKeys)
    {
        var a = new byte[16];
        Buffer.BlockCopy(block, offset, a, 0, 16);

        for (int i = 0; i < 9; i++)
        {
            X(a, roundKeys[i]);
            S(a);
            LTransform(a);
        }
        X(a, roundKeys[9]);

        Buffer.BlockCopy(a, 0, block, offset, 16);
    }

    public static void Decrypt(byte[] block, int offset, byte[][] roundKeys)
    {
        var a = new byte[16];
        Buffer.BlockCopy(block, offset, a, 0, 16);

        X(a, roundKeys[9]);
        for (int i = 8; i >= 0; i--)
        {
            LTransformInv(a);
            SInv(a);
            X(a, roundKeys[i]);
        }

        Buffer.BlockCopy(a, 0, block, offset, 16);
    }

    // CTR mode encryption/decryption (symmetric)
    public static byte[] CtrProcess(byte[] input, byte[] key, byte[] nonce)
    {
        if (nonce.Length != 16) throw new ArgumentException("Nonce must be 16 bytes.", nameof(nonce));
        var roundKeys = ExpandKey(key);
        var output = new byte[input.Length];
        var counter = (byte[])nonce.Clone();
        var keystream = new byte[16];
        int pos = 0;

        while (pos < input.Length)
        {
            Buffer.BlockCopy(counter, 0, keystream, 0, 16);
            Encrypt(keystream, 0, roundKeys);

            int blockEnd = Math.Min(pos + 16, input.Length);
            for (int i = pos; i < blockEnd; i++)
                output[i] = (byte)(input[i] ^ keystream[i - pos]);

            IncrementCounter(counter);
            pos += 16;
        }

        Array.Clear(keystream, 0, 16);
        return output;
    }

    private static void IncrementCounter(byte[] counter)
    {
        for (int i = 15; i >= 0; i--)
        {
            if (++counter[i] != 0) break;
        }
    }
}
