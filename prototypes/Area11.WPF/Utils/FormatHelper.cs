using System.Globalization;

namespace Area11.WPF.Utils;

public static class FormatHelper
{
    public static string Rupees(long paisa) => "Rs " + (paisa / 100m).ToString("N2", CultureInfo.InvariantCulture);

    public static string RupeesWhole(long paisa) => "Rs " + (paisa / 100m).ToString("N0", CultureInfo.InvariantCulture);

    public static string Quantity(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    public static string Stamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public static string Date(DateTime value) => value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    public static string Time(DateTime value) => value.ToString("hh:mm tt", CultureInfo.InvariantCulture);
}
