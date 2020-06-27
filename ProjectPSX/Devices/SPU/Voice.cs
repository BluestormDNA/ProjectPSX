using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectPSX.Devices.Spu {
    public class Voice {
        public ushort volumeLeft;           //0
        public ushort volumeRight;          //2
        public ushort pitch;                //4
        public ushort startAddress;         //6
        public ushort adsrLo;               //8
        public ushort adsrHi;               //A
        public ushort adsrVolume;           //C
        public ushort adpcmRepeatAddress;   //E

        public Voice() {

        }
    }
}
