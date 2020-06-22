using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using ProjectPSX.Devices;
using ProjectPSX.Devices.Input;

namespace ProjectPSX {
    public class ProjectPSX {
        const int PSX_MHZ = 33868800;
        const int SYNC_CYCLES = 100;
        const int MIPS_UNDERCLOCK = 3; //Testing: This compensates the ausence of HALT instruction on MIPS Architecture, may broke some games.

        private CPU cpu;
        private BUS bus;
        private CDROM cdrom;
        private Controller controller;

        private IHostWindow window;

        private long counter;

        public ProjectPSX(IHostWindow window, string diskFilename) {
            this.window = window;

            controller = new DigitalController();
            cdrom = new CDROM(window, diskFilename);
            bus = new BUS(controller, cdrom);
            cpu = new CPU(bus);

            bus.loadBios();

            bus.setWindow(window);
        }

        public void toggleDebug() {
            if (!cpu.debug) {
                cpu.debug = true;
                bus.gpu.debug = true;
            } else {
                cpu.debug = false;
                bus.gpu.debug = false;
            }
        }

        public void POWER_ON() {
            var timer = new System.Timers.Timer(1000);
            timer.Elapsed += OnTimedEvent;
            timer.Enabled = true;
        }

        public void RunUncapped() {
            Task t = Task.Factory.StartNew(EXECUTE, TaskCreationOptions.LongRunning);
        }

        public void RunFrame() {
            //33868800 / 60 = 564480 / 300 (Sync * underclock) = 1882~
            for(int i = 0; i < 1882; i++) {
                for (int j = 0; j < SYNC_CYCLES; j++) {
                    cpu.Run();
                    //cpu.handleInterrupts();
                    counter++;
                }
                bus.tick(SYNC_CYCLES * MIPS_UNDERCLOCK);
                cpu.handleInterrupts();
            }
        }

        private void EXECUTE() {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;

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

        public void JoyPadUp(GamepadInputsEnum button) {
            controller.handleJoyPadUp(button);
        }

        public void JoyPadDown(GamepadInputsEnum button) {
            controller.handleJoyPadDown(button);
        }

        private void OnTimedEvent(object sender, ElapsedEventArgs e) {
            window.SetWindowText($"ProjectPSX | Cpu Speed {(int)((float)counter / (PSX_MHZ / MIPS_UNDERCLOCK) * SYNC_CYCLES)}% | Vps {window.GetVPS()}");
            counter = 0;
        }

    }
}
