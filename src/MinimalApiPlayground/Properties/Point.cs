public class Point
{
    public double X { get; set; }

    public double Y { get; set; }

    public override string ToString() => $"({X},{Y})";

    public static bool TryParse(string value, out Point? point)
    {
        // Format is "(12.3,10.1)"

        var trimmedValue = value.TrimStart('(').TrimEnd(')');
        var segments = trimmedValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 2
            && double.TryParse(segments[0], out var x)
            && double.TryParse(segments[1], out var y))
        {
            point = new Point { X = x, Y = y };
            return true;
        }

        point = null;
        return false;
    }
}
