using System.Windows;
using PaymentApp.Domain;

namespace WpEngine.UI.Views.Payment;

public partial class ContractWindow : Window
{
    public ContractWindow(ContractInfo contract)
    {
        InitializeComponent();
        DataContext = contract;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
