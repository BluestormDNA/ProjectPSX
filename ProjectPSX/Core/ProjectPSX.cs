using System.Threading.Tasks;

namespace ProjectPSX {
    class ProjectPSX {
        private CPU cpu;
        private BUS bus;

        public ProjectPSX(Window window) {
            cpu = new CPU();
            bus = new BUS();

            bus.loadBios();

            bus.setWindow(window);
        }

        public void POWER_ON() {
            Task t = Task.Factory.StartNew(EXECUTE, TaskCreationOptions.LongRunning);
        }

        public void EXECUTE() {
            while (true) {
                cpu.Run(bus);
                bus.tick(2); //2 ticks per opcode
                cpu.handleInterrupts(bus);
            }
        }

    }
}
