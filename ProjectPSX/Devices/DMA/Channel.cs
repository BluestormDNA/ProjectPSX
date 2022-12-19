namespace ProjectPSX.Devices; 
public abstract class Channel {
    public abstract void write(uint register, uint value);
    public abstract uint load(uint regiter);
}
