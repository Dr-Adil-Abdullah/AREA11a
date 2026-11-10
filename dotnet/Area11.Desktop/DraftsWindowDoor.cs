using System;
using System.Linq;
using System.Windows;
using Area11.Core.Models;
using Area11.Desktop.ViewModels;
using Area11.Desktop.Views;

namespace Area11.Desktop;

/// <summary>The one UI door to the drafts drawer; view-models do not construct Windows.</summary>
internal static class DraftsWindowDoor
{
    public static void Show(AppServices services, User user, string draftType, Action<int> onOpen)
    {
        DraftsView? window = null;
        var viewModel = new DraftsViewModel(services, user, draftType, onOpen, () => window?.Close());
        window = new DraftsView(viewModel);
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(candidate => candidate.IsActive)
                    ?? Application.Current?.MainWindow;

        if (owner is not null && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }

        window.ShowDialog();
    }
}
