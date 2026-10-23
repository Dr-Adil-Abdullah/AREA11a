using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The sample shop page (F-53, D-69, D-74): one button that fills a new Area11 with two and a half
/// months of one small pharmacy, so every screen - the counter, the khata, the reports, the black box -
/// has something to show.
///
/// No rule lives here. <see cref="SampleShopService"/> decides whether this database may receive demo
/// data at all, writes all of it in one transaction, and leaves one line in the black box; this class
/// shows what is about to happen, asks for the tick, and puts the answer in one sentence.
/// </summary>
public sealed class SampleShopViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;

    private string _titleText = "Ready to fill this shop";
    private string _subtitleText = string.Empty;
    private string _stepsText = string.Empty;
    private string _summaryText = string.Empty;
    private string _problemText = string.Empty;
    private bool _hasProblem;
    private bool _canSeed;
    private bool _confirm;
    private string _statusText = "Read what will be added, then tick the box.";
    private bool _isStatusError;

    public SampleShopViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        RefreshCommand = new RelayCommand(Load);
        SeedCommand = new RelayCommand(Seed);

        Load();
    }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand SeedCommand { get; }

    public string TitleText
    {
        get => _titleText;
        private set => SetProperty(ref _titleText, value);
    }

    public string SubtitleText
    {
        get => _subtitleText;
        private set => SetProperty(ref _subtitleText, value);
    }

    public string StepsText
    {
        get => _stepsText;
        private set => SetProperty(ref _stepsText, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public string ProblemText
    {
        get => _problemText;
        private set => SetProperty(ref _problemText, value);
    }

    public bool HasProblem
    {
        get => _hasProblem;
        private set => SetProperty(ref _hasProblem, value);
    }

    /// <summary>
    /// True while the program runs as the demo shop (`--demo`, D-76). The page turns this into a
    /// banner, because the one thing a demo must never do is look like the shop's own books.
    /// </summary>
    public bool IsDemo => DemoShop.IsOn;

    /// <summary>The same question the other way round, for the hint that only the real shop needs.</summary>
    public bool IsRealShop => !DemoShop.IsOn;

    public string DemoBanner =>
        "DEMO SHOP - nothing on any page is real. It is a separate database in "
        + (DemoShop.DataFolder ?? string.Empty)
        + " and your own shop's database has not been touched. When the app is finished it is enough to "
        + "close this window and delete that folder: no real bill can be affected.";

    /// <summary>
    /// Shown in the real shop: where the demo lives and how to open it without mixing anything.
    /// </summary>
    public string DemoHint =>
        "To try the demo without writing anything into this shop, close Area11 and double-click "
        + "DEMO-AREA11.cmd - it opens a separate demo shop with its own database.";

    /// <summary>True when the database itself allows it (the tick is a separate question).</summary>
    public bool CanSeed
    {
        get => _canSeed;
        private set
        {
            if (SetProperty(ref _canSeed, value))
            {
                OnPropertyChanged(nameof(CanPress));
            }
        }
    }

    /// <summary>The owner's own "yes, I understand" - a demo shop cannot appear by a stray click.</summary>
    public bool Confirm
    {
        get => _confirm;
        set
        {
            if (SetProperty(ref _confirm, value))
            {
                OnPropertyChanged(nameof(CanPress));
            }
        }
    }

    public bool CanPress => CanSeed && Confirm;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsStatusError
    {
        get => _isStatusError;
        private set => SetProperty(ref _isStatusError, value);
    }

    public void Load()
    {
        var preview = _services.SampleShop.Preview(_user);

        if (!preview.Ok || preview.Value is null)
        {
            SetStatus(preview.Error ?? "This page could not be opened.", true);
            CanSeed = false;
            return;
        }

        var report = preview.Value;

        TitleText = report.TitleLabel;
        SubtitleText = report.SubtitleLabel;
        StepsText = report.StepsText;
        SummaryText = report.SummaryLabel;
        ProblemText = report.ProblemText;
        HasProblem = ProblemText.Length > 0;
        CanSeed = report.CanSeed;
        Confirm = false;

        SetStatus(
            report.CanSeed
                ? "Tick the box and press the button. Everything is written in one go."
                : "The program will not touch this database - read the reason above.",
            !report.CanSeed);
    }

    private void Seed()
    {
        if (!Confirm)
        {
            SetStatus("Tick the box first - this writes demo money into every page.", true);
            return;
        }

        var seeded = _services.SampleShop.Seed(_user);

        if (!seeded.Ok)
        {
            SetStatus(seeded.Error ?? "The sample shop could not be added.", true);
            return;
        }

        Confirm = false;
        CanSeed = false;

        SetStatus(
            "The sample shop is in. Open Counter (POS) and sell something, or SELL -> Bills to walk "
            + "through two and a half months: seven hundred bills, a khata with names, and today's drawer "
            + "still open. The black box has one line saying it was demo data.",
            false);
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}
