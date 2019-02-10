using System;
using System.Threading.Tasks;

namespace ProjectPSX {
    class Program {
        private CPU cpu;
        private BUS mmu;

        static void Main() {
            Program p = new Program();
            p.POWER_ON();
        }

        public void POWER_ON() {
            cpu = new CPU();
            mmu = new BUS();

            mmu.loadBios();

            while (true) {
                cpu.Run(mmu);
            }

        }

    }
}

