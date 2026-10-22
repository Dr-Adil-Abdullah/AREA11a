namespace Area11.Core.Utils;

/// <summary>
/// Which build the shop is running.
///
/// Why this exists: the owner tests each version on his own computer and must be able to see, at a
/// glance, that he opened the new one - not an old copy from an earlier download. So every version
/// carries a name and a colour; the colour is painted on the brand brushes in
/// <c>Area11.Desktop/Resources/Theme.xaml</c> and the label is shown on the sign-in window and in
/// the sidebar.
///
/// The rule for the team (AGENTS.md section 9): **whenever the code changes, bump <see cref="Version"/>,
/// pick the next colour, and add the row to `docs/VERSIONS.md`.** `tools/check_repo.py` fails the
/// build if this file, the theme and that table disagree, so the three can never drift apart.
///
/// Colour choice: white text sits on the brand colour, so the shade must stay dark enough to read
/// (contrast at least 4.5:1 against white).
/// </summary>
public static class BuildInfo
{
    /// <summary>Version shown in the window title and on the sign-in screen.</summary>
    public const string Version = "0.20.0";

    /// <summary>The colour of this version, in one word - also the name of the release.</summary>
    public const string CodeName = "Pebble";

    /// <summary>The exact brand colour of this version (must equal BrandBrush in Theme.xaml).</summary>
    public const string BrandHex = "#57534E";

    /// <summary>Hover / pressed shade of the same colour.</summary>
    public const string BrandDarkHex = "#403C38";

    /// <summary>Very light tint used for stripes and badges.</summary>
    public const string BrandSoftHex = "#F4F3F1";

    public const string ReleasedOn = "2026-10-26";

    /// <summary>"v0.2.0 · Purple" - the label the owner looks for.</summary>
    public static string Label => $"v{Version} · {CodeName}";

    /// <summary>Full label for tooltips: version, colour and date.</summary>
    public static string FullLabel => $"{Label} ({ReleasedOn})";
}
