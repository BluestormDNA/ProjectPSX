using System;
using System.Collections.Generic;
using static ProjectPSX.Devices.CDROM;

namespace ProjectPSX.Devices {
    static class Extension {
        public static void EnqueueRange<T>(this Queue<T> queue, Span<T> parameters) {
            foreach (T parameter in parameters)
                queue.Enqueue(parameter);
        }

        public static void EnqueueDelayedInterrupt(this Queue<DelayedInterrupt> queue, byte interrupt, int delay = 50_000) {
            queue.Enqueue(new DelayedInterrupt(delay, interrupt));
        }
    }
}
