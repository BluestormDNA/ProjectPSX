using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectPSX.Devices.Spu {
    public class Voice {
        public ushort volumeLeft;           //0
        public ushort volumeRight;          //2
        public ushort pitch;                //4
        public ushort startAddress;         //6
        public ushort currentAddress;       //6 Internal
        public ushort adsrLo;               //8
        public ushort adsrHi;               //A
        public ushort adsrVolume;           //C
        public ushort adpcmRepeatAddress;   //E

        public Phase adsrPhase;

        public short old;
        public short older;
        public short oldest;


        public Voice() {
            adsrPhase = Phase.Off;
        }

        public void keyOn() {
            currentAddress = startAddress;
            adsrPhase = Phase.Attack;
        }

        public void keyOff() {
            adsrPhase = Phase.Release;
        }

        public enum Phase {
            Off,
            Attack,
            Decay,
            Sustain,
            Release
        }
    }
}
