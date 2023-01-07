using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ProjectPSX.Devices.CdRom;
using ProjectPSX.Devices.Spu;

namespace ProjectPSX.Devices {
    public class SPU {

        // Todo:
        // lr sweep envelope
        // clean up queue/list dequeues enqueues and casts

        private static short[] gaussTable = new short[] {
                -0x001, -0x001, -0x001, -0x001, -0x001, -0x001, -0x001, -0x001,
                -0x001, -0x001, -0x001, -0x001, -0x001, -0x001, -0x001, -0x001,
                0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0001,
                0x0001, 0x0001, 0x0001, 0x0002, 0x0002, 0x0002, 0x0003, 0x0003,
                0x0003, 0x0004, 0x0004, 0x0005, 0x0005, 0x0006, 0x0007, 0x0007,
                0x0008, 0x0009, 0x0009, 0x000A, 0x000B, 0x000C, 0x000D, 0x000E,
                0x000F, 0x0010, 0x0011, 0x0012, 0x0013, 0x0015, 0x0016, 0x0018,
                0x0019, 0x001B, 0x001C, 0x001E, 0x0020, 0x0021, 0x0023, 0x0025,
                0x0027, 0x0029, 0x002C, 0x002E, 0x0030, 0x0033, 0x0035, 0x0038,
                0x003A, 0x003D, 0x0040, 0x0043, 0x0046, 0x0049, 0x004D, 0x0050,
                0x0054, 0x0057, 0x005B, 0x005F, 0x0063, 0x0067, 0x006B, 0x006F,
                0x0074, 0x0078, 0x007D, 0x0082, 0x0087, 0x008C, 0x0091, 0x0096,
                0x009C, 0x00A1, 0x00A7, 0x00AD, 0x00B3, 0x00BA, 0x00C0, 0x00C7,
                0x00CD, 0x00D4, 0x00DB, 0x00E3, 0x00EA, 0x00F2, 0x00FA, 0x0101,
                0x010A, 0x0112, 0x011B, 0x0123, 0x012C, 0x0135, 0x013F, 0x0148,
                0x0152, 0x015C, 0x0166, 0x0171, 0x017B, 0x0186, 0x0191, 0x019C,
                0x01A8, 0x01B4, 0x01C0, 0x01CC, 0x01D9, 0x01E5, 0x01F2, 0x0200,
                0x020D, 0x021B, 0x0229, 0x0237, 0x0246, 0x0255, 0x0264, 0x0273,
                0x0283, 0x0293, 0x02A3, 0x02B4, 0x02C4, 0x02D6, 0x02E7, 0x02F9,
                0x030B, 0x031D, 0x0330, 0x0343, 0x0356, 0x036A, 0x037E, 0x0392,
                0x03A7, 0x03BC, 0x03D1, 0x03E7, 0x03FC, 0x0413, 0x042A, 0x0441,
                0x0458, 0x0470, 0x0488, 0x04A0, 0x04B9, 0x04D2, 0x04EC, 0x0506,
                0x0520, 0x053B, 0x0556, 0x0572, 0x058E, 0x05AA, 0x05C7, 0x05E4,
                0x0601, 0x061F, 0x063E, 0x065C, 0x067C, 0x069B, 0x06BB, 0x06DC,
                0x06FD, 0x071E, 0x0740, 0x0762, 0x0784, 0x07A7, 0x07CB, 0x07EF,
                0x0813, 0x0838, 0x085D, 0x0883, 0x08A9, 0x08D0, 0x08F7, 0x091E,
                0x0946, 0x096F, 0x0998, 0x09C1, 0x09EB, 0x0A16, 0x0A40, 0x0A6C,
                0x0A98, 0x0AC4, 0x0AF1, 0x0B1E, 0x0B4C, 0x0B7A, 0x0BA9, 0x0BD8,
                0x0C07, 0x0C38, 0x0C68, 0x0C99, 0x0CCB, 0x0CFD, 0x0D30, 0x0D63,
                0x0D97, 0x0DCB, 0x0E00, 0x0E35, 0x0E6B, 0x0EA1, 0x0ED7, 0x0F0F,
                0x0F46, 0x0F7F, 0x0FB7, 0x0FF1, 0x102A, 0x1065, 0x109F, 0x10DB,
                0x1116, 0x1153, 0x118F, 0x11CD, 0x120B, 0x1249, 0x1288, 0x12C7,
                0x1307, 0x1347, 0x1388, 0x13C9, 0x140B, 0x144D, 0x1490, 0x14D4,
                0x1517, 0x155C, 0x15A0, 0x15E6, 0x162C, 0x1672, 0x16B9, 0x1700,
                0x1747, 0x1790, 0x17D8, 0x1821, 0x186B, 0x18B5, 0x1900, 0x194B,
                0x1996, 0x19E2, 0x1A2E, 0x1A7B, 0x1AC8, 0x1B16, 0x1B64, 0x1BB3,
                0x1C02, 0x1C51, 0x1CA1, 0x1CF1, 0x1D42, 0x1D93, 0x1DE5, 0x1E37,
                0x1E89, 0x1EDC, 0x1F2F, 0x1F82, 0x1FD6, 0x202A, 0x207F, 0x20D4,
                0x2129, 0x217F, 0x21D5, 0x222C, 0x2282, 0x22DA, 0x2331, 0x2389,
                0x23E1, 0x2439, 0x2492, 0x24EB, 0x2545, 0x259E, 0x25F8, 0x2653,
                0x26AD, 0x2708, 0x2763, 0x27BE, 0x281A, 0x2876, 0x28D2, 0x292E,
                0x298B, 0x29E7, 0x2A44, 0x2AA1, 0x2AFF, 0x2B5C, 0x2BBA, 0x2C18,
                0x2C76, 0x2CD4, 0x2D33, 0x2D91, 0x2DF0, 0x2E4F, 0x2EAE, 0x2F0D,
                0x2F6C, 0x2FCC, 0x302B, 0x308B, 0x30EA, 0x314A, 0x31AA, 0x3209,
                0x3269, 0x32C9, 0x3329, 0x3389, 0x33E9, 0x3449, 0x34A9, 0x3509,
                0x3569, 0x35C9, 0x3629, 0x3689, 0x36E8, 0x3748, 0x37A8, 0x3807,
                0x3867, 0x38C6, 0x3926, 0x3985, 0x39E4, 0x3A43, 0x3AA2, 0x3B00,
                0x3B5F, 0x3BBD, 0x3C1B, 0x3C79, 0x3CD7, 0x3D35, 0x3D92, 0x3DEF,
                0x3E4C, 0x3EA9, 0x3F05, 0x3F62, 0x3FBD, 0x4019, 0x4074, 0x40D0,
                0x412A, 0x4185, 0x41DF, 0x4239, 0x4292, 0x42EB, 0x4344, 0x439C,
                0x43F4, 0x444C, 0x44A3, 0x44FA, 0x4550, 0x45A6, 0x45FC, 0x4651,
                0x46A6, 0x46FA, 0x474E, 0x47A1, 0x47F4, 0x4846, 0x4898, 0x48E9,
                0x493A, 0x498A, 0x49D9, 0x4A29, 0x4A77, 0x4AC5, 0x4B13, 0x4B5F,
                0x4BAC, 0x4BF7, 0x4C42, 0x4C8D, 0x4CD7, 0x4D20, 0x4D68, 0x4DB0,
                0x4DF7, 0x4E3E, 0x4E84, 0x4EC9, 0x4F0E, 0x4F52, 0x4F95, 0x4FD7,
                0x5019, 0x505A, 0x509A, 0x50DA, 0x5118, 0x5156, 0x5194, 0x51D0,
                0x520C, 0x5247, 0x5281, 0x52BA, 0x52F3, 0x532A, 0x5361, 0x5397,
                0x53CC, 0x5401, 0x5434, 0x5467, 0x5499, 0x54CA, 0x54FA, 0x5529,
                0x5558, 0x5585, 0x55B2, 0x55DE, 0x5609, 0x5632, 0x565B, 0x5684,
                0x56AB, 0x56D1, 0x56F6, 0x571B, 0x573E, 0x5761, 0x5782, 0x57A3,
                0x57C3, 0x57E2, 0x57FF, 0x581C, 0x5838, 0x5853, 0x586D, 0x5886,
                0x589E, 0x58B5, 0x58CB, 0x58E0, 0x58F4, 0x5907, 0x5919, 0x592A,
                0x593A, 0x5949, 0x5958, 0x5965, 0x5971, 0x597C, 0x5986, 0x598F,
                0x5997, 0x599E, 0x59A4, 0x59A9, 0x59AD, 0x59B0, 0x59B2, 0x59B3,
        };

