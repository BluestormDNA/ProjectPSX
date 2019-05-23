using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectPSX {
    class ProjectPSX {
        const int PSX_MHZ = 33868800;
        private CPU cpu;
        private BUS bus;

        private Window window;

        private long cycle;
        private long counter;
        Stopwatch watch = new Stopwatch();

        public ProjectPSX(Window window) {
            this.window = window;

            bus = new BUS();
            cpu = new CPU(bus);

            bus.loadBios();

            bus.setWindow(window);
        }

        public void POWER_ON() {
            Task t = Task.Factory.StartNew(EXECUTE, TaskCreationOptions.LongRunning);
        }

        public void EXECUTE() {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;

            try {
                while (true) {
                    if(cycle == 1) {
                        watch.Restart();
                    }

                    for(int i = 0; i < 50; i++) {
                        cpu.Run();
                        cpu.handleInterrupts();
                        counter++;
                    }

                    bus.tick(150); //2 ticks per opcode



                    if (counter >= PSX_MHZ) {
                        counter = 0;
                        cycle = 0;
                        window.Text = "ProjectPSX | Fps: " + (60 / ((float)watch.ElapsedMilliseconds / 1000)).ToString();
                    }
                    cycle++;
                }
            } catch(Exception e) {
                Console.WriteLine(e.ToString());
            }
        }

    }
}
