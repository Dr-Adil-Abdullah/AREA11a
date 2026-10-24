using System.Windows.Controls;

namespace Area11.Desktop.Views;

/// <summary>
/// Code-behind stays empty. The three tabs are the existing customer, recovery and supplier views,
/// fed by PartnersViewModel; every rule still lives in the services they call.
/// </summary>
public partial class PartnersView : UserControl
{
    public PartnersView()
    {
        InitializeComponent();
    }
}