        private byte[] spuOutput = new byte[2048];
        private int spuOutputPointer;

        private Sector cdBuffer = new Sector(Sector.XA_BUFFER);

        private unsafe byte* ram = (byte*)Marshal.AllocHGlobal(512 * 1024);
        private Voice[] voices = new Voice[24];

        private short mainVolumeLeft;
        private short mainVolumeRight;
        private short reverbOutputVolumeLeft;
        private short reverbOutputVolumeRight;

        private uint keyOn;
        private uint keyOff;
        private uint pitchModulationEnableFlags;
        private uint channelNoiseMode;
        private uint channelReverbMode;
        private uint endx;

        private ushort unknownA0;

        private uint ramReverbStartAddress;
        private uint ramReverbInternalAddress;
        private ushort ramIrqAddress;
        private ushort ramDataTransferAddress;
        private uint ramDataTransferAddressInternal;
        private ushort ramDataTransferFifo;

        private ushort ramDataTransferControl;

        private ushort cdVolumeLeft;
        private ushort cdVolumeRight;
        private ushort externVolumeLeft;
        private ushort externVolumeRight;
        private ushort currentVolumeLeft;
        private ushort currentVolumeRight;

        private uint unknownBC;

        private int captureBufferPos;

        //Reverb Area
        private uint dAPF1;   // Reverb APF Offset 1
        private uint dAPF2;   // Reverb APF Offset 2
        private short vIIR;   // Reverb Reflection Volume 1
        private short vCOMB1; // Reverb Comb Volume 1
        private short vCOMB2; // Reverb Comb Volume 2
        private short vCOMB3; // Reverb Comb Volume 3
        private short vCOMB4; // Reverb Comb Volume 4
        private short vWALL;  // Reverb Reflection Volume 2
        private short vAPF1;  // Reverb APF Volume 1
        private short vAPF2;  // Reverb APF Volume 2
        private uint mLSAME;  // Reverb Same Side Reflection Address 1 Left
        private uint mRSAME;  // Reverb Same Side Reflection Address 1 Right
        private uint mLCOMB1; // Reverb Comb Address 1 Left
        private uint mLCOMB2; // Reverb Comb Address 2 Left
        private uint mRCOMB1; // Reverb Comb Address 1 Right
        private uint mRCOMB2; // Reverb Comb Address 2 Right
        private uint dLSAME;  // Reverb Same Side Reflection Address 2 Left
        private uint dRSAME;  // Reverb Same Side Reflection Address 2 Right
        private uint mLDIFF;  // Reverb Different Side Reflection Address 1 Left
        private uint mRDIFF;  // Reverb Different Side Reflection Address 1 Right
        private uint mLCOMB3; // Reverb Comb Address 3 Left
        private uint mRCOMB3; // Reverb Comb Address 3 Right
        private uint mLCOMB4; // Reverb Comb Address 4 Left
        private uint mRCOMB4; // Reverb Comb Address 4 Right
        private uint dLDIFF;  // Reverb Different Side Reflection Address 2 Left
        private uint dRDIFF;  // Reverb Different Side Reflection Address 2 Right
        private uint mLAPF1;  // Reverb APF Address 1 Left
        private uint mRAPF1;  // Reverb APF Address 1 Right
        private uint mLAPF2;  // Reverb APF Address 2 Left
        private uint mRAPF2;  // Reverb APF Address 2 Right
        private short vLIN;   // Reverb Input Volume Left
        private short vRIN;   // Reverb Input Volume Right

