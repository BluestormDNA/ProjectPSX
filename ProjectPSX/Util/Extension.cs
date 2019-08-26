using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectPSX.Devices {
    static class Extension {
        public static void EnqueueRange<T>(this Queue<T> queue, params T[] parameters) {
            foreach (T parameter in parameters)
                queue.Enqueue(parameter);
        }
    }
}
