using System.Globalization;

public static class CountryResourceDisplay
{
    public static string Format(decimal value)
        => value > 99999m ? "99999+" : value.ToString("#,0.#", CultureInfo.InvariantCulture);
}