        private struct Control {
            public ushort register;
            public bool spuEnabled => ((register >> 15) & 0x1) != 0;
            public bool spuUnmuted => ((register >> 14) & 0x1) != 0;
            public int noiseFrequencyShift => (register >> 10) & 0xF;
            public int noiseFrequencyStep => (register >> 8) & 0x3;
            public bool reverbMasterEnabled => ((register >> 7) & 0x1) != 0;
            public bool irq9Enabled => ((register >> 6) & 0x1) != 0;
            public int soundRamTransferMode => (register >> 4) & 0x3;
            public bool externalAudioReverb => ((register >> 3) & 0x1) != 0;
            public bool cdAudioReverb => ((register >> 2) & 0x1) != 0;
            public bool externalAudioEnabled => ((register >> 1) & 0x1) != 0;
            public bool cdAudioEnabled => (register & 0x1) != 0;
        }
        Control control;

        private struct Status {
            public ushort register;
            public bool isSecondHalfCaptureBuffer => ((register >> 11) & 0x1) != 0;
            public bool dataTransferBusyFlag => ((register >> 10) & 0x1) != 0;
            public bool dataTransferDmaReadRequest => ((register >> 9) & 0x1) != 0;
            public bool dataTransferDmaWriteRequest => ((register >> 8) & 0x1) != 0;
            //  7     Data Transfer DMA Read/Write Request ;seems to be same as SPUCNT.Bit5
            public bool irq9Flag {
                get { return ((register >> 6) & 0x1) != 0; }
                set { register = value ? (ushort)(register | (1 << 6)) : (ushort)(register & ~(1 << 6)); }
            }
            //  0..5     Mode same as SPUCNT 0..5
        }
        Status status;

        private IHostWindow window;
        private InterruptController interruptController;

        public SPU(IHostWindow window, InterruptController interruptController) {
            this.window = window;
            this.interruptController = interruptController;

            for (int i = 0; i < voices.Length; i++) {
                voices[i] = new Voice();
            }
        }

