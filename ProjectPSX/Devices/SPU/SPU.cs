using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ProjectPSX.Devices.Spu;

namespace ProjectPSX.Devices {
    public class SPU {

        List<byte> samples = new List<byte>();
        List<short> output = new List<short>();
        Queue<byte> cdbuffer = new Queue<byte>();

        private static int[] positiveXaAdpcmTable = new int[] { 0, 60, 115, 98, 122 };
        private static int[] negativeXaAdpcmTable = new int[] { 0, 0, -52, -55, -60 };

        private byte[] ram = new byte[512 * 1024];
        private Voice[] voices = new Voice[24];

        private ushort mainVolumeLeft;
        private ushort mainVolumeRight;
        private ushort reverbOutputLeft;
        private ushort reverbOutputRight;

        private uint keyOn;
        private uint keyOff;
        private uint channelFMPitchMode;
        private uint channelNoiseMode;
        private uint channelReverbMode;
        private uint endx;

        private ushort unknownA0;

        private ushort ramReverbStartAddress;
        private ushort ramIrqAddress;
        private ushort ramDataTransferAddress;
        private uint ramDataTransferAddressInternal;
        private ushort ramDataTransferFifo;

        private ushort controlRegister; //SPUCNT
        private ushort ramDataTransferControl;
        private ushort statusRegister; //SPUSTAT

        private ushort cdVolumeLeft;
        private ushort cdVolumeRight;
        private ushort externVolumeLeft;
        private ushort externVolumeRight;
        private ushort currentVolumeLeft;
        private ushort currentVolumeRight;

        private uint unknownBC;

        private IHostWindow window;

        public SPU(IHostWindow window) {
            this.window = window;
            for (int i = 0; i < voices.Length; i++) {
                voices[i] = new Voice();
            }
        }

        internal void write(uint addr, ushort value) {
            switch (addr) {
                case uint _ when (addr >= 0x1F801C00 && addr <= 0x1F801D7F):

                    uint index = ((addr & 0xFF0) >> 4) - 0xC0;

                    switch (addr & 0xF) {
                        case 0x0: voices[index].volumeLeft = value; break;
                        case 0x2: voices[index].volumeRight = value; break;
                        case 0x4: voices[index].pitch = value; break;
                        case 0x6: voices[index].startAddress = value; break;
                        case 0x8: voices[index].adsrLo = value; break;
                        case 0xA: voices[index].adsrHi = value; break;
                        case 0xC: voices[index].adsrVolume = value; break;
                        case 0xE: voices[index].adpcmRepeatAddress = value; break;
                    }
                    break;

                case 0x1F801D80:
                    mainVolumeLeft = value;
                    break;

                case 0x1F801D82:
                    mainVolumeRight = value;
                    break;

                case 0x1F801D84:
                    reverbOutputLeft = value;
                    break;

                case 0x1F801D86:
                    reverbOutputRight = value;
                    break;

                case 0x1F801D88:
                    keyOn = (keyOn & 0xFFFF0000) | value;
                    break;

                case 0x1F801D8A:
                    keyOn = (keyOn & 0xFFFF) | (uint)(value << 16);
                    break;

                case 0x1F801D8C:
                    keyOff = (keyOff & 0xFFFF0000) | value;
                    break;

                case 0x1F801D8E:
                    keyOff = (keyOff & 0xFFFF) | (uint)(value << 16);
                    break;

                case 0x1F801D90:
                    channelFMPitchMode = (channelFMPitchMode & 0xFFFF0000) | value;
                    break;

                case 0x1F801D92:
                    channelFMPitchMode = (channelFMPitchMode & 0xFFFF) | (uint)(value << 16);
                    break;

                case 0x1F801D94:
                    channelNoiseMode = (channelNoiseMode & 0xFFFF0000) | value;
                    break;

                case 0x1F801D96:
                    channelNoiseMode = (channelNoiseMode & 0xFFFF) | (uint)(value << 16);
                    break;

                case 0x1F801D98:
                    channelReverbMode = (channelReverbMode & 0xFFFF0000) | value;
                    break;

                case 0x1F801D9A:
                    channelReverbMode = (channelReverbMode & 0xFFFF) | (uint)(value << 16);
                    break;

                case 0x1F801D9C:
                    endx = (endx & 0xFFFF0000) | value;
                    break;

                case 0x1F801D9E:
                    endx = (endx & 0xFFFF) | (uint)(value << 16);
                    break;

                case 0x1F801DA0:
                    unknownA0 = value;
                    break;

                case 0x1F801DA2:
                    ramReverbStartAddress = value;
                    break;

                case 0x1F801DA4:
                    ramIrqAddress = value;
                    break;

                case 0x1F801DA6:
                    ramDataTransferAddress = value;
                    ramDataTransferAddressInternal = (uint)(value * 8);
                    break;

                case 0x1F801DA8:
                    //Console.WriteLine($"[SPU] Manual DMA Write {ramDataTransferAddressInternal:x8} {value:x4}");
                    ramDataTransferFifo = value;
                    ram[ramDataTransferAddressInternal++] = (byte)value;
                    ram[ramDataTransferAddressInternal++] = (byte)(value >> 8);
                    break;

                case 0x1F801DAA:
                    controlRegister = value;
                    break;

                case 0x1F801DAC:
                    ramDataTransferControl = value;
                    break;

                case 0x1F801DAE:
                    statusRegister = value;
                    break;

                case 0x1F801DB0:
                    cdVolumeLeft = value;
                    break;

                case 0x1F801DB2:
                    cdVolumeRight = value;
                    break;

                case 0x1F801DB4:
                    externVolumeLeft = value;
                    break;

                case 0x1F801DB6:
                    externVolumeRight = value;
                    break;

                case 0x1F801DB8:
                    currentVolumeLeft = value;
                    break;

                case 0x1F801DBA:
                    currentVolumeRight = value;
                    break;

                case 0x1F801DBC:
                    unknownBC = (unknownBC & 0xFFFF0000) | value;
                    break;

                case 0x1F801DBE:
                    unknownBC = (unknownBC & 0xFFFF) | (uint)(value << 16);
                    break;
            }

        }

