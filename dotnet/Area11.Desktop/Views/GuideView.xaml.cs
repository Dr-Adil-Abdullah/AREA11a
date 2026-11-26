using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Area11.Core.Utils;

namespace Area11.Desktop.Views;

/// <summary>Native, read-only viewer. Constructed only by an explicit Guidelines/Docs request.</summary>
public partial class GuideView : UserControl
{
    private GuideLibrary? _library;
    private readonly Dictionary<string, TreeViewItem> _treeItems = new(StringComparer.Ordinal);
    private bool _changing;
    public event EventHandler? CloseRequested;

    public GuideView()
    {
        InitializeComponent();
        try
        {
            _library = GuideLibrary.Load(Path.Combine(AppContext.BaseDirectory, "Guide", "guide.json"));
            MetadataText.Text = $"Docs {_library.Version} · {_library.Date} · چھ حصے؛ 96 موضوعات";
            var number = 0;
            foreach (var root in _library.Roots)
            {
                TopicTree.Items.Add(CreateBranch(root));
                var card = TopicButton(root, true, (++number).ToString("00"));
                card.MinHeight = 150;
                RootGrid.Children.Add(card);
            }
            ShowHome();
        }
        catch (Exception)
        {
            MetadataText.Text = "گائیڈ کا مواد نہیں کھل سکا۔";
            StatusText.Text = "Guide/guide.json کی فائل یا اشاعت چیک کریں۔ اصل کاروباری فارم نہیں بدلا گیا۔";
            SearchBox.IsEnabled = false;
        }
    }

    internal void FocusSearch() => Dispatcher.BeginInvoke(new Action(() => SearchBox.Focus()));

    private Brush GuideBrush(string key) => (Brush)FindResource(key);

    private TreeViewItem CreateBranch(GuideTopic topic)
    {
        var children = _library!.Children(topic.Id);
        var label = new TextBlock
        {
            Text = topic.Title, TextWrapping = TextWrapping.Wrap, Width = 174,
            FontWeight = topic.Parent.Length == 0 ? FontWeights.SemiBold : FontWeights.Normal,
            ToolTip = topic.Path
        };
        var item = new TreeViewItem
        {
            Header = label, Tag = topic, Style = (Style)FindResource("GuideTreeItem"),
            ItemContainerStyle = (Style)FindResource("GuideTreeItem")
        };
        AutomationProperties.SetName(item, topic.Title);
        AutomationProperties.SetHelpText(item, children.Count == 0 ? "مضمون" : $"{children.Count} ذیلی موضوعات");
        _treeItems.Add(topic.Id, item);
        foreach (var child in children) item.Items.Add(CreateBranch(child));
        return item;
    }