        internal void write(uint addr, ushort value) {
            switch (addr) {
                case uint _ when (addr >= 0x1F801C00 && addr <= 0x1F801D7F):

                    uint index = ((addr & 0xFF0) >> 4) - 0xC0;

                    switch (addr & 0xF) {
                        case 0x0: voices[index].volumeLeft.register = value; break;
                        case 0x2: voices[index].volumeRight.register = value; break;
                        case 0x4: voices[index].pitch = value; break;
                        case 0x6: voices[index].startAddress = value; break;
                        case 0x8: voices[index].adsr.lo = value; break;
                        case 0xA: voices[index].adsr.hi = value; break;
                        case 0xC: voices[index].adsrVolume = value; break;
                        case 0xE: voices[index].adpcmRepeatAddress = value; break;
                    }
                    break;

                case 0x1F801D80:
                    mainVolumeLeft = (short)value;
                    break;

                case 0x1F801D82:
                    mainVolumeRight = (short)value;
                    break;

                case 0x1F801D84:
                    reverbOutputVolumeLeft = (short)value;
                    break;

                case 0x1F801D86:
                    reverbOutputVolumeRight = (short)value;
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
                    //1F801D90h - Voice 0..23 Pitch Modulation Enable Flags(PMON)
                    pitchModulationEnableFlags = (pitchModulationEnableFlags & 0xFFFF0000) | value;
                    break;

                case 0x1F801D92:
                    pitchModulationEnableFlags = (pitchModulationEnableFlags & 0xFFFF) | (uint)(value << 16);
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
                    ramReverbStartAddress = (uint)(value << 3);
                    ramReverbInternalAddress = (uint)(value << 3);
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
                    writeRam(ramDataTransferAddressInternal, value);
                    ramDataTransferAddressInternal += 2;
                    break;

                case 0x1F801DAA:
                    control.register = value;

                    if (!control.spuEnabled) {
                        foreach (Voice v in voices) {
                            v.adsrPhase = Voice.Phase.Off;
                            v.adsrVolume = 0;
                        }
                    }

                    //Irq Flag is reseted on ack
                    if (!control.irq9Enabled)
                        status.irq9Flag = false;
                    
                    //Status 0..5 bits are the same as control
                    status.register &= 0xFFC0;
                    status.register |= (ushort)(value & 0x3F);
                    break;

                case 0x1F801DAC:
                    ramDataTransferControl = value;
                    break;

                case 0x1F801DAE:
                    status.register = value;
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

                // SPU Reverb Configuration Area
                case 0x1F801DC0:
                    dAPF1 = (uint)(value << 3);
                    break;

                case 0x1F801DC2:
                    dAPF2 = (uint)(value << 3);
                    break;

                case 0x1F801DC4:
                    vIIR = (short)value;
                    break;

                case 0x1F801DC6:
                    vCOMB1 = (short)value;
                    break;

                case 0x1F801DC8:
                    vCOMB2 = (short)value;
                    break;

                case 0x1F801DCA:
                    vCOMB3 = (short)value;
                    break;

                case 0x1F801DCC:
                    vCOMB4 = (short)value;
                    break;

                case 0x1F801DCE:
                    vWALL = (short)value;
                    break;

                case 0x1F801DD0:
                    vAPF1 = (short)value;
                    break;

                case 0x1F801DD2:
                    vAPF2 = (short)value;
                    break;

                case 0x1F801DD4:
                    mLSAME = (uint)(value << 3);
                    break;

                case 0x1F801DD6:
                    mRSAME = (uint)(value << 3);
                    break;

                case 0x1F801DD8:
                    mLCOMB1 = (uint)(value << 3);
                    break;

                case 0x1F801DDA:
                    mRCOMB1 = (uint)(value << 3);
                    break;

                case 0x1F801DDC:
                    mLCOMB2 = (uint)(value << 3);
                    break;

                case 0x1F801DDE:
                    mRCOMB2 = (uint)(value << 3);
                    break;

                case 0x1F801DE0:
                    dLSAME = (uint)(value << 3);
                    break;

                case 0x1F801DE2:
                    dRSAME = (uint)(value << 3);
                    break;

                case 0x1F801DE4:
                    mLDIFF = (uint)(value << 3);
                    break;

                case 0x1F801DE6:
                    mRDIFF = (uint)(value << 3);
                    break;

                case 0x1F801DE8:
                    mLCOMB3 = (uint)(value << 3);
                    break;

                case 0x1F801DEA:
                    mRCOMB3 = (uint)(value << 3);
                    break;

                case 0x1F801DEC:
                    mLCOMB4 = (uint)(value << 3);
                    break;

                case 0x1F801DEE:
                    mRCOMB4 = (uint)(value << 3);
                    break;

                case 0x1F801DF0:
                    dLDIFF = (uint)(value << 3);
                    break;

                case 0x1F801DF2:
                    dRDIFF = (uint)(value << 3);
                    break;

                case 0x1F801DF4:
                    mLAPF1 = (uint)(value << 3);
                    break;

                case 0x1F801DF6:
                    mRAPF1 = (uint)(value << 3);
                    break;

                case 0x1F801DF8:
                    mLAPF2 = (uint)(value << 3);
                    break;

                case 0x1F801DFA:
                    mRAPF2 = (uint)(value << 3);
                    break;

                case 0x1F801DFC:
                    vLIN = (short)value;
                    break;

                case 0x1F801DFE:
                    vRIN = (short)value;
                    break;

                default:
                    Console.WriteLine($"[SPU] Warning write:{addr:x8} value:{value:x8}");
                    writeRam(addr, value);
                    break;
            }

        }

