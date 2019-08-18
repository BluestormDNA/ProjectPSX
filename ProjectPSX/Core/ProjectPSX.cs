using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProjectPSX {
    class ProjectPSX {
        const int PSX_MHZ = 33868800;
        private CPU cpu;
        private BUS bus;

        private Window window;

        private long counter;
        Stopwatch watch = new Stopwatch();

        public ProjectPSX(Window window) {
            this.window = window;
            window.getScreen().MouseDoubleClick += new MouseEventHandler(toggleDebug);

            bus = new BUS();
            cpu = new CPU(bus);

            bus.loadBios();

            bus.setWindow(window);
        }

        public void toggleDebug(object sender, MouseEventArgs e) {
            if (!cpu.debug) {
                cpu.debug = true;
                bus.gpu.debug = true;
            } else {
                cpu.debug = false;
                bus.gpu.debug = false;
            }
        }

        public void POWER_ON() {
            Task t = Task.Factory.StartNew(EXECUTE, TaskCreationOptions.LongRunning);
        }

        private void EXECUTE() {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            watch.Start();
            try {
                while (true) {
                    for (int i = 0; i < 2822; i++) {
                        for (int j = 0; j < 100; j++) {
                            cpu.Run();
                            //cpu.handleInterrupts();
                            counter++;
                        }
                        bus.tick(200);
                        cpu.handleInterrupts();

                    }

                    if (watch.ElapsedMilliseconds > 1000) {
                        window.Text = " ProjectPSX | Cpu Speed " + (int)(((float)counter / (PSX_MHZ / 2)) * 100) + "%" + " | Fps " + window.getFPS();
                        watch.Restart();
                        counter = 0;
                    }
                }
            } catch (Exception e) {
                Console.WriteLine(e.ToString());
            }
        }

    }
}
