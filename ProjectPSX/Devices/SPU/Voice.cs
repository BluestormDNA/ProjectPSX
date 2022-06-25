using System;

namespace ProjectPSX.Devices.Spu {
    public class Voice {

        private static ReadOnlySpan<sbyte> positiveXaAdpcmTable => new sbyte[] { 0, 60, 115, 98, 122 };
        private static ReadOnlySpan<sbyte> negativeXaAdpcmTable => new sbyte[] { 0, 0, -52, -55, -60 };

        public struct Volume {
            public ushort register;
            public bool isSweepMode => ((register >> 15) & 0x1) != 0;
            public short fixedVolume => (short)(register << 1);
            public bool isSweepExponential => ((register >> 14) & 0x1) != 0;
            public bool isSweepDirectionDecrease => ((register >> 13) & 0x1) != 0;
            public bool isSweepPhaseNegative => ((register >> 12) & 0x1) != 0;
            public int sweepShift => (register >> 2) & 0x1F;
            public int sweepStep => register & 0x3;
        }
        public Volume volumeLeft;           //0
        public Volume volumeRight;          //2

        public ushort pitch;                //4
        public ushort startAddress;         //6
        public ushort currentAddress;       //6 Internal

        public struct ADSR {
            public ushort lo;               //8
            public ushort hi;               //A
            public bool isAttackModeExponential => ((lo >> 15) & 0x1) != 0;
            public int attackShift => (lo >> 10) & 0x1F;
            public int attackStep => (lo >> 8) & 0x3; //"+7,+6,+5,+4"
            public int decayShift => (lo >> 4) & 0xF;
            public int sustainLevel => lo & 0xF; //Level=(N+1)*800h

            public bool isSustainModeExponential => ((hi >> 15) & 0x1) != 0;
            public bool isSustainDirectionDecrease => ((hi >> 14) & 0x1) != 0;
            public int sustainShift => (hi >> 8) & 0x1F;
            public int sustainStep => (hi >> 6) & 0x3;
            public bool isReleaseModeExponential => ((hi >> 5) & 0x1) != 0;
            public int releaseShift => hi & 0x1F;
        }
        public ADSR adsr;

        public ushort adsrVolume;           //C
        public ushort adpcmRepeatAddress;   //E

        public struct Counter {            //internal
            public uint register;
            public uint currentSampleIndex {
                get { return (register >> 12) & 0x1F; }
                set {
                    register = (ushort)(register &= 0xFFF);
                    register |= value << 12;
                }
            }

            public uint interpolationIndex => (register >> 3) & 0xFF;
        }
        public Counter counter;

        public Phase adsrPhase;

        public short old;
        public short older;

        public short latest;

        public bool hasSamples;

        public bool readRamIrq;

        public Voice() {
            adsrPhase = Phase.Off;
        }

        public void keyOn() {
            hasSamples = false;
            old = 0;
            older = 0;
            currentAddress = startAddress;
            adsrCounter = 0;
            adsrVolume = 0;
            adsrPhase = Phase.Attack;
        }

        public void keyOff() {
            adsrCounter = 0;
            adsrPhase = Phase.Release;
        }

        public enum Phase {
            Attack,
            Decay,
            Sustain,
            Release,
            Off,
        }

        public byte[] spuAdpcm = new byte[16];
        public short[] decodedSamples = new short[31]; //28 samples from current block + 3 to make room for interpolation
        internal unsafe void decodeSamples(byte* ram, ushort ramIrqAddress) {
            //save the last 3 samples from the last decoded block
            //this are needed for interpolation in case the voice.counter.currentSampleIndex is 0 1 or 2
            decodedSamples[2] = decodedSamples[decodedSamples.Length - 1];
            decodedSamples[1] = decodedSamples[decodedSamples.Length - 2];
            decodedSamples[0] = decodedSamples[decodedSamples.Length - 3];

            new Span<byte>(ram, 1024 * 512).Slice(currentAddress * 8, 16).CopyTo(spuAdpcm);

            //ramIrqAddress is >> 8 so we only need to check for currentAddress and + 1
            readRamIrq |= currentAddress == ramIrqAddress || currentAddress + 1 == ramIrqAddress;

            int headerShift = spuAdpcm[0] & 0x0F;
            if (headerShift > 12) headerShift = 9;
            int shift = 12 - headerShift;

            int filter = (spuAdpcm[0] & 0x70) >> 4; //filter on SPU adpcm is 0-4 vs XA wich is 0-3
            if (filter > 4) filter = 4; //Crash Bandicoot sets this to 7 at the end of the first level and overflows the filter

            int f0 = positiveXaAdpcmTable[filter];
            int f1 = negativeXaAdpcmTable[filter];

            //Actual ADPCM decoding is the same as on XA but the layout here is sequencial by nibble where on XA in grouped by nibble line
            int position = 2; //skip shift and flags
            int nibble = 1;
            for (int i = 0; i < 28; i++) {
                nibble = (nibble + 1) & 0x1;

                int t = signed4bit((byte)((spuAdpcm[position] >> (nibble * 4)) & 0x0F));
                int s = (t << shift) + ((old * f0 + older * f1 + 32) / 64);
                short sample = (short)Math.Clamp(s, -0x8000, 0x7FFF);

                decodedSamples[3 + i] = sample;

                older = old;
                old = sample;

                position += nibble;
            }
        }