        internal ushort load(uint addr) {
            switch (addr) {
                case uint _ when (addr >= 0x1F801C00 && addr <= 0x1F801D7F):

                    uint index = ((addr & 0xFF0) >> 4) - 0xC0;

                    return (addr & 0xF) switch {
                        0x0 => voices[index].volumeLeft.register,
                        0x2 => voices[index].volumeRight.register,
                        0x4 => voices[index].pitch,
                        0x6 => voices[index].startAddress,
                        0x8 => voices[index].adsr.lo,
                        0xA => voices[index].adsr.hi,
                        0xC => voices[index].adsrVolume,
                        0xE => voices[index].adpcmRepeatAddress,
                        _ => 0xFFFF,
                    };

                case 0x1F801D80:
                    return (ushort)mainVolumeLeft;

                case 0x1F801D82:
                    return (ushort)mainVolumeRight;

                case 0x1F801D84:
                    return (ushort)reverbOutputVolumeLeft;

                case 0x1F801D86:
                    return (ushort)reverbOutputVolumeRight;

                case 0x1F801D88:
                    return (ushort)keyOn;

                case 0x1F801D8A:
                    return (ushort)(keyOn >> 16);

                case 0x1F801D8C:
                    return (ushort)keyOff;

                case 0x1F801D8E:
                    return (ushort)(keyOff >> 16);

                case 0x1F801D90:
                    return (ushort)pitchModulationEnableFlags;

                case 0x1F801D92:
                    return (ushort)(pitchModulationEnableFlags >> 16);

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
                    return (ushort)(ramReverbStartAddress >> 3);

                case 0x1F801DA4:
                    return ramIrqAddress;

                case 0x1F801DA6:
                    return ramDataTransferAddress;

                case 0x1F801DA8:
                    return ramDataTransferFifo;

                case 0x1F801DAA:
                    return control.register;

                case 0x1F801DAC:
                    return ramDataTransferControl;

                case 0x1F801DAE:
                    return status.register;

                case 0x1F801DB0:
                    return cdVolumeLeft;

                case 0x1F801DB2:
                    return cdVolumeRight;

                case 0x1F801DB4:
                    return externVolumeLeft;

                case 0x1F801DB6:
                    return externVolumeRight;

                case 0x1F801DB8:
                    return currentVolumeLeft;

                case 0x1F801DBA:
                    return currentVolumeRight;

                case 0x1F801DBC:
                    return (ushort)unknownBC;

                case 0x1F801DBE:
                    return (ushort)(unknownBC >> 16);

                // SPU Reverb Configuration Area
                case 0x1F801DC0:
                    return (ushort)(dAPF1 >> 3);

                case 0x1F801DC2:
                    return (ushort)(dAPF2 >> 3);

                case 0x1F801DC4:
                    return (ushort)vIIR;

                case 0x1F801DC6:
                    return (ushort)vCOMB1;

                case 0x1F801DC8:
                    return (ushort)vCOMB2;

                case 0x1F801DCA:
                    return (ushort)vCOMB3;

                case 0x1F801DCC:
                    return (ushort)vCOMB4;

                case 0x1F801DCE:
                    return (ushort)vWALL;

                case 0x1F801DD0:
                    return (ushort)vAPF1;

                case 0x1F801DD2:
                    return (ushort)vAPF2;

                case 0x1F801DD4:
                    return (ushort)(mLSAME >> 3);

                case 0x1F801DD6:
                    return (ushort)(mRSAME >> 3);

                case 0x1F801DD8:
                    return (ushort)(mLCOMB1 >> 3);

                case 0x1F801DDA:
                    return (ushort)(mRCOMB1 >> 3);

                case 0x1F801DDC:
                    return (ushort)(mLCOMB2 >> 3);

                case 0x1F801DDE:
                    return (ushort)(mRCOMB2 >> 3);

                case 0x1F801DE0:
                    return (ushort)(dLSAME >> 3);

                case 0x1F801DE2:
                    return (ushort)(dRSAME >> 3);

                case 0x1F801DE4:
                    return (ushort)(mLDIFF >> 3);

                case 0x1F801DE6:
                    return (ushort)(mRDIFF >> 3);

                case 0x1F801DE8:
                    return (ushort)(mLCOMB3 >> 3);

                case 0x1F801DEA:
                    return (ushort)(mRCOMB3 >> 3);

                case 0x1F801DEC:
                    return (ushort)(mLCOMB4 >> 3);

                case 0x1F801DEE:
                    return (ushort)(mRCOMB4 >> 3);

                case 0x1F801DF0:
                    return (ushort)(dLDIFF >> 3);

                case 0x1F801DF2:
                    return (ushort)(dRDIFF >> 3);

                case 0x1F801DF4:
                    return (ushort)(mLAPF1 >> 3);

                case 0x1F801DF6:
                    return (ushort)(mRAPF1 >> 3);

                case 0x1F801DF8:
                    return (ushort)(mLAPF2 >> 3);

                case 0x1F801DFA:
                    return (ushort)(mRAPF2 >> 3);

                case 0x1F801DFC:
                    return (ushort)vLIN;

                case 0x1F801DFE:
                    return (ushort)vRIN;

                default:
                    return (ushort)loadRam(addr);
            }
        }

        internal void pushCdBufferSamples(byte[] decodedXaAdpcm) {
            cdBuffer.fillWith(decodedXaAdpcm);
        }

