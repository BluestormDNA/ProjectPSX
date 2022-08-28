using ProjectPSX.Devices;
using ProjectPSX.Devices.CdRom;
using ProjectPSX.Devices.Expansion;
using ProjectPSX.Devices.Input;

namespace ProjectPSX {
    public class ProjectPSX {
        const int PSX_MHZ = 33868800;
        const int SYNC_CYCLES = 100;
        const int MIPS_UNDERCLOCK = 3; //Testing: This compensates the ausence of HALT instruction on MIPS Architecture, may broke some games.
        const int CYCLES_PER_FRAME = PSX_MHZ / 60;
        const int SYNC_LOOPS = (CYCLES_PER_FRAME / (SYNC_CYCLES * MIPS_UNDERCLOCK)) + 1;

        private CPU cpu;
        private BUS bus;
        private CDROM cdrom;
        private GPU gpu;
        private SPU spu;
        private JOYPAD joypad;
        private TIMERS timers;
        private MDEC mdec;
        private Controller controller;
        private MemoryCard memoryCard;
        private CD cd;
        private InterruptController interruptController;
        private Exp2 exp2;

        public ProjectPSX(IHostWindow window, string diskFilename) {
            controller = new DigitalController();
            memoryCard = new MemoryCard();

            interruptController = new InterruptController();

            cd = new CD(diskFilename);
            spu = new SPU(window, interruptController);
            gpu = new GPU(window);
            cdrom = new CDROM(cd, spu);
            joypad = new JOYPAD(controller, memoryCard);
            timers = new TIMERS();
            mdec = new MDEC();
            exp2 = new Exp2();
            bus = new BUS(gpu, cdrom, spu, joypad, timers, mdec, interruptController, exp2);
            cpu = new CPU(bus);

            bus.loadBios();
            if (diskFilename.EndsWith(".exe")) {
                bus.loadEXE(diskFilename);
            }
        }

        public void RunFrame() {
            //A lame mainloop with a workaround to be able to underclock.
            for (int i = 0; i < SYNC_LOOPS; i++) {
                for (int j = 0; j < SYNC_CYCLES; j++) {
                    cpu.Run();
                    //cpu.handleInterrupts();
                }
                bus.tick(SYNC_CYCLES * MIPS_UNDERCLOCK);
                cpu.handleInterrupts();
            }
        }

        public void JoyPadUp(GamepadInputsEnum button) => controller.handleJoyPadUp(button);

        public void JoyPadDown(GamepadInputsEnum button) => controller.handleJoyPadDown(button);

        public void toggleDebug() {
            cpu.debug = !cpu.debug;
            gpu.debug = !gpu.debug;
        }

        public void toggleCdRomLid() {
            cdrom.toggleLid();
        }

    }
}
