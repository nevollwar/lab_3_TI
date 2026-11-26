using System.Windows;
using System.Windows.Controls;
using PaymentApp.Domain;
using WpEngine.UI.ViewModels;

namespace WpEngine.UI.Views;

public partial class LoopsView : UserControl
{
    public LoopsView()
    {
        InitializeComponent();
    }

    public void AttachBank(Bank bank)
    {
        if (DataContext is LoopPanelViewModel vm)
            vm.AttachBank(bank);
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new LoopHelpWindow
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
    }
}
