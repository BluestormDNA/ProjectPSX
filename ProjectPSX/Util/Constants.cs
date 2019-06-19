namespace ProjectPSX {

    public enum EX {
        INTERRUPT = 0x0,
        LOAD_ADRESS_ERROR = 0x4,
        STORE_ADRESS_ERROR = 0x5,
        BUS_ERROR_FETCH = 0x6,
        SYSCALL = 0x8,
        BREAK = 0x9,
        ILLEGAL_INSTR = 0xA,
        COPROCESSOR_ERROR = 0xB,
        OVERFLOW = 0xC
    }

    public enum Interrupt {
        VBLANK = 0x1,
        GPU = 0x2,
        CDROM = 0x4,
        DMA = 0x8,
        TIMER0 = 0x10,
        TIMER1 = 0x20,
        TIMER2 = 0x40,
        CONTR = 0x80,
        SIO = 0x100,
        SPU = 0x200,
        PIO = 0x400
    }

    public enum Width {
        WORD,
        BYTE,
        HALF
    }

}