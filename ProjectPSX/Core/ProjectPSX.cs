using ProjectPSX.Devices;
using ProjectPSX.Devices.Input;

namespace ProjectPSX {
    public class ProjectPSX {
        const int PSX_MHZ = 33868800;
        const int SYNC_CYCLES = 100;
        const int MIPS_UNDERCLOCK = 2; //Testing: This compensates the ausence of HALT instruction on MIPS Architecture, may broke some games.

        private CPU cpu;
        private BUS bus;
        private CDROM cdrom;
        private Controller controller;

        public ProjectPSX(IHostWindow window, string diskFilename) {
            controller = new DigitalController();
            cdrom = new CDROM(window, diskFilename);
            bus = new BUS(window, controller, cdrom);
            cpu = new CPU(bus);

            bus.loadBios();
        }

        public void RunFrame() {
            //A lame mainloop with a workaround to be able to underclock.
            int cyclesPerFrame = PSX_MHZ / 60;
            int syncLoops = (cyclesPerFrame / (SYNC_CYCLES * MIPS_UNDERCLOCK)) + 1;

            for(int i = 0; i < syncLoops; i++) {
                for (int j = 0; j < SYNC_CYCLES; j++) {
                    cpu.Run();
                    //cpu.handleInterrupts();
                }
                bus.tick(SYNC_CYCLES * MIPS_UNDERCLOCK);
                cpu.handleInterrupts();
            }
        }

        public void JoyPadUp(GamepadInputsEnum button) {
            controller.handleJoyPadUp(button);
        }

        public void JoyPadDown(GamepadInputsEnum button) {
            controller.handleJoyPadDown(button);
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

    }
}
