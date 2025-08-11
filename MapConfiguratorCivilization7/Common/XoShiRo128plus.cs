
namespace MapConfiguratorCivilization7.Helper
{
    public class XoShiRo128plus
    {
        private uint[] s = new uint[4];

        public XoShiRo128plus(int seed)
        {
            // Use SplitMix32 to fill s[0..3] from a single 32-bit seed
            uint x = (uint)seed;
            for (int i = 0; i < 4; i++)
            {
                s[i] = SplitMix32(ref x);
            }

            // Ensure state is not all zero
            if (s[0] == 0 && s[1] == 0 && s[2] == 0 && s[3] == 0)
            {
                s[0] = 1;
            }
        }

        private static uint SplitMix32(ref uint x)
        {
            uint z = (x += 0x9E3779B9);
            z = (z ^ (z >> 16)) * 0x85EBCA6B;
            z = (z ^ (z >> 13)) * 0xC2B2AE35;
            return z ^ (z >> 16);
        }

        private static uint Rotl(uint x, int k)
        {
            return (x << k) | (x >> (32 - k));
        }

        public uint NextUint()
        {
            uint result = s[0] + s[3];

            uint t = s[1] << 9;

            s[2] ^= s[0];
            s[3] ^= s[1];
            s[1] ^= s[2];
            s[0] ^= s[3];

            s[2] ^= t;

            s[3] = Rotl(s[3], 11);

            return result;
        }

        public float NextFloat()
        {
            return (NextUint() >> 8) / (float)(1 << 24); // 24-bit float precision [0,1)
        }

        public bool NextBool()
        {
            return (NextUint() & 1) == 1;
        }
    }
}
