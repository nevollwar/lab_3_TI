using System.Collections.ObjectModel;
using System.Globalization;
using PaymentApp.Domain;

namespace WpEngine.UI.ViewModels.Payment;

public class TransferOperationViewModel : OperationViewModelBase
{
    private readonly Bank _bank;

    public override string Title => "Перевод со счёта на счёт";

    public override ContractInfo Contract { get; } = new ContractInfo(
        "Перевод со счёта на счёт",
        "1) from ≠ null ∧ to ≠ null (оба счёта выбраны и принадлежат системе)\n" +
        "2) from ≠ to\n" +
        "3) amount > 0\n" +
        "4) from.Balance ≥ amount",
        "1) from.Balance' = from.Balance − amount\n" +
        "2) to.Balance' = to.Balance + amount\n" +
        "3) |Journal'| = |Journal| + 1, последняя запись = (from, to, amount)\n" +
        "4) Σ Balance' = Σ Balance (инвариант «сумма балансов системы»)",
        "Эффекты: меняются балансы ровно двух счетов — источника и получателя, " +
        "в журнал операций добавляется одна запись; остальные счета не изменяются.\n" +
        "Исключения: при нарушении любого предусловия Guard.Requires выбрасывает " +
        "PreconditionException, состояние системы не меняется, кнопка «Выполнить» в UI недоступна. " +
        "Нарушение постусловия ловит Debug.Assert, индикатор Post становится красным.",
        "Иванов (10 000,00) переводит Петрову (2 500,00) сумму 10 000,00. " +
        "После перевода: Иванов 0,00, Петров 12 500,00, сумма балансов не изменилась, в журнале +1 запись.",
        "Петров (2 500,00) переводит сумму 2 500,01 — больше баланса. " +
        "Guard.Requires выбрасывает PreconditionException, балансы и журнал не меняются.");

    public ObservableCollection<AccountViewModel> Accounts { get; }
    public ObservableCollection<string> JournalLines { get; } = new();

    public string TotalBalanceText => $"{_bank.TotalBalance:N2} ₽";

    private AccountViewModel? _fromAccount;
    public AccountViewModel? FromAccount
    {
        get => _fromAccount;
        set
        {
            if (SetProperty(ref _fromAccount, value))
            {
                OnInputChanged();
            }
        }
    }

    private AccountViewModel? _toAccount;
    public AccountViewModel? ToAccount
    {
        get => _toAccount;
        set
        {
            if (SetProperty(ref _toAccount, value))
            {
                OnInputChanged();
            }
        }
    }

    private string _amountText = "";
    public string AmountText
    {
        get => _amountText;
        set
        {
            if (SetProperty(ref _amountText, value))
            {
                OnInputChanged();
            }
        }
    }

    private string _lastResultText = "Перевод ещё не выполнялся.";
    public string LastResultText
    {
        get => _lastResultText;
        set => SetProperty(ref _lastResultText, value);
    }

    public TransferOperationViewModel(Bank bank)
    {
        _bank = bank;
        Accounts = new ObservableCollection<AccountViewModel>(bank.Accounts.Select(a => new AccountViewModel(a)));
        CheckPreconditions();
    }

    public override void RefreshAccounts()
    {
        foreach (AccountViewModel account in Accounts)
        {
            account.Refresh();
        }
        OnPropertyChanged(nameof(TotalBalanceText));
        CheckPreconditions();
    }

    public override void CheckPreconditions()
    {
        string? violation = TryParseAmount(AmountText, out decimal amount)
            ? _bank.FindTransferPreViolation(FromAccount?.Model, ToAccount?.Model, amount)
            : "сумма должна быть числом (например, 1500 или 99,90)";

        IsPreValid = violation == null;
        StatusMessage = violation == null
            ? "Все предусловия выполнены — можно выполнить перевод."
            : "Предусловие нарушено: " + violation;
    }

    public override void Execute()
    {
        TryParseAmount(AmountText, out decimal amount);
        TransferResult result = _bank.Transfer(FromAccount!.Model, ToAccount!.Model, amount);

        RefreshAccounts();

        TransactionRecord record = result.Record;
        JournalLines.Insert(0, $"{record.Timestamp:HH:mm:ss}  {record.FromAccount} → {record.ToAccount}: {record.Amount:N2} ₽");

        LastResultText =
            $"Отправитель: {result.FromBalanceBefore:N2} → {result.FromBalanceAfter:N2} (−{amount:N2})\n" +
            $"Получатель: {result.ToBalanceBefore:N2} → {result.ToBalanceAfter:N2} (+{amount:N2})\n" +
            $"Сумма балансов системы: {result.TotalBefore:N2} → {result.TotalAfter:N2}";

        if (!result.PostconditionHolds)
        {
            throw new InvalidOperationException("постусловие перевода не выполнено");
        }
    }

    private void OnInputChanged()
    {
        IsPostValid = null;
        CheckPreconditions();
    }

    private static bool TryParseAmount(string text, out decimal amount)
    {
        string normalized = (text ?? "").Trim().Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out amount);
    }
}
