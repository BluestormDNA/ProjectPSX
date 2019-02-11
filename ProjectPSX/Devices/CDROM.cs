using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectPSX.Devices {
    class CDROM : Device {

        public CDROM() {
            
        }

        public new uint load(Width w, uint addr) {
            return 0;
        }

        public new void write(Width w, uint addr, uint value) {
        }
    }
}