        public static int signed4bit(byte value) {
            return (value << 28) >> 28;
        }

        internal short processVolume(Volume volume) {
            if (!volume.isSweepMode) {
                return volume.fixedVolume;
            } else {
                return 0x7FFF; //todo handle sweep mode volume envelope
            }
        }

        int adsrCounter;
        internal void tickAdsr(int v) {
            if (adsrPhase == Phase.Off) {
                adsrVolume = 0;
                return;
            }

            int adsrTarget;
            int adsrShift;
            int adsrStep;
            bool isDecreasing;
            bool isExponential;

            //Todo move out of tick the actual change of phase
            switch (adsrPhase) {
                case Phase.Attack:
                    adsrTarget = 0x7FFF;
                    adsrShift = adsr.attackShift;
                    adsrStep = 7 - adsr.attackStep; // reg is 0-3 but values are "+7,+6,+5,+4"
                    isDecreasing = false; // Allways increase till 0x7FFF
                    isExponential = adsr.isAttackModeExponential;
                    break;
                case Phase.Decay:
                    adsrTarget = (adsr.sustainLevel + 1) * 0x800;
                    adsrShift = adsr.decayShift;
                    adsrStep = -8;
                    isDecreasing = true; // Allways decreases (till target)
                    isExponential = true; // Allways exponential
                    break;
                case Phase.Sustain:
                    adsrTarget = 0;
                    adsrShift = adsr.sustainShift;
                    adsrStep = adsr.isSustainDirectionDecrease? -8 + adsr.sustainStep: 7 - adsr.sustainStep;
                    isDecreasing = adsr.isSustainDirectionDecrease; //till keyoff
                    isExponential = adsr.isSustainModeExponential;
                    break;
                case Phase.Release:
                    adsrTarget = 0;
                    adsrShift = adsr.releaseShift;
                    adsrStep = -8;
                    isDecreasing = true; // Allways decrease till 0
                    isExponential = adsr.isReleaseModeExponential;
                    break;
                default:
                    adsrTarget = 0;
                    adsrShift = 0;
                    adsrStep = 0;
                    isDecreasing = false;
                    isExponential = false;
                    break;
            }

            //Envelope Operation depending on Shift/Step/Mode/Direction
            //AdsrCycles = 1 SHL Max(0, ShiftValue-11)
            //AdsrStep = StepValue SHL Max(0,11-ShiftValue)
            //IF exponential AND increase AND AdsrLevel>6000h THEN AdsrCycles=AdsrCycles*4    
            //IF exponential AND decrease THEN AdsrStep = AdsrStep * AdsrLevel / 8000h
            //Wait(AdsrCycles); cycles counted at 44.1kHz clock
            //AdsrLevel=AdsrLevel+AdsrStep  ;saturated to 0..+7FFFh

            if (adsrCounter > 0) { adsrCounter--; return; }

            int envelopeCycles = 1 << Math.Max(0, adsrShift - 11);
            int envelopeStep = adsrStep << Math.Max(0, 11 - adsrShift);
            if(isExponential && !isDecreasing && adsrVolume > 0x6000) { envelopeCycles *= 4; }
            if(isExponential && isDecreasing) { envelopeStep = (envelopeStep * adsrVolume) >> 15; }

            adsrVolume = (ushort)Math.Clamp(adsrVolume + envelopeStep, 0, 0x7FFF);
            adsrCounter = envelopeCycles;

            bool nextPhase = isDecreasing ? (adsrVolume <= adsrTarget) : (adsrVolume >= adsrTarget);
            if (nextPhase && adsrPhase != Phase.Sustain) {
                adsrPhase++;
                adsrCounter = 0;
            };
        }
    }
}
