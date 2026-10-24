using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Area11.Core.Utils;

namespace Area11.Desktop.Converters;

/// <summary>
/// Turns an <see cref="EmptyHint"/> into one sentence, or into nothing at all (v0.33.0, D-89).
///
/// Why a converter and not a property on the page: the window binds to a *different* view-model for
/// every page, and only this one converter has to know that. A page that is not empty hands back
/// null, the converter answers <see cref="Visibility.Visible"/> for the card, and the card disappears
/// - so there is no second flag anywhere that could drift out of step with the list it describes.
/// </summary>
public sealed class EmptyHintConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not EmptyHint hint)
        {
            return string.Empty;
        }

        // `parameter` is how a binding chooses what it wants: the whole sentence by default, or one
        // half of it - `Headline` / `Advice` - so a view can set them in two sizes.
        return (parameter as string) switch
        {
            "Headline" => hint.Headline,
            "Advice" => hint.Advice,
            _ => hint.Sentence
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("The empty-page card is read-only: nothing on it is typed into.");
}

/// <summary>
/// The card's own visibility: **visible when the page is empty, collapsed when it has rows**
/// (v0.33.0, D-89). Written here rather than reusing <c>BoolToVis</c> so the rule reads at the
/// binding - a hint is an object, not a bool, and "not null" is the whole question.
/// </summary>
public sealed class EmptyHintVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is EmptyHint ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("The empty-page card is read-only: its visibility never writes back.");
}
