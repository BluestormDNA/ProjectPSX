using System.Collections.Generic;

namespace ProjectPSX.Devices.CdRom;

public class Track
{
    public Track()
    {
    }

    public Track(string file, long fileLength, byte index, int lbaStart, int lbaEnd)
    {
        File = file;
        FileLength = fileLength;
        Index = index;
        LbaStart = lbaStart;
        LbaEnd = lbaEnd;
    }

    public string File { get; init; } = null!;

    public long FilePosition { get; set; }

    public long FileLength { get; set; }

    public byte Index { get; set; }

    public int LbaStart { get; set; }

    public int LbaEnd { get; set; }

    public int LbaLength { get; set; }

    public IList<TrackIndex> Indices { get; } = new List<TrackIndex>();

    public override string ToString()
    {
        return
            $"{nameof(Index)}: {Index}, {nameof(LbaStart)}: {LbaStart}, {nameof(LbaEnd)}: {LbaEnd}, {nameof(LbaLength)}: {LbaLength}, {nameof(Indices)}: {Indices.Count}, {nameof(FilePosition)}: {FilePosition}";
    }
}