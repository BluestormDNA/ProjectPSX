using System;
using System.Collections.Generic;

namespace ProjectPSX.Devices {
    static class Extension {
        public static void EnqueueRange<T>(this Queue<T> queue, Span<T> parameters) {
            foreach (T parameter in parameters)
                queue.Enqueue(parameter);
        }
    }
}