        private int counter = 0;
        private const int CYCLES_PER_SAMPLE = 0x300; //33868800 / 44100hz
        private int reverbCounter = 0;
        public bool tick(int cycles) {
            bool edgeTrigger = false;
            counter += cycles;

            if (counter < CYCLES_PER_SAMPLE) {
                return false;
            }
            counter -= CYCLES_PER_SAMPLE;

            int sumLeft = 0;
            int sumRight = 0;

            int sumLeftReverb = 0;
            int sumRightReverb = 0;

            uint edgeKeyOn = keyOn;
            uint edgeKeyOff = keyOff;
            keyOn = 0;
            keyOff = 0;

            tickNoiseGenerator();

            for (int i = 0; i < voices.Length; i++) {
                Voice v = voices[i];

                //keyOn and KeyOff are edge triggered on 0 to 1
                if ((edgeKeyOff & (0x1 << i)) != 0) {
                    v.keyOff();
                }

                if ((edgeKeyOn & (0x1 << i)) != 0) {
                    endx &= ~(uint)(0x1 << i);
                    v.keyOn();
                }

                if (v.adsrPhase == Voice.Phase.Off) {
                    v.latest = 0;
                    continue;
                }

                short sample;
                if ((channelNoiseMode & (0x1 << i)) != 0) {
                    //Generated by tickNoiseGenerator
                    sample = (short)noiseLevel;
                } else {
                    sample = sampleVoice(i);
                    //Read irqAddress Irq
                    edgeTrigger |= control.irq9Enabled && v.readRamIrq;
                    v.readRamIrq = false;
                }

                //Handle ADSR Envelope
                sample = (short)((sample * v.adsrVolume) >> 15);
                v.tickAdsr(i);

                //Save sample for possible pitch modulation
                v.latest = sample;

                //Sum each voice sample
                sumLeft += (sample * v.processVolume(v.volumeLeft)) >> 15;
                sumRight += (sample * v.processVolume(v.volumeRight)) >> 15;

                if((channelReverbMode & (0x1 << i)) != 0) {
                    sumLeftReverb += (sample * v.processVolume(v.volumeLeft)) >> 15;
                    sumRightReverb += (sample * v.processVolume(v.volumeRight)) >> 15;
                }
            }

            if (!control.spuUnmuted) { //todo merge this on the for voice loop
                //On mute the spu still ticks but output is 0 for voices (not for cdInput)
                sumLeft = 0;
                sumRight = 0;
            }

            //Merge in CD audio (CDDA or XA)
            short cdL = 0;
            short cdR = 0;

            // CD Audio samples are always "consumed" even if cdAudio is disabled and will end
            // on the capture buffer area of ram, this is needed for VibRibbon to be playable
            if(cdBuffer.hasSamples()) {
                cdL = cdBuffer.readShort();
                cdR = cdBuffer.readShort();
            }

            if(control.cdAudioEnabled) { //Be sure theres something on the queue...
                //Apply Spu Cd In (CDDA/XA) Volume
                cdL = (short)((cdL * cdVolumeLeft) >> 15);
                cdR = (short)((cdR * cdVolumeRight) >> 15);

                sumLeft += cdL;
                sumRight += cdR;

                if(control.cdAudioReverb) {
                    sumLeftReverb += cdL;
                    sumRightReverb += cdR;
                }
            }

            if (reverbCounter == 0) {
                var (reverbL, reverbR) = processReverb(sumLeftReverb, sumRightReverb);

                sumLeft += reverbL;
                sumRight += reverbR;
            }

            // reverb is on a 22050hz clock
            reverbCounter = (reverbCounter + 1) & 0x1;

            //Write to capture buffers and check ram irq
            edgeTrigger |= handleCaptureBuffer(0 * 1024 + captureBufferPos, cdL);
            edgeTrigger |= handleCaptureBuffer(1 * 1024 + captureBufferPos, cdR);
            edgeTrigger |= handleCaptureBuffer(2 * 1024 + captureBufferPos, voices[1].latest);
            edgeTrigger |= handleCaptureBuffer(3 * 1024 + captureBufferPos, voices[3].latest);
            captureBufferPos = (captureBufferPos + 2) & 0x3FF;

            //Clamp sum
            sumLeft = (Math.Clamp(sumLeft, -0x8000, 0x7FFF) * mainVolumeLeft) >> 15;
            sumRight = (Math.Clamp(sumRight, -0x8000, 0x7FFF) * mainVolumeRight) >> 15;

            //Add to samples bytes to output array
            spuOutput[spuOutputPointer++] = (byte)sumLeft;
            spuOutput[spuOutputPointer++] = (byte)(sumLeft >> 8);
            spuOutput[spuOutputPointer++] = (byte)sumRight;
            spuOutput[spuOutputPointer++] = (byte)(sumRight >> 8);

            if (spuOutputPointer >= 2048) {
                window.Play(spuOutput);
                spuOutputPointer = 0;
            }

            if (control.irq9Enabled && edgeTrigger) {
                status.irq9Flag = true;
            }
            return control.irq9Enabled && edgeTrigger;
        }

        private bool handleCaptureBuffer(int address, short sample) {
            writeRam((uint)address, sample);
            return address >> 3 == ramIrqAddress;
        }

        //Wait(1 cycle); at 44.1kHz clock
        //Timer=Timer-NoiseStep  ;subtract Step(4..7)
        //ParityBit = NoiseLevel.Bit15 xor Bit12 xor Bit11 xor Bit10 xor 1
        //IF Timer<0 then NoiseLevel = NoiseLevel * 2 + ParityBit
        //IF Timer<0 then Timer = Timer + (20000h SHR NoiseShift); reload timer once
        //IF Timer<0 then Timer = Timer + (20000h SHR NoiseShift); reload again if needed
        int noiseTimer;
        int noiseLevel;
        private void tickNoiseGenerator() {
            int noiseStep = control.noiseFrequencyStep + 4;
            int noiseShift = control.noiseFrequencyShift;

            noiseTimer -= noiseStep;
            int parityBit = ((noiseLevel >> 15) & 0x1) ^ ((noiseLevel >> 12) & 0x1) ^ ((noiseLevel >> 11) & 0x1) ^ ((noiseLevel >> 10) & 0x1) ^ 1;
            if (noiseTimer < 0) noiseLevel = noiseLevel * 2 + parityBit;
            if (noiseTimer < 0) noiseTimer += 0x20000 >> noiseShift;
            if (noiseTimer < 0) noiseTimer += 0x20000 >> noiseShift;
        }