        internal ushort load(uint addr) {
            switch (addr) {
                case uint _ when (addr >= 0x1F801C00 && addr <= 0x1F801D7F):

                    uint index = ((addr & 0xFF0) >> 4) - 0xC0;

                    switch (addr & 0xF) {
                        case 0x0: return voices[index].volumeLeft;
                        case 0x2: return voices[index].volumeRight;
                        case 0x4: return voices[index].pitch;
                        case 0x6: return voices[index].startAddress;
                        case 0x8: return voices[index].adsrLo;
                        case 0xA: return voices[index].adsrHi;
                        case 0xC: return voices[index].adsrVolume;
                        case 0xE: return voices[index].adpcmRepeatAddress;
                    }
                    return 0xFFFF;

                case 0x1F801D80:
                    return mainVolumeLeft;

                case 0x1F801D82:
                    return mainVolumeRight;

                case 0x1F801D84:
                    return reverbOutputLeft;

                case 0x1F801D86:
                    return reverbOutputRight;

                case 0x1F801D88:
                    return (ushort)keyOn;

                case 0x1F801D8A:
                    return (ushort)(keyOn >> 16);

                case 0x1F801D8C:
                    return (ushort)keyOff;

                case 0x1F801D8E:
                    return (ushort)(keyOff >> 16);

                case 0x1F801D90:
                    return (ushort)channelFMPitchMode;

                case 0x1F801D92:
                    return (ushort)(channelFMPitchMode >> 16);

                case 0x1F801D94:
                    return (ushort)channelNoiseMode;

                case 0x1F801D96:
                    return (ushort)(channelNoiseMode >> 16);

                case 0x1F801D98:
                    return (ushort)channelReverbMode;

                case 0x1F801D9A:
                    return (ushort)(channelReverbMode >> 16);

                case 0x1F801D9C:
                    return (ushort)endx;

                case 0x1F801D9E:
                    return (ushort)(endx >> 16);

                case 0x1F801DA0:
                    return unknownA0;
                   
                case 0x1F801DA2:
                    return ramReverbStartAddress;

                case 0x1F801DA4:
                    return ramIrqAddress;

                case 0x1F801DA6:
                    return ramDataTransferAddress;

                case 0x1F801DA8:
                    return ramDataTransferFifo;

                case 0x1F801DAA:
                    return controlRegister;

                case 0x1F801DAC:
                    return ramDataTransferControl;

                case 0x1F801DAE:
                    return statusRegister;

                case 0x1F801DB0:
                    return cdVolumeLeft;

                case 0x1F801DB2:
                    return cdVolumeRight;

                case 0x1F801DB4:
                    return externVolumeLeft;

                case 0x1F801DB6:
                    return externVolumeRight;

                case 0x1F801DB8:
                    return currentVolumeLeft ;

                case 0x1F801DBA:
                    return currentVolumeRight;

                case 0x1F801DBC:
                    return (ushort)unknownBC;

                case 0x1F801DBE:
                    return (ushort)(unknownBC >> 16);

                default:
                    return 0xFFFF;
            }
        }

