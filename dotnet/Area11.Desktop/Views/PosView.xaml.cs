using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

public partial class PosView : UserControl
{
    public PosView()
    {
        InitializeComponent();
    }

    private void OnCounterLoaded(object sender, RoutedEventArgs e)
    {
        // Only this view's load, not a child's. Never refocus on every property/selection change.
        if (!ReferenceEquals(e.OriginalSource, this)) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (IsVisible && DataContext is PosViewModel { IsBlocked: false }
                && Window.GetWindow(this)?.IsActive == true)
                MedicineSearchBox.Focus();
        }));
    }

    private void OnAddToCartClick(object sender, RoutedEventArgs e)
    {
        // The command runs after Click. A successful add clears SearchText; a refusal does not.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusSearchAfterAdd));
    }

    private void FocusSearchAfterAdd()
    {
        if (!IsLoaded || !IsVisible || DataContext is not PosViewModel viewModel
            || viewModel.IsBlocked || viewModel.SearchText.Length != 0
            || Window.GetWindow(this)?.IsActive != true) return;
        MedicineSearchBox.Focus();
    }

    private void AddFocusedSearchMatch()
    {
        if (SearchMatchesGrid.SelectedItem is null
            || DataContext is not PosViewModel viewModel || viewModel.IsBlocked) return;
        if (viewModel.AddCommand.CanExecute(null))
        {
            // Same validation/stock/permissions path as the existing Add button, never a new sale path.
            viewModel.AddCommand.Execute(null);
            FocusSearchAfterAdd();
        }
    }

    private void OnSearchMatchDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        // Headers, scrollbars, empty space and photo-gallery buttons are not Add gestures.
        var row = FindParent<DataGridRow>(source);
        if (row is null || !ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(row), SearchMatchesGrid)
            || FindParent<ButtonBase>(source) is not null) return;
        e.Handled = true;
        SearchMatchesGrid.SelectedItem = row.Item;
        AddFocusedSearchMatch();
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match) return match;
            // ComboBox choices live in a Popup visual tree; retain their owning input for Escape/navigation.
            if (child is ComboBoxItem item && ItemsControl.ItemsControlFromItemContainer(item) is ComboBox owner)
                child = owner;
            else
                child = child is Visual ? VisualTreeHelper.GetParent(child) : LogicalTreeHelper.GetParent(child);
        }
        return null;
    }

    private static DataGridCell? FirstGridCell(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is DataGridCell cell) return cell;
            var nested = FirstGridCell(child);
            if (nested is not null) return nested;
        }
        return null;
    }

    private static bool FocusGrid(DataGrid grid)
    {
        if (!grid.IsVisible || !grid.IsEnabled) return false;
        if (grid.Items.Count == 0) return grid.Focus();
        if (grid.SelectedItem is null) grid.SelectedIndex = 0;
        grid.ScrollIntoView(grid.SelectedItem);
        if (grid.Columns.Count > 0)
            grid.CurrentCell = new DataGridCellInfo(grid.SelectedItem, grid.Columns[0]);
        grid.UpdateLayout();
        var row = grid.ItemContainerGenerator.ContainerFromItem(grid.SelectedItem) as DataGridRow;
        // Focus the cell, not the card's photo button; Enter on the row must Add, not open photos.
        return (row is not null && FirstGridCell(row)?.Focus() == true) || grid.Focus();
    }

    private bool FocusSection(FrameworkElement section)
    {
        if (ReferenceEquals(section, SearchEntryPanel)) return MedicineSearchBox.Focus();
        if (ReferenceEquals(section, SearchResultsPanel)) return FocusGrid(SearchMatchesGrid);
        if (ReferenceEquals(section, CartPanel)) return FocusGrid(CartGrid);
        if (ReferenceEquals(section, CheckoutFields)) return CustomerPicker.Focus();
        if (ReferenceEquals(section, CheckoutMoney))
            return CashReceivedBox.IsEnabled ? CashReceivedBox.Focus() : CompleteSaleButton.Focus();
        return section.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }

    private FrameworkElement[] CounterSections() => [SearchEntryPanel, SearchResultsPanel, CartPanel,
        CheckoutDetailsPanel, ParkedCartsPanel, CheckoutFields, CheckoutMoney, CounterActions];

    private bool MoveCounterSection(Key key)
    {
        // Stable reading order. Collapsed details and empty/hidden matches are skipped.
        var sections = CounterSections();
        var combo = FindParent<ComboBox>(Keyboard.FocusedElement as DependencyObject);
        var current = Array.FindIndex(sections, section => section.IsKeyboardFocusWithin
            || (combo is not null && section.IsAncestorOf(combo)));
        if (current < 0) return false; // Do not hijack another window or the application's shell.
        if (CartGrid.IsKeyboardFocusWithin
            && (!CartGrid.CommitEdit(DataGridEditingUnit.Cell, true)
                || !CartGrid.CommitEdit(DataGridEditingUnit.Row, true))) return true;
        // Leave a ComboBox cleanly: subsequent arrows must not keep editing its old popup.
        if (combo is not null) combo.IsDropDownOpen = false;
        var step = key is Key.Up or Key.Left ? -1 : 1;
        for (var index = current + step; index >= 0 && index < sections.Length; index += step)
        {
            var section = sections[index];
            if (!section.IsVisible || !section.IsEnabled) continue;
            if (ReferenceEquals(section, SearchResultsPanel)
                && (!SearchMatchesGrid.IsVisible || SearchMatchesGrid.Items.Count == 0)) continue;
            if (FocusSection(section)) return true;
        }
        return true; // At the boundary, stay in this section instead of selecting textbox text.
    }

    private void OnCounterPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is PosViewModel { IsBlocked: true }) return;
        var source = e.OriginalSource as DependencyObject;
        var arrow = e.Key is Key.Up or Key.Down or Key.Left or Key.Right;
        if (arrow && Keyboard.Modifiers == ModifierKeys.Shift)
        {
            e.Handled = MoveCounterSection(e.Key);
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.None)
        {
            if (e.Key == Key.Down && MedicineSearchBox.IsKeyboardFocusWithin
                && SearchMatchesGrid.IsVisible && SearchMatchesGrid.Items.Count > 0)
            {
                e.Handled = FocusGrid(SearchMatchesGrid);
                return;
            }
            if (e.Key == Key.Enter && SearchMatchesGrid.IsKeyboardFocusWithin
                && FindParent<ButtonBase>(source) is null)
            {
                e.Handled = true;
                if (!e.IsRepeat) AddFocusedSearchMatch();
                return;
            }
            // Native TextBox caret, ComboBox choices and DataGrid cell/row arrows are untouched.
            // Buttons/checks have no inner choices, so arrows may move to the next visible control.
            if (arrow && FindParent<ButtonBase>(source) is ButtonBase button)
            {
                var direction = e.Key switch
                {
                    Key.Up => FocusNavigationDirection.Up,
                    Key.Down => FocusNavigationDirection.Down,
                    Key.Left => FocusNavigationDirection.Left,
                    _ => FocusNavigationDirection.Right
                };
                var section = Array.Find(CounterSections(), candidate => candidate.IsKeyboardFocusWithin);
                if (section is not null && button.PredictFocus(direction) is UIElement target
                    && section.IsAncestorOf(target)) target.Focus();
                e.Handled = true; // Use Shift+arrow (or Tab) to leave this section.
                return;
            }
            if (e.Key == Key.Enter && FindParent<CheckBox>(source) is CheckBox checkBox)
            {
                e.Handled = true;
                if (!e.IsRepeat) checkBox.IsChecked = checkBox.IsChecked != true;
                return;
            }
        }
        // Escape first belongs to an open unit/customer/payment list, not hidden search results.
        if (e.Key == Key.Escape && FindParent<ComboBox>(source) is ComboBox { IsDropDownOpen: true }) return;
        if (e.Key == Key.Escape && DataContext is PosViewModel searchModel && searchModel.IsSearchResultsOpen)
        {
            var returnToSearch = SearchMatchesGrid.IsKeyboardFocusWithin;
            searchModel.CloseSearchResultsCommand.Execute(null);
            if (returnToSearch) MedicineSearchBox.Focus();
            e.Handled = true;
            return;
        }
        // Preview catches F4 before a focused ComboBox uses it to open its dropdown.
        // This is only a shortcut to the existing guarded command, never another save path.
        if (e.Key != Key.F4 || Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }
        e.Handled = true;
        if (e.IsRepeat || DataContext is not PosViewModel viewModel)
        {
            return;
        }
        if (!CartGrid.CommitEdit(DataGridEditingUnit.Cell, true)
            || !CartGrid.CommitEdit(DataGridEditingUnit.Row, true))
        {
            return;
        }
        if (viewModel.CompleteCommand.CanExecute(null))
        {
            viewModel.CompleteCommand.Execute(null);
        }
    }
}
