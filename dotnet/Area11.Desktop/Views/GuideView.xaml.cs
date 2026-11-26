using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Area11.Core.Utils;

namespace Area11.Desktop.Views;

/// <summary>Native, read-only viewer. Constructed only by an explicit Guidelines/Docs request.</summary>
public partial class GuideView : UserControl
{
    private GuideLibrary? _library;
    private bool _changing;
    public event EventHandler? CloseRequested;

    public GuideView()
    {
        InitializeComponent();
        try
        {
            _library = GuideLibrary.Load(Path.Combine(AppContext.BaseDirectory, "Guide", "guide.json"));
            MetadataText.Text = $"Docs {_library.Version} · {_library.Date} · تین مکمل نمونے؛باقی موضوعات placeholders";
            foreach (var root in _library.Roots)
            {
                var card = TopicButton(root, 22);
                card.MinHeight = 90;
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

    public void ShowTopic(string id)
    {
        if (_library is null) return;
        var topic = _library.Find(id);
        if (topic is null)
        {
            ShowHome();
            StatusText.Text = "اس جگہ کا موضوع نہیں ملا؛چھ حصوں کی فہرست سے منتخب کریں۔";
            return;
        }
        Landing.Visibility = Visibility.Collapsed;
        ArticlePanel.Visibility = Visibility.Visible;
        PathText.Text = topic.Path;
        var document = new FlowDocument
        {
            FlowDirection = FlowDirection.RightToLeft, FontFamily = new FontFamily("Segoe UI"),
            FontSize = 18, PagePadding = new Thickness(18), Background = Brushes.White, Foreground = Brushes.Black
        };
        document.Blocks.Add(new Paragraph(new Run(topic.Title)) { FontSize = 27, FontWeight = FontWeights.Bold });
        if (topic.Blocks.Count == 0)
            document.Blocks.Add(new Paragraph(new Run($"یہاں «{topic.Title}» کی معلومات آئے گی۔")));
        foreach (var block in topic.Blocks)
        {
            if (block.Kind == "table")
            {
                var table = new Table { CellSpacing = 0 };
                var group = new TableRowGroup();
                foreach (var cells in block.Rows)
                {
                    var row = new TableRow();
                    foreach (var text in cells)
                        row.Cells.Add(new TableCell(new Paragraph(new Run(text)))
                        {
                            Padding = new Thickness(8), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0.5)
                        });
                    group.Rows.Add(row);
                }
                table.RowGroups.Add(group);
                document.Blocks.Add(table);
            }
            else
                document.Blocks.Add(new Paragraph(new Run((block.Kind == "item" ? "• " : "") + block.Text))
                {
                    FontSize = block.Kind == "heading" ? 22 : 18,
                    FontWeight = block.Kind == "heading" ? FontWeights.Bold : FontWeights.Normal
                });
        }
        Reader.Document = document;
        ChildPanel.Children.Clear();
        foreach (var linked in _library.Children(id).Concat(topic.Links.Select(link => _library.Find(link)).OfType<GuideTopic>()).DistinctBy(item => item.Id))
            ChildPanel.Children.Add(TopicButton(linked, 14));
    }

    private Button TopicButton(GuideTopic topic, double size)
    {
        var button = new Button
        {
            Content = new TextBlock { Text = topic.Title, TextWrapping = TextWrapping.Wrap },
            FontSize = size, Padding = new Thickness(12), Margin = new Thickness(4)
        };
        button.Click += (_, _) => ShowTopic(topic.Id);
        return button;
    }

    private void ShowHome()
    {
        if (_library is null) return;
        _changing = true;
        SearchBox.Clear();
        TopicList.ItemsSource = _library.Roots;
        TopicList.SelectedItem = null;
        _changing = false;
        Reader.Document = new FlowDocument();
        ChildPanel.Children.Clear();
        ArticlePanel.Visibility = Visibility.Collapsed;
        Landing.Visibility = Visibility.Visible;
        PathText.Text = "پہلے حصہ منتخب کریں؛کوئی سبق خود نہیں کھولا گیا۔";
        StatusText.Text = "فہرست،عنوان اورتین تیار مضامین کے متن میں تلاش۔";
    }

    private void OnHome(object sender, RoutedEventArgs e) => ShowHome();
    private void OnClose(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
    private void OnSearch(object sender, TextChangedEventArgs e)
    {
        if (_changing || _library is null) return;
        _changing = true;
        TopicList.ItemsSource = _library.Search(SearchBox.Text);
        TopicList.SelectedItem = null;
        _changing = false;
        StatusText.Text = "تمام چھ حصوں میں تلاش؛نتیجہ خود نہیں کھولا جائے گا۔";
    }
    private void OnSelection(object sender, SelectionChangedEventArgs e)
    {
        if (!_changing && TopicList.SelectedItem is GuideTopic topic) ShowTopic(topic.Id);
    }
}
