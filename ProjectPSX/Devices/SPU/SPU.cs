
using System;
using ProjectPSX.Devices.Spu;

namespace ProjectPSX.Devices {
    public class SPU {

        private byte[] RAM = new byte[512 * 1024];
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
        private uint channelEnabled;

        private ushort unknownA0;

        private ushort ramReverbStartAddress;
        private ushort ramIrqAddress;
        private ushort ramDataTransferAddress;
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

        public SPU() {
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
                    channelEnabled = (channelEnabled & 0xFFFF0000) | value;
                    break;

                case 0x1F801D9E:
                    channelEnabled = (channelEnabled & 0xFFFF) | (uint)(value << 16);
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
                    break;

                case 0x1F801DA8:
                    ramDataTransferFifo = value;
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
                    unknownBC = (channelEnabled & 0xFFFF0000) | value;
                    break;

                case 0x1F801DBE:
                    unknownBC = (channelEnabled & 0xFFFF) | (uint)(value << 16);
                    break;
            }

        }

        internal ushort load(Width width, uint addr) {
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
                    return (ushort)channelEnabled;

                case 0x1F801D9E:
                    return (ushort)(channelEnabled >> 16);

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

        public bool tick(int cycles) {

            return false;
        }

        public void processDma(uint[] load) {
            //Console.WriteLine($"[SPU] DMA Unprocessed length: {load.Length}");
        }
    }
}