        public unsafe short sampleVoice(int v) {
            Voice voice = voices[v];

            //Decode samples if its empty / next block
            if (!voice.hasSamples) {
                voice.decodeSamples(ram, ramIrqAddress);
                voice.hasSamples = true;

                byte flags = voice.spuAdpcm[1];
                bool loopStart = (flags & 0x4) != 0;

                if (loopStart) voice.adpcmRepeatAddress = voice.currentAddress;
            }

            //Get indexs for gauss interpolation
            uint interpolationIndex = voice.counter.interpolationIndex;
            uint sampleIndex = voice.counter.currentSampleIndex;

            //Interpolate latest samples
            //this is why the latest 3 samples from the last block are saved
            int interpolated;
            interpolated  = gaussTable[0x0FF - interpolationIndex] * voice.decodedSamples[sampleIndex + 0];
            interpolated += gaussTable[0x1FF - interpolationIndex] * voice.decodedSamples[sampleIndex + 1];
            interpolated += gaussTable[0x100 + interpolationIndex] * voice.decodedSamples[sampleIndex + 2];
            interpolated += gaussTable[0x000 + interpolationIndex] * voice.decodedSamples[sampleIndex + 3];
            interpolated >>= 15;

            //Pitch modulation: Starts at voice 1 as it needs the last voice
            int step = voice.pitch;
            if (((pitchModulationEnableFlags & (0x1 << v)) != 0) && v > 0) {
                int factor = voices[v - 1].latest + 0x8000; //From previous voice
                step = (step * factor) >> 15;
                step &= 0xFFFF;
            }
            if (step > 0x3FFF) step = 0x4000;

            //Console.WriteLine("step u " + ((uint)step).ToString("x8") + "step i" + ((int)step).ToString("x8") + " " + voice.counter.register.ToString("x8"));
            voice.counter.register += (ushort)step;

            if (voice.counter.currentSampleIndex >= 28) {
                //Beyond the current adpcm sample block prepare to decode next
                voice.counter.currentSampleIndex -= 28;
                voice.currentAddress += 2;
                voice.hasSamples = false;

                //LoopEnd and LoopRepeat flags are set after the "current block" set them as it's finished
                byte flags = voice.spuAdpcm[1];
                bool loopEnd = (flags & 0x1) != 0;
                bool loopRepeat = (flags & 0x2) != 0;

                if (loopEnd) {
                    endx |= (uint)(0x1 << v);
                    if(loopRepeat) {
                        voice.currentAddress = voice.adpcmRepeatAddress;
                    } else {
                        voice.adsrPhase = Voice.Phase.Off;
                        voice.adsrVolume = 0;
                    }
                }
            }

            return (short)interpolated;
        }

        public (short, short) processReverb(int lInput, int rInput) {
            // Input from mixer
            int Lin = (vLIN * lInput) >> 15;
            int Rin = (vRIN * rInput) >> 15;

            // Same side reflection LtoL and RtoR
            short mlSame = saturateSample(Lin + ((loadReverb(dLSAME) * vWALL) >> 15) - ((loadReverb(mLSAME - 2) * vIIR) >> 15) + loadReverb(mLSAME - 2));
            short mrSame = saturateSample(Rin + ((loadReverb(dRSAME) * vWALL) >> 15) - ((loadReverb(mRSAME - 2) * vIIR) >> 15) + loadReverb(mRSAME - 2));
            writeReverb(mLSAME, mlSame);
            writeReverb(mRSAME, mrSame);

            // Different side reflection LtoR and RtoL
            short mldiff = saturateSample(Lin + ((loadReverb(dRDIFF) * vWALL) >> 15) - ((loadReverb(mLDIFF - 2) * vIIR) >> 15) + loadReverb(mLDIFF - 2));
            short mrdiff = saturateSample(Rin + ((loadReverb(dLDIFF) * vWALL) >> 15) - ((loadReverb(mRDIFF - 2) * vIIR) >> 15) + loadReverb(mRDIFF - 2));
            writeReverb(mLDIFF, mldiff);
            writeReverb(mRDIFF, mrdiff);

            // Early echo (comb filter with input from buffer)
            short l = saturateSample((vCOMB1 * loadReverb(mLCOMB1) >> 15) + (vCOMB2 * loadReverb(mLCOMB2) >> 15) + (vCOMB3 * loadReverb(mLCOMB3) >> 15) + (vCOMB4 * loadReverb(mLCOMB4) >> 15));
            short r = saturateSample((vCOMB1 * loadReverb(mRCOMB1) >> 15) + (vCOMB2 * loadReverb(mRCOMB2) >> 15) + (vCOMB3 * loadReverb(mRCOMB3) >> 15) + (vCOMB4 * loadReverb(mRCOMB4) >> 15));

            // Late reverb APF1 (All pass filter 1 with input from COMB)
            l = saturateSample(l - saturateSample((vAPF1 * loadReverb(mLAPF1 - dAPF1)) >> 15));
            r = saturateSample(r - saturateSample((vAPF1 * loadReverb(mRAPF1 - dAPF1)) >> 15));

            writeReverb(mLAPF1, l);
            writeReverb(mRAPF1, r);
            
            l = saturateSample((l * vAPF1 >> 15) + loadReverb(mLAPF1 - dAPF1));
            r = saturateSample((r * vAPF1 >> 15) + loadReverb(mRAPF1 - dAPF1));

            // Late reverb APF2 (All pass filter 2 with input from APF1)
            l = saturateSample(l - saturateSample((vAPF2 * loadReverb(mLAPF2 - dAPF2)) >> 15));
            r = saturateSample(r - saturateSample((vAPF2 * loadReverb(mRAPF2 - dAPF2)) >> 15));
            
            writeReverb(mLAPF2, l);
            writeReverb(mRAPF2, r);

            l = saturateSample((l * vAPF2 >> 15) + loadReverb(mLAPF2 - dAPF2));
            r = saturateSample((r * vAPF2 >> 15) + loadReverb(mRAPF2 - dAPF2));

            // Output to mixer (output volume multiplied with input from APF2)
            l = saturateSample(l * reverbOutputVolumeLeft >> 15);
            r = saturateSample(r * reverbOutputVolumeRight >> 15);

            // Saturate address
            ramReverbInternalAddress = Math.Max(ramReverbStartAddress, (ramReverbInternalAddress + 2) & 0x7_FFFE);

            return (l, r);
        }

