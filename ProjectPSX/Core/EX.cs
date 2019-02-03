using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectPSX {
    enum EX {
        LOAD_ADRESS_ERROR = 0x4,
        STORE_ADRESS_ERROR = 0x5,
        SYSCALL = 0x8,
        BREAK = 0x9,
        ILLEGAL_INSTR = 0xA,
        COPROCESSOR_ERROR = 0xB,
        OVERFLOW = 0xC
    }
}
