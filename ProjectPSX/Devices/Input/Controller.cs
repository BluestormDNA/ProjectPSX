using System.Collections.Generic;
using ProjectPSX.Devices.Input;

namespace ProjectPSX {
    public abstract class Controller {

        protected Queue<byte> transferDataFifo = new Queue<byte>();
        protected ushort buttons = 0xFFFF;
        public bool ack;

        public abstract byte process(byte b);
        public abstract void resetToIdle();

        public void handleJoyPadDown(GamepadInputsEnum inputCode) {
            buttons &= (ushort)~(buttons & (ushort)inputCode);           
            //Console.WriteLine(buttons.ToString("x8"));
        }

        public void handleJoyPadUp(GamepadInputsEnum inputCode) {
            buttons |= (ushort)inputCode;
            //Console.WriteLine(buttons.ToString("x8"));
        }

    }
}
