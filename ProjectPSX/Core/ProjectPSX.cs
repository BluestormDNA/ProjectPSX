using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;

namespace ProjectPSX {
    class ProjectPSX {
        const int PSX_MHZ = 33868800;
        const int SYNC_CYCLES = 100;
        const int MIPS_UNDERCLOCK = 3; //Testing: This compensates the ausence of HALT instruction on MIPS Architecture, may broke some games.

        private CPU cpu;
        private BUS bus;

        private Window window;

        private long counter;

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

            var timer = new System.Timers.Timer(1000);
            timer.Elapsed += OnTimedEvent;
            timer.Enabled = true;

            try {
                while (true) {
                    for (int j = 0; j < SYNC_CYCLES; j++) {
                        cpu.Run();
                        //cpu.handleInterrupts();
                        counter++;
                    }
                    bus.tick(SYNC_CYCLES * MIPS_UNDERCLOCK);
                    cpu.handleInterrupts();
                }
            } catch (Exception e) {
                Console.WriteLine(e.ToString());
            }
        }


        private void OnTimedEvent(object sender, ElapsedEventArgs e)
        {
            SetWindowText("ProjectPSX | Cpu Speed " + (int)(((float)counter / (PSX_MHZ / MIPS_UNDERCLOCK)) * SYNC_CYCLES) + "%" + " | Fps " + window.getFPS());
            counter = 0;
        }

        // Thread safe write Window Text
        private delegate void SafeCallDelegate(string text);
        private void SetWindowText(string text)
        {
            if (window.InvokeRequired)
            {
                SafeCallDelegate d = new SafeCallDelegate(SetWindowText);
                if (window != null)
                {
                    window.Invoke(d, new object[] { text });
                }
            }
            else
            {
                window.Text = text;
            }
        }
    }
}
