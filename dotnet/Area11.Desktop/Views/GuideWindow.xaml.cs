using System.Windows;

namespace Area11.Desktop.Views;

public partial class GuideWindow : Window
{
    public GuideWindow()
    {
        InitializeComponent();
        ContentGuide.CloseRequested += (_, _) => Close();
    }

    public void ShowTopic(string topic) => ContentGuide.ShowTopic(topic);
}
