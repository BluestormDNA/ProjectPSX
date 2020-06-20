using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectPSX.Devices.CdRom {
    public class XaAdpcm {

        private const int BytesPerHeader = 24;

        private static short oldL;
        private static short olderL;
        private static short oldR;
        private static short olderR;
        private static int sixStep = 6;
        private static int resamplePointer;
        private static short[][] resampleRingBuffer = { new short[32], new short[32] };

        private static int[] positiveXaAdpcmTable = new int[] { 0, 60, 115, 98, 122 };
        private static int[] negativeXaAdpcmTable = new int[] { 0, 0, -52, -55, -60 };

        private static short[][] zigZagTable = new short[][] {
                     new short[]{       0,       0,       0,       0,       0, -0x0002,  0x000A, -0x0022,
                                   0x0041, -0x0054,  0x0034,  0x0009, -0x010A,  0x0400, -0x0A78,  0x234C,
                                   0x6794, -0x1780,  0x0BCD, -0x0623,  0x0350, -0x016D,  0x006B,  0x000A,
                                  -0x0010,  0x0011, -0x0008,  0x0003, -0x0001},

                     new short[]{       0,       0,       0, -0x0002,       0,  0x0003, -0x0013,  0x003C,
                                  -0x004B,  0x00A2, -0x00E3,  0x0132, -0x0043, -0x0267,  0x0C9D,  0x74BB,
                                  -0x11B4,  0x09B8, -0x05BF,  0x0372, -0x01A8,  0x00A6, -0x001B,  0x0005,
                                   0x0006, -0x0008,  0x0003, -0x0001,      0},

                     new short[]{       0,       0, -0x0001,  0x0003, -0x0002, -0x0005,  0x001F, -0x004A,
                                   0x00B3, -0x0192,  0x02B1, -0x039E,  0x04F8, -0x05A6,  0x7939, -0x05A6,
                                   0x04F8, -0x039E,  0x02B1, -0x0192,  0x00B3, -0x004A,  0x001F, -0x0005,
                                  -0x0002,  0x0003, -0x0001,       0,      0},

                     new short[]{       0, -0x0001,  0x0003, -0x0008,  0x0006,  0x0005, -0x001B,  0x00A6,
                                  -0x01A8,  0x0372, -0x05BF,  0x09B8, -0x11B4,  0x74BB,  0x0C9D, -0x0267,
                                  -0x0043,  0x0132, -0x00E3,  0x00A2, -0x004B,  0x003C, -0x0013,  0x0003,
                                        0, -0x0002,       0,       0,      0},

                     new short[]{ -0x0001,  0x0003, -0x0008,  0x0011, -0x0010,  0x000A,  0x006B, -0x016D,
                                   0x0350, -0x0623,  0x0BCD, -0x1780,  0x6794,  0x234C, -0x0A78,  0x0400,
                                  -0x010A,  0x0009,  0x0034, -0x0054,  0x0041, -0x0022,  0x000A, -0x0001,
                                        0,  0x0001,       0,       0,      0},

                     new short[]{  0x0002, -0x0008,  0x0010, -0x0023,  0x002B,  0x001A, -0x00EB,  0x027B,
                                  -0x0548,  0x0AFA, -0x16FA,  0x53E0,  0x3C07, -0x1249,  0x080E, -0x0347,
                                   0x015B, -0x0044, -0x0017,  0x0046, -0x0023,  0x0011, -0x0005,       0,
                                        0,       0,       0,       0,      0},

                     new short[]{ -0x0005,  0x0011, -0x0023,  0x0046, -0x0017, -0x0044,  0x015B, -0x0347,
                                   0x080E, -0x1249,  0x3C07,  0x53E0, -0x16FA,  0x0AFA, -0x0548,  0x027B,
                                  -0x00EB,  0x001A,  0x002B, -0x0023,  0x0010, -0x0008,  0x0002,       0,
                                        0,       0,       0,       0,      0}
        };

        public static byte[] Decode(byte[] xaadpcm, byte codingInfo) {
            List<byte> decoded = new List<byte>();

            List<short> l = new List<short>();
            List<short> r = new List<short>();

            bool isStereo = (codingInfo & 0x1) == 0x1;
            bool is18900hz = ((codingInfo >> 2) & 0x1) == 0x1;
            bool is8BitPerSample = ((codingInfo >> 4) & 0x1) == 0x1;

            if (is18900hz) Console.ReadLine();

            //Console.WriteLine($"decoding XAPCDM {xaadpcm.Length} is18900: {is18900hz} is8Bit: {is8BitPerSample} isStereo: {isStereo}");

            int position = BytesPerHeader; //Skip sync, header and subheader
            for (int i = 0; i < 18; i++) { //Each sector consists of 12h 128-byte portions (=900h bytes) (the remaining 14h bytes of the sectors 914h-byte data region are 00h filled).
                for (int blk = 0; blk < 4; blk++) {

                    if (isStereo) {
                        //Console.WriteLine("isStereo");
                        l.AddRange(decodeNibbles(xaadpcm, position, blk, 0, ref oldL, ref olderL));
                        r.AddRange(decodeNibbles(xaadpcm, position, blk, 1, ref oldR, ref olderR));
                    } else {
                        List<short> m0 = decodeNibbles(xaadpcm, position, blk, 0, ref oldL, ref olderL);
                        List<short> m1 = decodeNibbles(xaadpcm, position, blk, 1, ref oldL, ref olderL);

                        List<short> resampledM0 = resampleTo44100Hz(m0, is18900hz, 0);
                        List<short> resampledM1 = resampleTo44100Hz(m1, is18900hz, 1);

                        for (int sample = 0; sample < resampledM0.Count; sample++) {
                            decoded.Add((byte)resampledM0[sample]);
                            decoded.Add((byte)(resampledM0[sample] >> 8));
                            decoded.Add((byte)resampledM0[sample]);
                            decoded.Add((byte)(resampledM0[sample] >> 8));
                        }

                        for (int sample = 0; sample < resampledM1.Count; sample++) {
                            decoded.Add((byte)resampledM1[sample]);
                            decoded.Add((byte)(resampledM1[sample] >> 8));
                            decoded.Add((byte)resampledM1[sample]);
                            decoded.Add((byte)(resampledM1[sample] >> 8));
                        }
                    }
                    //Console.WriteLine("nextblock " + blk);
                }
                //Console.WriteLine("next i " + i + "position" + position);
                position += 128;
            }

            Console.WriteLine(position + " " + xaadpcm.Length);

            //Console.WriteLine("Sizes" + l.Count + " " + r.Count);
            List<short> resampledL = resampleTo44100Hz(l, is18900hz, 0);
            List<short> resampledR = resampleTo44100Hz(r, is18900hz, 1);
            //Console.WriteLine("Sizes" + resampledL.Count + " " + resampledR.Count);

            for (int sample = 0; sample < resampledL.Count; sample++) {
                decoded.Add((byte)resampledL[sample]);
                decoded.Add((byte)(resampledL[sample] >> 8));
                decoded.Add((byte)resampledR[sample]);
                decoded.Add((byte)(resampledR[sample] >> 8));
            }

            //Console.WriteLine("decoded " + decoded.Count);

            return decoded.ToArray();
        }

        private static List<short> resampleTo44100Hz(List<short> samples, bool is18900hz, int channel) {
            List<short> resamples = new List<short>();

            for (int i = 0; i < samples.Count; i++) {
                resampleRingBuffer[channel][resamplePointer++ & 0x1F] = samples[i];

                sixStep--;
                if (sixStep == 0) {
                    sixStep = 6;
                    //Console.WriteLine("dentro sixStep pre");
                    for (int table = 0; table < 7; table++) {
                        resamples.Add(zigZagInterpolate(resamplePointer, table, channel));
                    }
                    //Console.WriteLine("dentro sixStep post");
                }
            }
            //Console.WriteLine("fin resample");
            return resamples;
        }

        private static short zigZagInterpolate(int resamplePointer, int table, int channel) {
            int sum = 0;
            for (int i = 0; i < 29; i++) {
                //Console.WriteLine("dentro zigzag " + i);
                sum += (resampleRingBuffer[channel][(resamplePointer - i) & 0x1F] * zigZagTable[table][i]) / 0x8000;
                //Console.WriteLine("dentro zigzag post " + i);
            }
            //Console.WriteLine("en sum " + sum);
            return (short)Math.Clamp(sum, -0x8000, 0x7FFF);
        }

        public static List<short> decodeNibbles(byte[] xaapdcm, int position, int blk, int nibble, ref short old, ref short older) {
            List<short> list = new List<short>();

            int shift = 12 - (xaapdcm[position + 4 + blk * 2 + nibble] & 0x0F);
            int filter = (xaapdcm[position + 4 + blk * 2 + nibble] & 0x30) >> 4;

            int f0 = positiveXaAdpcmTable[filter];
            int f1 = negativeXaAdpcmTable[filter];

            for (int i = 0; i < 28; i++) {
                int t = signed4bit((byte)((xaapdcm[position + 16 + blk + i * 4] >> (nibble * 4)) & 0x0F));
                int s = (t << shift) + ((old * f0 + older * f1 + 32) / 64);
                short sample = (short)Math.Clamp(s, -0x8000, 0x7FFF);

                list.Add(sample);
                older = old;
                old = sample;
            }

            return list;
        }

        public static int signed4bit(byte value) {
            return (value << 28) >> 28;
        }
    }
}
