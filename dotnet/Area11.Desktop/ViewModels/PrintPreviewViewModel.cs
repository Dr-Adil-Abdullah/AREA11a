using Area11.Core.Models;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// Portion 11: the exact text the printer will receive, on the screen before the paper moves.
/// Paper costs money and a wrong paper width loses the last column, so every bulk print and every
/// reprint from the history pages can pass through here; printing itself stays one button.
/// </summary>
public sealed class PrintPreviewViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly ReceiptDocument? _document;
    private readonly BulkPrintJob? _job;

    private string _statusText = string.Empty;
    private bool _isStatusError;

    public PrintPreviewViewModel(AppServices services, ReceiptDocument document)
    {
        _services = services;
        _document = document ?? throw new ArgumentNullException(nameof(document));

        Title = "Preview - " + _document.Title;
        PreviewText = _document.PlainText;
        PrintCommand = new RelayCommand(Print);
    }

    public PrintPreviewViewModel(AppServices services, BulkPrintJob job)
    {
        _services = services;
        _job = job ?? throw new ArgumentNullException(nameof(job));

        Title = "Preview - " + job.Title + " (" + job.Documents.Count + ")";
        PreviewText = Combine(job);
        PrintCommand = new RelayCommand(Print);
    }

    public string Title { get; }

    /// <summary>The paper text, cut to the printer's width - monospaced on the screen.</summary>
    public string PreviewText { get; }

    public RelayCommand PrintCommand { get; }

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

    private static string Combine(BulkPrintJob job)
    {
        var parts = new List<string> { job.Summary.PlainText };
        parts.AddRange(job.Documents.Select(document => document.PlainText));
        return string.Join(Environment.NewLine + new string('=', 32) + Environment.NewLine, parts);
    }

    private void Print()
    {
        var printed = _job is null
            ? _services.Printer.Print(_document!)
            : _services.Printer.PrintBulk(_job);

        if (printed.Ok)
        {
            SetStatus(_job is null ? "Sent to the printer." : "Sent " + _job.Documents.Count + " to the printer.", false);
            return;
        }

        SetStatus(printed.Error ?? "The printer did not answer.", true);
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}
