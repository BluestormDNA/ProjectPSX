namespace ProjectPSX.Devices.CdRom;

public readonly struct TrackIndex
{
    public int Number { get; }

    public TrackPosition Position { get; }

    public TrackIndex(int number, TrackPosition position)
    {
        Number = number;
        Position = position;
    }

    public override string ToString()
    {
        return $"{nameof(Number)}: {Number}, {nameof(Position)}: {Position}";
    }
}