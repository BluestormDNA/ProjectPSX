using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProjectPSX.Devices.CdRom {
    class Sector {

        private const int BYTES_PER_SECTOR_RAW = 2352;
        private byte[] sectorBuffer = new byte[BYTES_PER_SECTOR_RAW];

        private int pointer;
        private int size;

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

        public Span<uint> read(int size) { //size from dma comes as u32
            var dma = sectorBuffer.AsSpan().Slice(pointer, size * 4);
            pointer += size * 4;
            return MemoryMarshal.Cast<byte, uint>(dma);
        }

        public Span<byte> read() => sectorBuffer.AsSpan().Slice(0, size);

        public bool hasData() => pointer < size;

        public void clear() {
            pointer = 0;
            size = 0;
        }

    }
}
