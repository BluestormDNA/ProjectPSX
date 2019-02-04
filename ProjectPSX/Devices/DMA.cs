using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectPSX.Devices {
    public class DMA : Device {

        private uint CONTROL { get { return load(Width.WORD, 0x1F8010F0); } set { write(Width.WORD, 0x1F8010F0, value); } }
        private uint INTERRUPT { get { return load(Width.WORD, 0x1F8010F4); } set { write(Width.WORD, 0x1F8010F4, value); } }

        public DMA() {
            registers = new byte[0x80];
            memOffset = 0x1F801080;

            CONTROL = 0x07654321;
        }

        public new void write(Width w, uint addr, uint value) {
            base.write(w, addr, value);
            Console.WriteLine("Write on DMA Address: {0}  Value: {1}", addr.ToString("x8"), value.ToString("x8"));
        }

        public new uint load(Width w, uint addr) {
            Console.WriteLine("Load on DMA Address: {0}  Value: {1}", addr.ToString("x8"), base.load(w, addr).ToString("x8"));
            return base.load(w, addr);
        }

    }
}
