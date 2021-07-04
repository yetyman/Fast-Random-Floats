using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NodeDirectedFuelMap
{
    public class RandomHelper
    {
        private float[] fbuffer2 = null;
        private float[] fbuffer1 = null;

        private byte[] bbuffer = new byte[sizeof(UInt32)];

        private float inverseIntMax = 1 / (float)Int32.MaxValue;


        ManualResetEventSlim backBufferFill = new ManualResetEventSlim(true);
        ManualResetEventSlim fillABuffer = new ManualResetEventSlim(true);
        bool two = false;
        public float Rand()
        {
            FillBuffer(bbuffer, 0, sizeof(UInt32));
            return BitConverter.ToUInt32(bbuffer, 0)/(float)UInt32.MaxValue;
        }

        public void Rand(float[] randomValues)
        {
            if ((fbuffer1?.Length ?? 0) < randomValues.Length)
                fbuffer1 = new float[randomValues.Length];
            if ((fbuffer2?.Length ?? 0) < randomValues.Length)
                fbuffer2 = new float[randomValues.Length];

            backBufferFill.Wait();//wait until one of the buffers is ready for use.
            backBufferFill.Reset();
            fillABuffer.Set();

            if (two)
                fbuffer1.CopyTo(randomValues, 0);
            else
                fbuffer2.CopyTo(randomValues, 0);
        }
        public void RandDirect(float[] randomValues)
        {
            var fs = new Span<float>(randomValues);
            var bs = MemoryMarshal.Cast<float, byte>(fs);
            var ns = MemoryMarshal.Cast<byte, int>(bs);

            FillBuffer(bs, 0, bs.Length);

            int i = 0;
            int l = ns.Length;
            while (i < l)
                randomValues[i] = (ns[i++] & 0x7F_FF_FF_FF) * inverseIntMax;
        }
        public float[] Rand(int length)
        {
            if ((fbuffer1?.Length ?? 0) < length)
                fbuffer1 = new float[length];
            if ((fbuffer2?.Length ?? 0) < length)
                fbuffer2 = new float[length];

            backBufferFill.Wait();//wait until one of the buffers is ready for use.
            backBufferFill.Reset();
            fillABuffer.Set();

            if (two)
                return fbuffer1; //.CopyTo(randomValues, 0);
            else
                return fbuffer2; //.CopyTo(randomValues, 0);
        }
        public void BgRand(Span<float> randomValues)
        {
            var bs = MemoryMarshal.Cast<float, byte>(randomValues);
            var ns = MemoryMarshal.Cast<byte, int>(bs);

            FillBuffer(bs, 0, bs.Length);

            int i = 0;
            int l = ns.Length;
            while (i < l)
                randomValues[i] = (ns[i++] & 0x7F_FF_FF_FF) * inverseIntMax;
        }
        private void bgGen()
        {
            while (true)
            {
                fillABuffer.Wait();
                fillABuffer.Reset();
                if (two)
                    BgRand(fbuffer2);
                else
                    BgRand(fbuffer1);
                two = !two;
                backBufferFill.Set();
            }
        }
        private ulong SplitMix64(ulong? nseed = null)
        {
            var seed = nseed ?? (ulong)DateTime.Now.Ticks;

            ulong result = seed += 0x9E3779B97f4A7C15;
            result = (result ^ (result >> 30)) * 0xBF58476D1CE4E5B9;
            result = (result ^ (result >> 27)) * 0x94D049BB133111EB;
            return result ^ (result >> 31);
        }
        public RandomHelper(ulong? seed = null)
        {
            InitXorShift(seed);
            Thread devotedWorkerThread = new Thread(bgGen);
            devotedWorkerThread.Start();
        }
        private void InitXorShift(ulong? seed = null)
        {
            ulong t = SplitMix64(seed);
            _x = (uint)t;
            _y = (uint)(t >> 32);

            seed += 0x9E3779B97f4A7C15;

            t = SplitMix64(seed);
            _z = (uint)t;
            _w = (uint)(t >> 32);

        }
        private uint _x, _y, _z, _w;
        private unsafe void FillBuffer(Span<byte> buf, int offset, int offsetEnd)
        {
            uint x = _x, y = _y, z = _z, w = _w; // copy the state into locals temporarily
            fixed (byte* pbytes = buf)
            {
                uint* pbuf = (uint*)(pbytes + offset);
                uint* pend = (uint*)(pbytes + offsetEnd);
                while (pbuf < pend)
                {
                    uint tx = x ^ (x << 11);
                    uint ty = y ^ (y << 11);
                    uint tz = z ^ (z << 11);
                    uint tw = w ^ (w << 11);
                    *(pbuf++) = x = w ^ (w >> 19) ^ (tx ^ (tx >> 8));
                    *(pbuf++) = y = x ^ (x >> 19) ^ (ty ^ (ty >> 8));
                    *(pbuf++) = z = y ^ (y >> 19) ^ (tz ^ (tz >> 8));
                    *(pbuf++) = w = z ^ (z >> 19) ^ (tw ^ (tw >> 8));
                }
            }
            _x = x; _y = y; _z = z; _w = w;

        }
    }
}
