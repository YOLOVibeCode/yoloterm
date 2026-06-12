namespace YOLOTerm.Core.Contracts;

/// <summary>
/// RGB color for terminal cells (contracts v1)
/// </summary>
public struct Color
{
    public byte R { get; init; }
    public byte G { get; init; }
    public byte B { get; init; }

    public Color(byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
    }

    public static Color FromHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6)
            throw new ArgumentException($"Invalid hex color: {hex}");

        return new Color(
            Convert.ToByte(hex.Substring(0, 2), 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16)
        );
    }

    public string ToHex() => $"#{R:x2}{G:x2}{B:x2}";

    public override string ToString() => ToHex();

    public override bool Equals(object? obj) =>
        obj is Color other && R == other.R && G == other.G && B == other.B;

    public override int GetHashCode() => HashCode.Combine(R, G, B);

    public static bool operator ==(Color left, Color right) => left.Equals(right);
    public static bool operator !=(Color left, Color right) => !left.Equals(right);
}
