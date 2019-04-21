using System;
using System.Threading.Tasks;

namespace ProjectPSX {
    class Program {
        private CPU cpu;
        private BUS bus;

        static void Main() {
            Program p = new Program();
            p.POWER_ON();
        }

        public void POWER_ON() {
            cpu = new CPU();
            bus = new BUS();

            bus.loadBios();

            while (true) {
                cpu.Run(bus);
                bus.tick(2); //2 ticks per opcode
            }

        }

    }
}

