using System;

namespace ProjectPSX.Devices; 
public class DMA {

    Channel[] channels = new Channel[8];

    public DMA(BUS bus) {
        var interrupt = new InterruptChannel();
        channels[0] = new DmaChannel(0, interrupt, bus);
        channels[1] = new DmaChannel(1, interrupt, bus);
        channels[2] = new DmaChannel(2, interrupt, bus);
        channels[3] = new DmaChannel(3, interrupt, bus);
        channels[4] = new DmaChannel(4, interrupt, bus);
        channels[5] = new DmaChannel(5, interrupt, bus);
        channels[6] = new DmaChannel(6, interrupt, bus);
        channels[7] = interrupt;
    }

    public uint load(uint addr) {
        var channel = (addr & 0x70) >> 4;
        var register = addr & 0xF;
        //Console.WriteLine("DMA load " + channel + " " + register  + ":" + channels[channel].load(register).ToString("x8"));
        return channels[channel].load(register);
    }

    public void write(uint addr, uint value) {
        var channel = (addr & 0x70) >> 4;
        var register = addr & 0xF;
        //Console.WriteLine("DMA write " + channel + " " + register + ":" + value.ToString("x8"));

        channels[channel].write(register, value);
    }

    public bool tick() {
        for (var i = 0; i < 7; i++) {
            ((DmaChannel)channels[i]).transferBlockIfPending();
        }
        return ((InterruptChannel)channels[7]).tick();
    }

}