    public void ShowTopic(string id)
    {
        if (_library is null) return;
        var topic = _library.Find(id);
        if (topic is null)
        {
            ShowHome();
            StatusText.Text = "اس جگہ کا موضوع نہیں ملا؛ چھ حصوں کی فہرست سے منتخب کریں۔";
            return;
        }
        Landing.Visibility = Visibility.Collapsed;
        ArticlePanel.Visibility = Visibility.Visible;
        ArticleTitle.Text = topic.Title;
        PathText.Text = topic.Path;
        ShowBreadcrumbs(topic);
        SelectBranch(topic);

        var children = _library.Children(id);
        ChildPanel.Children.Clear();
        ChildrenSection.Visibility = children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ChildrenTitle.Text = $"اس حصے کے موضوعات  ·  {children.Count}";
        ChildrenSection.IsExpanded = true;
        foreach (var child in children)
        {
            var card = TopicButton(child);
            card.Width = 220;
            ChildPanel.Children.Add(card);
        }
        // Related links are NOT children: keep their navigation visually separate from the directory.
        RelatedPanel.Children.Clear();
        foreach (var linked in topic.Links.Select(link => _library.Find(link)).OfType<GuideTopic>()
                     .Where(link => link.Id != topic.Parent && children.All(child => child.Id != link.Id)).DistinctBy(link => link.Id))
            RelatedPanel.Children.Add(NavigationButton(linked.Title, () => ShowTopic(linked.Id)));
        RelatedSection.Visibility = RelatedPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        var document = new FlowDocument
        {
            FlowDirection = FlowDirection.RightToLeft, FontFamily = new FontFamily("Segoe UI"),
            FontSize = 17, LineHeight = 30, PagePadding = new Thickness(6, 4, 6, 16),
            Background = GuideBrush("SurfaceBrush"), Foreground = GuideBrush("TextBrush")
        };
        foreach (var block in topic.Blocks)
        {
            if (block.Kind == "table")
            {
                var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 8, 0, 18), FontSize = 15 };
                var group = new TableRowGroup();
                var index = 0;
                foreach (var cells in block.Rows)
                {
                    var row = new TableRow
                    {
                        Background = GuideBrush(index == 0 ? "BrandSoftBrush" : index % 2 == 0 ? "CanvasBrush" : "SurfaceBrush"),
                        FontWeight = index == 0 ? FontWeights.SemiBold : FontWeights.Normal
                    };
                    foreach (var text in cells)
                        row.Cells.Add(new TableCell(new Paragraph(new Run(text)) { Margin = new Thickness(0) })
                        {
                            Padding = new Thickness(10, 7, 10, 7), BorderBrush = GuideBrush("LineBrush"), BorderThickness = new Thickness(0.5)
                        });
                    group.Rows.Add(row);
                    index++;
                }
                table.RowGroups.Add(group);
                document.Blocks.Add(table);
            }
            else
                document.Blocks.Add(new Paragraph(new Run(block.Kind == "item" && !char.IsDigit(block.Text.FirstOrDefault()) ? "• " + block.Text : block.Text))
                {
                    FontSize = block.Kind == "heading" ? 20 : 17,
                    FontWeight = block.Kind == "heading" ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = GuideBrush(block.Kind == "heading" ? "BrandDarkBrush" : "TextBrush"),
                    Margin = block.Kind == "heading" ? new Thickness(0, 20, 0, 7) : new Thickness(0, 0, 0, 10)
                });
        }
        Reader.Document = document;
    }

    private void SelectBranch(GuideTopic topic)
    {
        // Also used by cards, breadcrumbs, search results and contextual Docs. Suppress recursive selection events.
        var wasChanging = _changing;
        _changing = true;
        try
        {
            for (GuideTopic? ancestor = topic; ancestor is not null; ancestor = _library!.Find(ancestor.Parent))
                if (_treeItems.TryGetValue(ancestor.Id, out var branch) && branch.HasItems) branch.IsExpanded = true;
            if (_treeItems.TryGetValue(topic.Id, out var selected))
            {
                selected.IsSelected = true;
                if (TopicTree.IsVisible) selected.BringIntoView();
            }
        }
        finally { _changing = wasChanging; }
    }

    private Button NavigationButton(string title, Action navigate)
    {
        var button = new Button
        {
            Content = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap },
            Style = (Style)FindResource("GhostButton"), FontSize = 13,
            Padding = new Thickness(9, 5, 9, 5), Margin = new Thickness(2)
        };
        button.Click += (_, _) => navigate();
        return button;
    }

    private void ShowBreadcrumbs(GuideTopic topic)
    {
        BreadcrumbPanel.Children.Clear();
        BreadcrumbPanel.Children.Add(NavigationButton("گائیڈ", ShowHome));
        var trail = new Stack<GuideTopic>();
        for (GuideTopic? current = topic; current is not null; current = _library!.Find(current.Parent)) trail.Push(current);
        foreach (var ancestor in trail)
        {
            BreadcrumbPanel.Children.Add(new TextBlock { Text = "/", Margin = new Thickness(3, 7, 3, 0), Foreground = GuideBrush("MutedBrush") });
            if (ancestor.Id == topic.Id)
                BreadcrumbPanel.Children.Add(new TextBlock
                {
                    Text = ancestor.Title, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 7, 6, 0),
                    FontWeight = FontWeights.SemiBold, Foreground = GuideBrush("BrandDarkBrush")
                });
            else
                BreadcrumbPanel.Children.Add(NavigationButton(ancestor.Title, () => ShowTopic(ancestor.Id)));
        }
    }

    private Button TopicButton(GuideTopic topic, bool root = false, string number = "")
    {
        var children = _library!.Children(topic.Id);
        var content = new StackPanel();
        if (root) content.Children.Add(new TextBlock
        {
            Text = number + "  /  " + children.Count + " ذیلی موضوعات", FontSize = 12,
            Foreground = GuideBrush("BrandDarkBrush"), Margin = new Thickness(0, 0, 0, 10)
        });
        content.Children.Add(new TextBlock
        {
            Text = topic.Title, TextWrapping = TextWrapping.Wrap, FontSize = root ? 21 : 16,
            FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6)
        });
        content.Children.Add(new TextBlock
        {
            Text = root ? topic.Blocks.FirstOrDefault()?.Text : children.Count > 0 ? $"{children.Count} ذیلی موضوعات  ←" : "مضمون پڑھیں  ←",
            TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = GuideBrush("MutedBrush")
        });
        var button = new Button { Content = content, Style = (Style)FindResource("GuideCardButton"), ToolTip = topic.Path };
        AutomationProperties.SetName(button, topic.Title);
        button.Click += (_, _) => ShowTopic(topic.Id);
        return button;
    }

    private void ShowHome()
    {
        if (_library is null) return;
        _changing = true;
        try
        {
            SearchBox.Clear();
            SearchResults.ItemsSource = null;
            SearchResults.Visibility = Visibility.Collapsed;
            TopicTree.Visibility = Visibility.Visible;
            foreach (var branch in _treeItems.Values) { branch.IsSelected = false; branch.IsExpanded = false; }
        }
        finally { _changing = false; }
        Reader.Document = new FlowDocument();
        ChildPanel.Children.Clear();
        RelatedPanel.Children.Clear();
        BreadcrumbPanel.Children.Clear();
        ArticlePanel.Visibility = Visibility.Collapsed;
        Landing.Visibility = Visibility.Visible;
        StatusText.Text = "حصہ منتخب کریں؛ تیر سے اس کی ذیلی فہرست کھولیں۔";
    }

    private void OnHome(object sender, RoutedEventArgs e) => ShowHome();
    private void OnClose(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
    private void OnClearSearch(object sender, RoutedEventArgs e) { SearchBox.Clear(); SearchBox.Focus(); }
    private void OnSearch(object sender, TextChangedEventArgs e)
    {
        if (_changing || _library is null) return;
        var searching = !string.IsNullOrWhiteSpace(SearchBox.Text);
        _changing = true;
        try
        {
            SearchResults.ItemsSource = searching ? _library.Search(SearchBox.Text) : null;
            SearchResults.SelectedItem = null;
            SearchResults.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
            TopicTree.Visibility = searching ? Visibility.Collapsed : Visibility.Visible;
        }
        finally { _changing = false; }
        StatusText.Text = searching
            ? $"{SearchResults.Items.Count} نتائج؛ فہرست واپس دیکھنے کیلئے تلاش صاف کریں۔"
            : "حصہ منتخب کریں؛ تیر سے اس کی ذیلی فہرست کھولیں۔";
    }
    private void OnTreeSelection(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (!_changing && e.NewValue is TreeViewItem { Tag: GuideTopic topic }) ShowTopic(topic.Id);
    }
    private void OnSearchSelection(object sender, SelectionChangedEventArgs e)
    {
        if (!_changing && SearchResults.SelectedItem is GuideTopic topic) ShowTopic(topic.Id);
    }
}
