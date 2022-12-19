using System;

namespace ProjectPSX.Devices;
public sealed class InterruptChannel : Channel {

    // 1F8010F0h DPCR - DMA Control register
    private uint control;

    // 1F8010F4h DICR - DMA Interrupt register
    private bool forceIRQ;
    private uint irqEnable;
    private bool masterEnable;
    private uint irqFlag;
    private bool masterFlag;

    private bool edgeInterruptTrigger;

    public InterruptChannel() {
        control = 0x07654321;
    }

    public override uint load(uint register) {
        switch (register) {
            case 0: return control;
            case 4: return loadInterrupt();
            case 6: return loadInterrupt() >> 16; //castlevania symphony of the night and dino crisis 2 ask for this
            default: Console.WriteLine("Unhandled register on interruptChannel DMA load " + register); return 0xFFFF_FFFF;
        }
    }

    private uint loadInterrupt() {
        uint interruptRegister = 0;

        interruptRegister |= (forceIRQ ? 1u : 0) << 15;
        interruptRegister |= irqEnable << 16;
        interruptRegister |= (masterEnable ? 1u : 0) << 23;
        interruptRegister |= irqFlag << 24;
        interruptRegister |= (masterFlag ? 1u : 0) << 31;

        return interruptRegister;
    }

    public override void write(uint register, uint value) {
        //Console.WriteLine("irqflag pre: " + irqFlag.ToString("x8"));
        switch (register) {
            case 0: control = value; break;
            case 4: writeInterrupt(value); break;
            case 6: writeInterrupt(value << 16 | (forceIRQ ? 1u : 0) << 15); break;
            default: Console.WriteLine($"Unhandled write on DMA Interrupt register {register}"); break;
        }
        //Console.WriteLine("irqflag post: " + irqFlag.ToString("x8"));
    }

    private void writeInterrupt(uint value) {
        forceIRQ = (value >> 15 & 0x1) != 0;
        irqEnable = value >> 16 & 0x7F;
        masterEnable = (value >> 23 & 0x1) != 0;
        irqFlag &= ~(value >> 24 & 0x7F);

        masterFlag = updateMasterFlag();
    }

    public void handleInterrupt(int channel) {
        //IRQ flags in Bit(24 + n) are set upon DMAn completion - but caution - they are set ONLY if enabled in Bit(16 + n).
        if ((irqEnable & 1 << channel) != 0) {
            irqFlag |= (uint)(1 << channel);
        }

        //Console.WriteLine($"MasterFlag: {masterFlag} irqEnable16: {irqEnable:x8} irqFlag24: {irqFlag:x8} {forceIRQ} {masterEnable} {((irqEnable & irqFlag) > 0)}");
        masterFlag = updateMasterFlag();
        edgeInterruptTrigger |= masterFlag;
    }

    public bool isDMAControlMasterEnabled(int channelNumber) {
        return (control >> 3 >> 4 * channelNumber & 0x1) != 0;
    }

    private bool updateMasterFlag() {
        //Bit31 is a simple readonly flag that follows the following rules:
        //IF b15 = 1 OR(b23 = 1 AND(b16 - 22 AND b24 - 30) > 0) THEN b31 = 1 ELSE b31 = 0
        return forceIRQ || masterEnable && (irqEnable & irqFlag) > 0;
    }

    public bool tick() {
        if (edgeInterruptTrigger) {
            edgeInterruptTrigger = false;
            //Console.WriteLine("[IRQ] Triggering DMA");
            return true;
        }
        return false;
    }
}
