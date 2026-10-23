using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

// empty-page: none - this page exists only to explain a failure, and its own card is the
// sentence; adding "nothing here yet" over it would contradict what it just said.
/// <summary>
/// The card that stands in for a page which could not be opened (v0.32.0, D-88).
///
/// Why a page of its own and not a message box: a box stops the shop and has to be dismissed before
/// anything else can be done. A card sits in the same place the page would have been, the menu still
/// works, the counter is one click away - and the sentence stays on the screen while the operator
/// reads it instead of vanishing with the click that dismissed it.
/// </summary>
public sealed class PageFaultViewModel : ObservableObject
{
    public PageFaultViewModel(PageFault fault)
    {
        Fault = fault ?? throw new ArgumentNullException(nameof(fault));
    }

    public PageFault Fault { get; }

    /// <summary>"The Bills page could not be opened."</summary>
    public string Sentence => Fault.Sentence;

    /// <summary>The exception's own name and message.</summary>
    public string Detail => Fault.Detail;

    /// <summary>What to do next - and that the rest of the shop still works.</summary>
    public string Advice => Fault.Advice;

    /// <summary>
    /// Where the full detail is - the file the owner is asked to send, named on the card so he does
    /// not have to look for it. The wording lives on <see cref="PageFault"/> so a test can read it.
    /// </summary>
    public string ReportLabel => Fault.ReportLabel;
}
