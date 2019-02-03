using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectPSX {
    class ProjectPSX {
        private CPU cpu;
        private BUS mmu;


        public ProjectPSX() {
            POWER_ON();
        }

        public void POWER_ON() {
            cpu = new CPU();
            mmu = new BUS();

            mmu.loadBios();

            Task t = Task.Factory.StartNew(EXECUTE, TaskCreationOptions.LongRunning);
        }

        public void EXECUTE() {
            while (true) {
                cpu.Run(mmu);
            }
        }

    }
}
