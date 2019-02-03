using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectPSX.Devices {
    public class DMA : Device {

        private uint CONTROL;
        private uint INTERRUPT;

        public DMA() {
            registers = new byte[0x80];
            memOffset = 0x1F801080;

            write32(0x1F8010F0, 0x07654321);
        }

        public new void write32(uint addr, uint value) {
            base.write32(addr, value);
            Console.WriteLine("Write32 on DMA Address: {0}  Value: {1}", addr.ToString("x8"), value.ToString("x8"));
        }

    }
}
