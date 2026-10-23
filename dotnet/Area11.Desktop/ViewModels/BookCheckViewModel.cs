using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>One rule on the "do the books add up?" page - its sentence, its size and its answer.</summary>
public sealed class BookCheckRuleRow
{
    public BookCheckRuleRow(BookCheckRule rule, int number)
    {
        Number = number.ToString();
        Title = rule.Title;
        Why = rule.Why;
        Summary = rule.Summary;
        IsClean = rule.Clean;
        Badge = rule.Clean ? "OK" : "FAULT";
    }

    public string Number { get; }

    public string Title { get; }

    public string Why { get; }

    public string Summary { get; }

    public bool IsClean { get; }

    /// <summary>"OK" / "FAULT" - a word, because a colour alone is not a message.</summary>
    public string Badge { get; }
}

/// <summary>
/// A pair of rows, but the class has to earn its place: the table shows the money in words.
///
/// Why not bind the Core model directly: `ExpectedPaisa` on a screen means nothing, and the owner
/// should never have to divide by a hundred to see a rupee. The view models turn paisa into the
/// text the shop reads, the same way every other page does.
/// </summary>
public sealed class BookCheckFindingRow
{
    public BookCheckFindingRow(string ruleTitle, BookCheckFinding finding)
    {
        Rule = ruleTitle;
        Code = finding.Code;
        ExpectedText = Money.Format(finding.ExpectedPaisa);
        FoundText = Money.Format(finding.FoundPaisa);
        GapText = finding.GapText;
    }

    public string Rule { get; }

    public string Code { get; }

    public string ExpectedText { get; }

    public string FoundText { get; }

    /// <summary>"Rs 5 missing" / "Rs 5 too much" - the direction is the actionable half.</summary>
    public string GapText { get; }
}

/// <summary>
/// "Do the books add up?" (F-54). A read-only page: there is exactly one command, and it re-reads
/// the database. Nothing here can change a row, which is why the page can be opened at any time -
/// in the middle of a busy day, with a customer at the counter.
///
/// Why this page exists at all: the owner's standing order is that no error may remain because it is
/// money. A total that no longer matches its lines is the one fault no other page can show - the
/// counter, the report and the receipt all read the same wrong number, so they agree with each other
/// and the shop learns nothing. This page compares the number with the rows underneath it.
/// </summary>
public sealed class BookCheckViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;

    private string _headline = "Not checked yet.";
    private string _advice = string.Empty;
    private string _whenText = string.Empty;
    private string _databaseText = string.Empty;
    private bool _hasProblems;

    public BookCheckViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        RefreshCommand = new RelayCommand(Load);

        Load();
    }

    /// <summary>The only action on the page. It cannot write, so it needs no confirmation.</summary>
    public RelayCommand RefreshCommand { get; }

    public ObservableCollection<BookCheckRuleRow> Rules { get; } = new();

    public ObservableCollection<BookCheckFindingRow> Findings { get; } = new();

    public string Headline
    {
        get => _headline;
        private set => SetProperty(ref _headline, value);
    }

    public string Advice
    {
        get => _advice;
        private set => SetProperty(ref _advice, value);
    }

    public string WhenText
    {
        get => _whenText;
        private set => SetProperty(ref _whenText, value);
    }

    /// <summary>The file that was read: a check is only about one database, and a second counter is another.</summary>
    public string DatabaseText
    {
        get => _databaseText;
        private set => SetProperty(ref _databaseText, value);
    }

    /// <summary>True when anything is wrong, so the banner can be red.</summary>
    public bool HasProblems
    {
        get => _hasProblems;
        private set => SetProperty(ref _hasProblems, value);
    }

    /// <summary>True when the answer is "everything adds up", so the page can say so in green.</summary>
    public bool IsClean => !HasProblems;

    private void Load()
    {
        Rules.Clear();
        Findings.Clear();

        var result = _services.Books.Run(_user);

        if (!result.Ok)
        {
            Headline = result.Error ?? "The check could not run.";
            Advice = "Nothing was changed. Send this page with tools\\SEND-REPORT.cmd.";
            WhenText = DateTime.Now.ToString("dd MMM yyyy, HH:mm");
            DatabaseText = string.Empty;
            SetProblems(true);
            return;
        }

        var report = result.Value!;

        var number = 1;

        foreach (var rule in report.Rules)
        {
            Rules.Add(new BookCheckRuleRow(rule, number++));

            foreach (var finding in rule.Findings)
            {
                Findings.Add(new BookCheckFindingRow(rule.Title, finding));
            }
        }

        Headline = report.Headline;
        Advice = report.Advice;
        WhenText = report.RanAt.ToString("dd MMM yyyy, HH:mm");
        DatabaseText = report.DatabaseFile;
        SetProblems(!report.Clean);
    }

    /// <summary>One change, two properties: the page reads both, so both are raised together.</summary>
    private void SetProblems(bool value)
    {
        HasProblems = value;
        OnPropertyChanged(nameof(IsClean));
    }
}
