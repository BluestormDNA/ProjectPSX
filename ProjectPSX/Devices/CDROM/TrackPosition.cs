using System;

namespace ProjectPSX.Devices.CdRom;

public readonly struct TrackPosition
{
    public int M { get; }

    public int S { get; }

    public int F { get; }

    public TrackPosition(int m, int s, int f)
    {
        if (m is < 0 or > 99)
            throw new ArgumentOutOfRangeException(nameof(m), s, null);

        if (s is < 0 or > 59)
            throw new ArgumentOutOfRangeException(nameof(s), s, null);

        if (f is < 0 or > 74)
            throw new ArgumentOutOfRangeException(nameof(f), f, null);

        M = m;
        S = s;
        F = f;
    }

    public override string ToString()
    {
        return $"{M:D2}:{S:D2}:{F:D2}";
    }

    public int ToInt32()
    {
        return M * 60 * 75 + S * 75 + F;
    }
}