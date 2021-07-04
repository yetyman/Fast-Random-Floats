using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading;

namespace NodeDirectedFuelMap
{
    public unsafe class RandomHelper86
    {
        private float[] fbuffer2 = null;
        private float[] fbuffer1 = null;

        private float[] bbuffer = new float[1];

        private float inverseIntMax = 1 / (float)Int32.MaxValue;


        ManualResetEventSlim backBufferFill = new ManualResetEventSlim(true);
        ManualResetEventSlim fillABuffer = new ManualResetEventSlim(true);
        bool two = false;
        public float Rand()
        {
            FillBuffer(bbuffer);
            return bbuffer[0];
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
            FillBuffer(randomValues);
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
            FillBuffer(randomValues);
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
        public RandomHelper86(ulong? seed = null)
        {
            InitXorShift(seed);
            Thread devotedWorkerThread = new Thread(bgGen);
            devotedWorkerThread.Start();
        }
        private void InitXorShift(ulong? seed = null)
        {
            ulong t = SplitMix64(seed);
            seed += 0x9E3779B97f4A7C15;
            ulong t2 = SplitMix64(seed);
            seed += 0x9E3779B97f4A7C15;
            ulong t3 = SplitMix64(seed);
            seed += 0x9E3779B97f4A7C15;
            ulong t4 = SplitMix64(seed);

            // probably fixme: verify that this way of seeding is actually good?
            X = Vector128.Create((uint)t, (uint)t2, (uint)t3, (uint)t4).AsUInt32();
            Y = Vector128.Create((uint)(t >> 32), (uint)(t2 >> 32), (uint)(t3 >> 32), (uint)(t4 >> 32)).AsUInt32();
            Z = Vector128.Create((uint)t2, (uint)t, (uint)t4, (uint)t3).AsUInt32();
            W = Vector128.Create((uint)(t2 >> 32), (uint)(t >> 32), (uint)(t4 >> 32), (uint)(t3 >> 32)).AsUInt32();

        }
        Vector128<uint> X, Y, Z, W;
        public void FillBuffer(Span<float> buffer)
        {
            Vector128<uint> x = X, y = Y, z = Z, w = W;
            Vector128<float> invMax = Vector128.Create(1.0f / int.MaxValue);
            Vector128<int> intMask = Vector128.Create(0x7F_FF_FF_FF);
            fixed (float* buf_ptr = buffer)
            {
                Vector128<uint>* start = (Vector128<uint>*)buf_ptr;
                uint* end = (uint*)(buf_ptr + buffer.Length);
                while (start < end)
                {
                    Vector128<uint> tx = Sse2.Xor(x, Sse2.ShiftLeftLogical(x, 11));
                    Vector128<uint> ty = Sse2.Xor(y, Sse2.ShiftLeftLogical(y, 11));
                    Vector128<uint> tz = Sse2.Xor(z, Sse2.ShiftLeftLogical(z, 11));
                    Vector128<uint> tw = Sse2.Xor(w, Sse2.ShiftLeftLogical(w, 11));

                    x = Sse2.Xor(w,
                        Sse2.Xor(
                            Sse2.ShiftRightLogical(w, 19),
                            Sse2.Xor(tx,
                                Sse2.ShiftRightLogical(tx, 8))
                            ));

                    y = Sse2.Xor(x,
                        Sse2.Xor(
                            Sse2.ShiftRightLogical(x, 19),
                            Sse2.Xor(ty,
                                Sse2.ShiftRightLogical(ty, 8))
                            ));

                    z = Sse2.Xor(y,
                        Sse2.Xor(
                            Sse2.ShiftRightLogical(y, 19),
                            Sse2.Xor(tz,
                                Sse2.ShiftRightLogical(tz, 8))
                            ));

                    w = Sse2.Xor(z,
                        Sse2.Xor(
                            Sse2.ShiftRightLogical(z, 19),
                            Sse2.Xor(tw,
                                Sse2.ShiftRightLogical(tw, 8))
                            ));

                    Sse2.Store((byte*)(start++), Sse.Multiply(Sse2.ConvertToVector128Single(Sse2.And(x.AsInt32(), intMask)), invMax).AsByte());
                    Sse2.Store((byte*)(start++), Sse.Multiply(Sse2.ConvertToVector128Single(Sse2.And(y.AsInt32(), intMask)), invMax).AsByte());
                    Sse2.Store((byte*)(start++), Sse.Multiply(Sse2.ConvertToVector128Single(Sse2.And(z.AsInt32(), intMask)), invMax).AsByte());
                    Sse2.Store((byte*)(start++), Sse.Multiply(Sse2.ConvertToVector128Single(Sse2.And(w.AsInt32(), intMask)), invMax).AsByte());
                }
            }
            X = x; Y = y; Z = z; W = w;
        }
    }
}