        internal void pushCdBufferSamples(byte[] decodedXaAdpcm) {
            //Console.WriteLine("cdBuffer was " + cdbuffer.Count + "new data is " + decodedXaAdpcm.Length);
            cdbuffer = new Queue<byte>(decodedXaAdpcm);
        }

        private int counter = 0;
        private int CYCLES_PER_SAMPLE = 0x300; //33868800 / 44100hz
        public bool tick(int cycles) {
            counter += cycles;

            if (counter < CYCLES_PER_SAMPLE) {
                return false;
            }
            counter -= CYCLES_PER_SAMPLE;

            if(cdbuffer.Count > 2) {
                byte cdLLo = cdbuffer.Dequeue();
                byte cdLHi = cdbuffer.Dequeue();
                byte cdRLo = cdbuffer.Dequeue();
                byte cdRHi = cdbuffer.Dequeue();

                samples.Add(cdLLo);
                samples.Add(cdLHi);
                samples.Add(cdRLo);
                samples.Add(cdRHi);
            }
            
            if(samples.Count > 2048) {
                //Console.WriteLine("spu play!");
                window.Play(samples.ToArray());
                samples.Clear();
            }

            return false;
        }

        private void decodeSamples() {
            short sampleLeft = 0;
            short sampleRight = 0;

            short sumLeft = 0;
            short sumRight = 0;

            short[] decoded = new short[28];

            for (int i = 0; i < voices.Length; i++) {
                //todo not process disabled voices
                Voice v = voices[i];

                if ((keyOn & (0x1 << i)) != 0) {
                    endx &= ~(uint)(0x1 << i);
                    v.keyOn();
                }

                if ((keyOff & (0x1 << i)) != 0) {
                    v.keyOff();
                }

                if (v.adsrPhase == Voice.Phase.Off) {
                    return;
                }

                byte[] spuAdpcm = new byte[16];
                Array.Copy(ram, v.currentAddress * 8, spuAdpcm, 0, 16);

                byte flags = spuAdpcm[1];
                bool loopEnd = (flags & 0x1) != 0;
                bool loopRepeat = (flags & 0x2) != 0;
                bool loopStart = (flags & 0x4) != 0;

                if (loopEnd) endx |= (uint)(0x1 << i);
                if (loopEnd && loopRepeat) v.currentAddress = v.adpcmRepeatAddress;
                if (loopStart) v.adpcmRepeatAddress = v.currentAddress;

                v.currentAddress += 2;

                List<short> samples = decodeSpuNibbles(spuAdpcm, ref v.old, ref v.older, ref v.oldest);

                int pointer = 0;
                for (int j = 0; j < samples.Count; j++) {
                    decoded[pointer++] += samples[j];
                }

            }

            for(int k = 0; k < decoded.Length; k++) {
                samples.Add((byte)decoded[k]);
                samples.Add((byte)(decoded[k] >> 8));
            }

        }

        public unsafe void processDma(uint[] load) {
            //Console.WriteLine($"[SPU] Process DMA Length: {load.Length} Address: {ramDataTransferAddressInternal:x8}");
            byte[] dma = Unsafe.As<byte[]>(load);
            foreach (byte b in dma) {
                ram[ramDataTransferAddressInternal++] = b;
                //Console.WriteLine("dma byte " + b);
            }
        }

        public static List<short> decodeSpuNibbles(byte[] xaapdcm, ref short old, ref short older, ref short oldest) {
            List<short> list = new List<short>();

            int shift = 12 - (xaapdcm[0] & 0x0F);
            int filter = (xaapdcm[0] & 0x30) >> 4;

            int f0 = positiveXaAdpcmTable[filter];
            int f1 = negativeXaAdpcmTable[filter];

            for (int i = 2; i < 16; i++) {
                int lo = signed4bit((byte)((xaapdcm[i] >> 0) & 0x0F));
                int hi = signed4bit((byte)((xaapdcm[i] >> 4) & 0x0F));

                int slo = (lo << shift) + ((old * f0 + older * f1 + 32) / 64);
                int shi = (hi << shift) + ((old * f0 + older * f1 + 32) / 64);

                short sampleLo = (short)Math.Clamp(slo, -0x8000, 0x7FFF);
                short sampleHi = (short)Math.Clamp(shi, -0x8000, 0x7FFF);

                list.Add(sampleLo);
                list.Add(sampleHi);
                oldest = older;
                older = old;
                old = sampleLo;
            }

            return list;
        }

        public static int signed4bit(byte value) {
            return (value << 28) >> 28;
        }
    }
}