        public short saturateSample(int sample) {
            if(sample < -0x8000) {
                return -0x8000;
            }

            if(sample > 0x7FFF) {
                return 0x7FFF;
            }

            return (short)sample;
        }


        public unsafe Span<uint> processDmaLoad(int size) { //todo trigger interrupt
            Span<byte> dma = new Span<byte>(ram, 1024*512).Slice((int)ramDataTransferAddressInternal, size * 4);

            //ramDataTransferAddressInternal already is >> 3 while ramIrqAddress is set as ushort
            //so check if it's in the size range and trigger int
            uint ramIrqAddress32 = (uint)ramIrqAddress << 3;
            if (ramIrqAddress32 > ramDataTransferAddressInternal && ramIrqAddress32 < (ramDataTransferAddressInternal + (size * 4))) {
                if(control.irq9Enabled) {
                    status.irq9Flag = true;
                    interruptController.set(Interrupt.SPU);
                }
            }

            ramDataTransferAddressInternal += (uint)(size * 4);

            return MemoryMarshal.Cast<byte, uint>(dma);
        }

        public unsafe void processDmaWrite(Span<uint> dma) { //todo trigger interrupt
            //Tekken 3 and FF8 overflows SPU Ram
            int size = dma.Length * 4;
            int destAddress = (int)ramDataTransferAddressInternal + size - 1;

            Span<byte> dmaSpan = MemoryMarshal.Cast<uint, byte>(dma);

            Span<byte> ramStartSpan = new Span<byte>(ram, 1024 * 512);
            Span<byte> ramDestSpan = ramStartSpan.Slice((int)ramDataTransferAddressInternal);

            //ramDataTransferAddressInternal already is >> 3 while ramIrqAddress is set as ushort
            //so check if it's in the size range and trigger int
            uint ramIrqAddress32 = (uint)ramIrqAddress << 3;
            if (ramIrqAddress32 > ramDataTransferAddressInternal && ramIrqAddress32 < (ramDataTransferAddressInternal + size)) {
                if (control.irq9Enabled) {
                    status.irq9Flag = true;
                    interruptController.set(Interrupt.SPU);
                }
            }

            if (destAddress <= 0x7FFFF) {
                dmaSpan.CopyTo(ramDestSpan);
            } else {
                int overflow = destAddress - 0x7FFFF;

                Span<byte> firstSlice = dmaSpan.Slice(0, dmaSpan.Length - overflow);
                Span<byte> overflowSpan = dmaSpan.Slice(dmaSpan.Length - overflow);
        
                firstSlice.CopyTo(ramDestSpan);
                overflowSpan.CopyTo(ramStartSpan);
            }
        
            ramDataTransferAddressInternal = (uint)((ramDataTransferAddressInternal + size) & 0x7FFFF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void writeRam<T>(uint addr, T value) where T : unmanaged {
            *(T*)(ram + addr) = value;
        }

        private unsafe short loadRam(uint addr) {
            //Console.WriteLine($"loadSPURam from addr {(addr & 0x3_FFFF):x8}");
            return *(short*)(ram + (addr & 0x7_FFFF));
        }

        private unsafe void writeReverb(uint addr, short value) {
            if (!control.reverbMasterEnabled) return;

            uint relative = (addr + ramReverbInternalAddress - ramReverbStartAddress) % (0x8_0000 - ramReverbStartAddress);
            uint wrapped = (ramReverbStartAddress + relative) & 0x7_FFFE;

            *(short*)(ram + wrapped) = value;
        }

        private unsafe short loadReverb(uint addr) {
            uint relative = (addr + ramReverbInternalAddress - ramReverbStartAddress) % (0x8_0000 - ramReverbStartAddress);
            uint wrapped = (ramReverbStartAddress + relative) & 0x7_FFFE;

            return *(short*)(ram + wrapped);
        }

    }
}
