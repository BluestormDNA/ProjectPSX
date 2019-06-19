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

        public void executeFps() {
            for (int i = 0; i < 2822; i++) {
                for (int j = 0; j < 100; j++) {
                    cpu.Run();
                }
                //cpu.handleInterrupts();
                bus.tick(200);
            }
        }
        private void EXECUTE() {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            watch.Start();
            try {
                while (true) {
                //for (int i = 0; i < 100; i++) {
                //    cpu.Run();
                //    counter++;
                //}
                //
                //bus.tick(200); //2 ticks per opcode
                //cpu.handleInterrupts();
                for (int i = 0; i < 2822; i++)
                {
                    for (int j = 0; j < 100; j++)
                    {
                        cpu.Run();
                        counter++;
                    }
                    bus.tick(200);
                    cpu.handleInterrupts();
                    }


                if (watch.ElapsedMilliseconds > 1000) {
                        //window.Text = " ProjectPSX | Speed % " + 1000 / (float)watch.ElapsedMilliseconds * 100;
                        window.Text = " ProjectPSX | Speed % " + (((float)counter / (PSX_MHZ / 2)) * 100);
                        watch.Restart();
                        counter = 0;
                        //fpsCounter = 0;
                    }
                    //if (counter >= PSX_MHZ) {
                    //    counter = 0;
                    //    window.Text = "ProjectPSX | Fps: " + (60 / ((float)watch.ElapsedMilliseconds / 1000)).ToString();
                    //    watch.Restart();
                    //}
                }
            } catch (Exception e) {
                Console.WriteLine(e.ToString());
            }
        }

    }
}
