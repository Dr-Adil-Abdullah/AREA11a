using System.Windows;
using Area11.Core.Services;

namespace Area11.Desktop;

/// <summary>
/// Applies the Core label catalog to WPF dynamic resources. Replacing an application resource
/// notifies every open page and window that uses it, which is the visual half of S-05.
/// </summary>
public static class LabelResources
{
    public const string Prefix = "AppLabel.";

    public static string Key(string labelKey) => Prefix + labelKey;

    public static void Apply(AppLabelService labels)
    {
        if (Application.Current is null)
        {
            return;
        }

        foreach (var label in labels.List())
        {
            Application.Current.Resources[Key(label.Key)] = label.Text;
        }
    }
}
