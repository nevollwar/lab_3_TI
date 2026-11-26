using System.Windows.Input;
using PaymentApp.Domain;

namespace WpEngine.UI.ViewModels.Payment;

public abstract class OperationViewModelBase : ViewModelBase
{
    public abstract string Title { get; }
    public abstract ContractInfo Contract { get; }

    private bool _isPreValid;
    public bool IsPreValid
    {
        get => _isPreValid;
        set => SetProperty(ref _isPreValid, value);
    }

    private bool? _isPostValid;
    public bool? IsPostValid
    {
        get => _isPostValid;
        set => SetProperty(ref _isPostValid, value);
    }

    private string _statusMessage = "Заполните параметры операции";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand ExecuteCommand { get; }

    protected OperationViewModelBase()
    {
        ExecuteCommand = new RelayCommand(RunOperation, () => IsPreValid);
    }

    public abstract void CheckPreconditions();
    public abstract void Execute();
    public virtual void RefreshAccounts() { }

    private void RunOperation()
    {
        CheckPreconditions();
        if (!IsPreValid)
        {
            return;
        }

        try
        {
            Execute();
            IsPostValid = true;
            StatusMessage = "Успешно: операция выполнена, постусловия истинны.";
        }
        catch (Exception ex)
        {
            IsPostValid = false;
            StatusMessage = "Ошибка выполнения: " + ex.Message;
        }
    }
}
