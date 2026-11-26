using System.Windows;
using System.Windows.Controls;
using WpEngine.UI.ViewModels.Payment;

namespace WpEngine.UI.Views.Payment;

public partial class PaymentModuleView : UserControl
{
    public PaymentModuleView()
    {
        InitializeComponent();
    }

    private void ShowContractButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is PaymentModuleViewModel vm &&
            vm.SelectedOperation?.Contract is { } contract)
        {
            var window = new ContractWindow(contract)
            {
                Owner = Window.GetWindow(this)
            };
            window.ShowDialog();
        }
    }
}
