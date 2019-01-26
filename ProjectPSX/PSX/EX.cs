using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectPSX {
    static class EX {
        public const uint LOAD_ADRESS_ERROR = 0x4;
        public const uint STORE_ADRESS_ERROR = 0x5;
        public const uint SYSCALL = 0x8;
        public const uint OVERFLOW = 0xC;
    }
}
