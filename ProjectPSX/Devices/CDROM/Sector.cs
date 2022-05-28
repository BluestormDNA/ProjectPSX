using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProjectPSX.Devices.CdRom {
    class Sector {

        // Standard size for a raw sector / CDDA
        public const int RAW_BUFFER = 2352;

        // Only for the SPU: It represents a sector of data already pre decoded AND resampled so we need a bigger buffer (RAW_BUFFER * 4)
        // and on the case of mono even a bigger one, as samples are mirrored to L/R as our output is allways stereo (that would be * 8)
        // but on the special case of 18900 resampling we need even a bigger one... so go * 16
        public const int XA_BUFFER = RAW_BUFFER * 16;

        private byte[] sectorBuffer;

        private int pointer;
        private int size;

        public Sector(int size) {
            sectorBuffer = new byte[size];
        }

        public void fillWith(Span<byte> data) {
            pointer = 0;
            size = data.Length;
            var dest = sectorBuffer.AsSpan();
            data.CopyTo(dest);
        }

        public ref byte readByte() {
            ref var data = ref MemoryMarshal.GetArrayDataReference(sectorBuffer);
            return ref Unsafe.Add(ref data, pointer++);
        }

        public ref short readShort() {
            ref var data = ref MemoryMarshal.GetArrayDataReference(sectorBuffer);
            ref var valueB = ref Unsafe.Add(ref data, pointer);
            ref var valueS = ref Unsafe.As<byte, short>(ref valueB);
            pointer += 2;
            return ref valueS;
        }

        public Span<uint> read(int size) { //size from dma comes as u32
            var dma = sectorBuffer.AsSpan().Slice(pointer, size * 4);
            pointer += size * 4;
            return MemoryMarshal.Cast<byte, uint>(dma);
        }

        public Span<byte> read() => sectorBuffer.AsSpan().Slice(0, size);

        public bool hasData() => pointer < size;

        public bool hasSamples() => size - pointer > 3;

        public void clear() {
            pointer = 0;
            size = 0;
        }

    }
}
