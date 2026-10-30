using System.Globalization;

/// <summary>Compact number display for the pixel UI (pause and game over stats): 12.3K, 4.5M, 1.2B.</summary>
public static class NumberFormat
{
    /// <summary>
    /// Numbers under 10,000 are written in full; from 10,000 on they get a K, M or B suffix with one decimal under 100
    /// of the unit (12.3K, 123K, 1.2M). The value is truncated, never rounded up, so it never reads "1000K".
    /// </summary>
    public static string Short(long value)
    {
        if (value < 0) return "-" + Short(-value);
        if (value < 10000) return value.ToString(CultureInfo.InvariantCulture);

        double scaled;
        char suffix;
        if (value < 1_000_000) { scaled = value / 1000.0; suffix = 'K'; }
        else if (value < 1_000_000_000) { scaled = value / 1_000_000.0; suffix = 'M'; }
        else { scaled = value / 1_000_000_000.0; suffix = 'B'; }

        if (scaled >= 100.0) return ((long)scaled).ToString(CultureInfo.InvariantCulture) + suffix;
        double truncated = System.Math.Floor(scaled * 10.0) / 10.0;
        return truncated.ToString("0.#", CultureInfo.InvariantCulture) + suffix;
    }
}
