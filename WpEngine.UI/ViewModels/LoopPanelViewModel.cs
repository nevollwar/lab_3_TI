using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using PaymentApp.Domain;
using WpEngine.Core.Loops;
using WpEngine.UI.Models;

namespace WpEngine.UI.ViewModels;

/// <summary>
/// ViewModel для расширенной панели цикла ЛР-3:
/// управление данными + пошаговое исполнение + блок «Инвариант и вариант» + блок «Проверка сохранения (wp)».
/// Не изменяет LoopViewModel, создаётся параллельно как LoopPanelVM в Window.Resources.
/// </summary>
public class LoopPanelViewModel : ViewModelBase
{
    //зависимости
    private readonly LoopRunner   _runner   = new();
    private readonly LoopVerifier _verifier = new();
    private Bank? _bank;
    private bool _isInitializingRunner;
    private bool _isRunning;
    private int  _runVersion;

    //параметры генерации (реалистичные банковские суммы в ₽)
    private int      _n         = 6;
    private int      _min       = 500;
    private int      _max       = 10000;
    private int      _threshold = 5000;
    private LoopMode _selectedMode = LoopMode.PrefixSum;

    //банковский контекст и мост с ЛР-1
    private string _scenarioContext     = string.Empty;
    private string _dbTransactionsBadge = "Из ЛР-1 (БД)";

    //состояние исполнения
    private int    _currentIndex;
    private int    _result;
    private int    _variantT;
    private bool   _isFinished;
    private bool   _postHeld;
    private string _statusLine = string.Empty;

    //инвариант
    private string _invariantFormula   = string.Empty;
    private string _invariantLatex     = string.Empty;
    private string _invariantWords     = string.Empty;
    private string _variantFormula     = string.Empty;
    private string _variantLatex       = string.Empty;
    private string _variantWords       = string.Empty;
    private bool   _invariantBeforeStep = true;
    private bool   _invariantAfterStep  = true;
    private bool   _hasSteppedOnce;
    private int    _variantMax         = 1;   // для прогресс-бара (= n)

    //блок wp
    private string _wpPreservationFormula      = string.Empty;
    private string _wpPreservationLatex        = string.Empty;
    private string _wpPreservationExplanation  = string.Empty;
    private bool   _verificationDone;
    private string _verificationSummary        = string.Empty;
    private bool   _allConditionsValid;

    //  Публичные свойства

    // параметры
    public int N
    {
        get => _n;
        set { if (SetProperty(ref _n, value)) VariantMax = Math.Max(1, value); }
    }
    public int Min       { get => _min;       set => SetProperty(ref _min, value); }
    public int Max       { get => _max;       set => SetProperty(ref _max, value); }
    public int Threshold
    {
        get => _threshold;
        set
        {
            if (SetProperty(ref _threshold, value))
            {
                ResetLoop();
            }
        }
    }

    public LoopMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (SetProperty(ref _selectedMode, value))
            {
                OnPropertyChanged(nameof(IsThresholdVisible));
                OnPropertyChanged(nameof(ResultUnit));
                ResetLoop();
                ApplySpec();
                RunVerification();
            }
        }
    }

    public bool IsThresholdVisible => SelectedMode == LoopMode.CountGreaterThan;
    public string ResultUnit => SelectedMode == LoopMode.CountGreaterThan ? "шт." : "₽";

    // массив 
    public ObservableCollection<ArrayItem2> Array { get; } = new();

    // состояние исполнения
    public int  CurrentIndex { get => _currentIndex; private set => SetProperty(ref _currentIndex, value); }
    public int  Result       { get => _result;       private set => SetProperty(ref _result, value); }
    public int  VariantT     { get => _variantT;     private set => SetProperty(ref _variantT, value); }
    public bool IsFinished   { get => _isFinished;   private set => SetProperty(ref _isFinished, value); }
    public bool PostHeld     { get => _postHeld;     private set => SetProperty(ref _postHeld, value); }
    public string StatusLine { get => _statusLine;   private set => SetProperty(ref _statusLine, value); }

    //инвариант и вариант
    public string InvariantFormula  { get => _invariantFormula;  private set => SetProperty(ref _invariantFormula, value); }
    public string InvariantLatex    { get => _invariantLatex;    private set => SetProperty(ref _invariantLatex, value); }
    public string InvariantWords    { get => _invariantWords;    private set => SetProperty(ref _invariantWords, value); }
    public string VariantFormula    { get => _variantFormula;    private set => SetProperty(ref _variantFormula, value); }
    public string VariantLatex      { get => _variantLatex;      private set => SetProperty(ref _variantLatex, value); }
    public string VariantWords      { get => _variantWords;      private set => SetProperty(ref _variantWords, value); }

    /// <summary>Инвариант истинен ДО шага (зелёный до и во время шагов).</summary>
    public bool InvariantBeforeStep { get => _invariantBeforeStep; private set => SetProperty(ref _invariantBeforeStep, value); }
    /// <summary>Инвариант истинен ПОСЛЕ шага (зелёный на промежуточных шагах, красный после завершения всех шагов).</summary>
    public bool InvariantAfterStep  { get => _invariantAfterStep;  private set => SetProperty(ref _invariantAfterStep, value); }
    /// <summary>Показывать таблицу истории шагов после первого шага.</summary>
    public bool HasSteppedOnce      { get => _hasSteppedOnce;      private set => SetProperty(ref _hasSteppedOnce, value); }

    /// <summary>Цвет индикатора «После шага»: зелёный пока цикл идёт, красный после завершения всех шагов (j = n).</summary>
    public bool InvariantAfterIsGreen => InvariantAfterStep && !IsFinished;

    /// <summary>Подпись индикатора «После шага».</summary>
    public string InvariantAfterStatusText =>
        IsFinished
            ? "✗ Цикл завершён (¬B)"
            : !HasSteppedOnce
                ? "✓ Готов к шагу"
                : InvariantAfterStep
                    ? "✓ Истинен"
                    : "✗ Нарушен";

    public int VariantMax
    {
        get => _variantMax;
        private set => SetProperty(ref _variantMax, Math.Max(1, value));
    }

    //блок wp
    public string WpPreservationFormula     { get => _wpPreservationFormula;     private set => SetProperty(ref _wpPreservationFormula, value); }
    public string WpPreservationLatex       { get => _wpPreservationLatex;       private set => SetProperty(ref _wpPreservationLatex, value); }
    public string WpPreservationExplanation { get => _wpPreservationExplanation; private set => SetProperty(ref _wpPreservationExplanation, value); }
    public bool   VerificationDone          { get => _verificationDone;          private set => SetProperty(ref _verificationDone, value); }
    public string VerificationSummary       { get => _verificationSummary;       private set => SetProperty(ref _verificationSummary, value); }
    public bool   AllConditionsValid        { get => _allConditionsValid;         private set => SetProperty(ref _allConditionsValid, value); }

    //банковский контекст и мост с ЛР-1
    public string ScenarioContext     { get => _scenarioContext;     private set => SetProperty(ref _scenarioContext, value); }
    public string DbTransactionsBadge { get => _dbTransactionsBadge; private set => SetProperty(ref _dbTransactionsBadge, value); }

    //коллекции
    public ObservableCollection<LoopStepItemViewModel>      StepHistory  { get; } = new();
    public ObservableCollection<LoopConditionItemViewModel> Conditions   { get; } = new();
    public ObservableCollection<string>                     VariantLog   { get; } = new();

    //доступные режимы для ComboBox
    public IReadOnlyList<LoopModeItem> AvailableModes { get; } =
    [
        new LoopModeItem(LoopMode.PrefixSum,        "Сумма транзакций (PrefixSum)"),
        new LoopModeItem(LoopMode.CountGreaterThan, "Крупные платежи > T (Count > T)"),
        new LoopModeItem(LoopMode.PrefixMax,        "Макс. платёж (PrefixMax)"),
    ];

    //команды
    public ICommand GenerateCommand     { get; }
    public ICommand LoadFromLab1Command { get; }
    public ICommand StepCommand         { get; }
    public ICommand RunCommand          { get; }
    public ICommand ResetCommand        { get; }
    public ICommand VerifyCommand       { get; }

    //  Конструктор

    public LoopPanelViewModel()
    {
        GenerateCommand     = new RelayCommand(_ => Generate());
        LoadFromLab1Command = new RelayCommand(_ => LoadFromLab1());
        StepCommand         = new RelayCommand(_ => Step(),    _ => !_isRunning && !IsFinished && Array.Count > 0);
        RunCommand          = new RelayCommand(async _ => await RunAsync(), _ => !_isRunning && !IsFinished && Array.Count > 0);
        ResetCommand        = new RelayCommand(_ => ResetLoop());
        VerifyCommand       = new RelayCommand(_ => RunVerification());

        Generate();
    }

    //  Мост с ЛР-1 (живой журнал транзакций payment.db)

    public void AttachBank(Bank bank)
    {
        if (_bank is PersistentBank oldPb)
            oldPb.TransactionsChanged -= OnBankTransactionsChanged;

        _bank = bank;

        if (_bank is PersistentBank newPb)
            newPb.TransactionsChanged += OnBankTransactionsChanged;

        RefreshDbBadge();
    }

    private void OnBankTransactionsChanged()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
            dispatcher.Invoke(RefreshDbBadge);
        else
            RefreshDbBadge();
    }

    private void RefreshDbBadge()
    {
        int count = 0;
        try
        {
            if (_bank is PersistentBank pb)
                count = pb.GetPersistedTransactions().Count;
            else if (_bank != null)
                count = _bank.Journal.Count;
        }
        catch
        {
            count = 0;
        }

        DbTransactionsBadge = count > 0
            ? $"Из ЛР-1 ({count})"
            : "Из ЛР-1 (БД)";
    }

    private void LoadFromLab1()
    {
        int[] arr;
        if (_bank is PersistentBank pb)
        {
            var persisted = pb.GetPersistedTransactions();
            arr = persisted
                .Take(50)
                .Reverse()
                .Select(tx => (int)Math.Round(tx.Amount))
                .ToArray();
        }
        else if (_bank != null)
        {
            arr = _bank.Journal
                .TakeLast(50)
                .Select(tx => (int)Math.Round(tx.Amount))
                .ToArray();
        }
        else
        {
            arr = [];
        }

        RefreshDbBadge();

        if (arr.Length == 0)
        {
            StatusLine = "В журнале ЛР-1 (payment.db) пока нет транзакций — проведите перевод, пополнение или оплату на вкладке ЛР-1.";
            return;
        }

        _n = arr.Length;
        OnPropertyChanged(nameof(N));
        InitRunner(arr);
        StatusLine = $"Загружено транзакций из ЛР-1: {arr.Length} шт. Нажмите «Шаг» или «Пуск».";
    }

    //  Генерация и инициализация

    internal void OnArrayCellEdited()
    {
        if (_isInitializingRunner) return;

        var arr = Array.Select(x => x.Value).ToArray();
        if (arr.Length == 0)
            arr = [0];

        InitRunner(arr);
        StatusLine = "Суммы транзакций изменены вручную — цикл сброшен в начальное состояние.";
    }

    private void Generate()
    {
        var rng = new Random();
        int minN = SelectedMode == LoopMode.PrefixMax ? 1 : 0;
        int n = Math.Clamp(N, minN, 50);
        int lo = Math.Min(Min, Max);
        int hi = Math.Max(Min, Max);
        var arr = new int[n];
        bool useHundreds = (long)hi - lo >= 500;
        int loStep = useHundreds ? (int)Math.Ceiling(lo / 100.0) : 0;
        int hiStep = useHundreds ? (int)Math.Floor(hi / 100.0) : -1;
        for (int i = 0; i < n; i++)
        {
            arr[i] = (useHundreds && loStep <= hiStep)
                ? rng.Next(loStep, hiStep + 1) * 100
                : rng.Next(lo, hi + 1);
        }

        InitRunner(arr);
        ApplySpec();
        RunVerification();
    }

    private void InitRunner(int[] arr)
    {
        if (_isInitializingRunner) return;
        _isInitializingRunner = true;
        _runVersion++;
        try
        {
            if (arr.Length == 0 && SelectedMode == LoopMode.PrefixMax)
                arr = [0];

            try
            {
                _runner.Reset(SelectedMode, arr, Threshold);
            }
            catch (ArgumentException)
            {
                // PrefixMax с пустым массивом — добавляем хотя бы один элемент
                arr = [0];
                _runner.Reset(SelectedMode, arr, Threshold);
            }

            RefreshArray();
            StepHistory.Clear();
            VariantLog.Clear();
            HasSteppedOnce = false;
            bool initInv = LoopRunner.CheckInvariant(SelectedMode, arr, _runner.J, _runner.Res, Threshold);
            InvariantBeforeStep = initInv;
            InvariantAfterStep  = initInv;
            VariantMax = Math.Max(1, arr.Length);
            StatusLine = string.Empty;
            UpdateState();
        }
        finally
        {
            _isInitializingRunner = false;
        }
    }

    private void ResetLoop()
    {
        var arr = _runner.Array.Count > 0 ? _runner.Array.ToArray() : [.. Array.Select(a => a.Value)];
        if (arr.Length == 0 && SelectedMode == LoopMode.PrefixMax) arr = [0];
        InitRunner(arr);
    }

    private void ApplySpec()
    {
        ScenarioContext = SelectedMode switch
        {
            LoopMode.PrefixSum =>
                "Сверка итогового оборота по журналу транзакций за день (продолжение ЛР-1).",
            LoopMode.CountGreaterThan =>
                "Подсчёт крупных операций выше порога T ₽ для финансового контроля.",
            LoopMode.PrefixMax =>
                "Поиск крупнейшего платежа дня по непустой выписке (Pre: n ≥ 1).",
            _ => string.Empty
        };

        InvariantLatex = SelectedMode switch
        {
            LoopMode.PrefixSum =>
                @"\text{res} = \sum_{i=0}^{j-1} a[i] \;\land\; 0 \le j \le n",
            LoopMode.CountGreaterThan =>
                @"\text{res} = \left|\{i < j : a[i] > T\}\right| \;\land\; 0 \le j \le n",
            LoopMode.PrefixMax =>
                @"\text{res} = \max(a[0..j)) \;\land\; 1 \le j \le n",
            _ => string.Empty
        };
        VariantLatex = @"t = n - j";

        try
        {
            var spec = _verifier.GetSpec(SelectedMode);
            InvariantFormula = spec.InvariantFormula;
            InvariantWords   = spec.InvariantWords;
            VariantFormula   = spec.VariantFormula;
            VariantWords     = spec.VariantWords;
        }
        catch
        {
            InvariantFormula = string.Empty;
            InvariantWords   = string.Empty;
            VariantFormula   = string.Empty;
            VariantWords     = string.Empty;
        }
    }

    //  Верификация (wp-движок через LoopVerifier)

    private void RunVerification()
    {
        try
        {
            var report = _verifier.VerifyMode(SelectedMode);

            WpPreservationFormula = report.PreservationFormula;
            WpPreservationLatex   = SelectedMode switch
            {
                LoopMode.PrefixSum =>
                    @"( \text{res} = \sum_{i=0}^{j-1} a[i] \land 0 \le j \le n \land j < n ) \Rightarrow \\ (0 \le j < n) \land (\text{res} + a[j] = \sum_{i=0}^{j} a[i]) \land (0 \le j + 1 \le n)",
                LoopMode.CountGreaterThan =>
                    @"(\text{Inv} \land j < n) \Rightarrow (0 \le j < n) \land \\ ((a[j] > T \land \text{res} + 1 = \text{count}(a[0..j+1) > T)) \lor (a[j] \le T \land \text{res} = \text{count}(a[0..j+1) > T)))",
                LoopMode.PrefixMax =>
                    @"(\text{res} = \max(a[0..j)) \land 1 \le j \le n \land j < n) \Rightarrow \\ (0 \le j < n) \land (\max(\text{res}, a[j]) = \max(a[0..j+1))) \land (1 \le j + 1 \le n)",
                _ => ConvertConditionTextToLatex(report.PreservationFormula)
            };
            WpPreservationExplanation = report.PreservationHumanExplanation;
            AllConditionsValid        = report.AllValid;
            VerificationSummary       = report.AllValid
                ? "✓ Все 4 условия тотальной корректности доказаны."
                : "✗ Часть условий не выполнена — проверьте инвариант.";

            Conditions.Clear();
            foreach (var c in report.AllConditions)
            {
                string formulaLatex = c.Id switch
                {
                    "Init"         => @"\text{Pre} \Rightarrow \text{wp}(\text{init}, \text{Inv})",
                    "Preservation" => @"(\text{Inv} \land B) \Rightarrow \text{wp}(S, \text{Inv})",
                    "Exit"         => @"(\text{Inv} \land \neg B) \Rightarrow \text{Post}",
                    "Termination"  => @"(\text{Inv} \land B \Rightarrow t \ge 0) \land (\text{Inv} \land B \Rightarrow \text{wp}(S, t' < t))",
                    "Def"          => @"\text{Def}(\text{Init}) \land \text{Def}(B) \land \text{Def}(S)",
                    _              => ConvertConditionTextToLatex(c.Formula)
                };

                Conditions.Add(new LoopConditionItemViewModel
                {
                    Title                = c.Title,
                    Formula              = c.Formula,
                    ExpandedFormula      = c.ExpandedFormula,
                    FormulaLatex         = formulaLatex,
                    ExpandedFormulaLatex = ConvertConditionTextToLatex(c.ExpandedFormula),
                    HumanExplanation     = c.HumanExplanation,
                    ProofSummary         = c.ProofSummary,
                    IsValid              = c.IsValid,
                });
            }

            VerificationDone = true;
        }
        catch (Exception ex)
        {
            VerificationDone          = false;
            WpPreservationFormula     = string.Empty;
            WpPreservationLatex       = string.Empty;
            WpPreservationExplanation = $"Ошибка верификации: {ex.Message}";
        }
    }

    private static readonly Regex LatexIdentifierRegex = new(
        @"(?<!\\)\b(res|sum|count|max|wp|Inv|Pre|Post|Def|Init|init|true|false|t0)\b",
        RegexOptions.Compiled);

    private static readonly Regex LatexMultiSpaceRegex = new(@"\s{2,}", RegexOptions.Compiled);

    private static string ConvertConditionTextToLatex(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Экранируем фигурные скобки из теоретико-множественной записи до добавления LaTeX-команд с { }
        string s = text
            .Replace("{", @"\{")
            .Replace("}", @"\}");

        // Заменяем ASCII и Unicode логические/математические операторы на WpfMath-совместимый LaTeX
        s = s
            .Replace("=>", @" \Rightarrow ")
            .Replace("⇒", @" \Rightarrow ")
            .Replace("&&", @" \land ")
            .Replace("∧", @" \land ")
            .Replace("||", @" \lor ")
            .Replace("∨", @" \lor ")
            .Replace("<=", @" \le ")
            .Replace("≤", @" \le ")
            .Replace(">=", @" \ge ")
            .Replace("≥", @" \ge ")
            .Replace("!=", @" \neq ")
            .Replace("≠", @" \neq ")
            .Replace("==", " = ")
            .Replace("¬", @"\neg ")
            .Replace("!", @"\neg ")
            .Replace("−", "-")
            .Replace("Σ", @"\sum ");

        // Оборачиваем известные ASCII-идентификаторы в \text{...} (max -> \max, t0 -> t_0)
        s = LatexIdentifierRegex.Replace(s, m => m.Value switch
        {
            "max" => @"\max",
            "t0"  => "t_0",
            _     => $@"\text{{{m.Value}}}"
        });

        s = LatexMultiSpaceRegex.Replace(s, " ").Trim();

        // Двухстрочный перенос после первого \Rightarrow для длинных формул
        if (s.Length > 75)
        {
            const string implies = @"\Rightarrow";
            int idx = s.IndexOf(implies, StringComparison.Ordinal);
            if (idx >= 0)
            {
                int insertPos = idx + implies.Length;
                if (insertPos < s.Length)
                    s = s.Insert(insertPos, @" \\");
            }
        }

        return s;
    }
    //  Пошаговое исполнение
    private void Step()
    {
        if (IsFinished) return;

        try
        {
            var snap = _runner.Step();

            StepHistory.Add(new LoopStepItemViewModel
            {
                StepNumber      = snap.StepNumber,
                J               = snap.J,
                Res             = snap.Res,
                Variant         = snap.Variant,
                InvariantBefore = snap.InvariantBefore,
                InvariantAfter  = snap.InvariantAfter,
                PostHeld        = snap.PostHeld,
                VariantLogEntry = snap.VariantLogEntry,
            });

            VariantLog.Add(snap.VariantLogEntry);

            InvariantBeforeStep = snap.InvariantBefore;
            InvariantAfterStep  = snap.InvariantAfter;
            HasSteppedOnce      = true;

            UpdateState();
            RefreshCurrentCell();

            StatusLine = snap.VariantLogEntry;
        }
        catch (InvalidOperationException ex)
        {
            StatusLine = ex.Message;
        }
    }

    private async Task RunAsync()
    {
        if (_isRunning || IsFinished) return;
        _isRunning = true;
        int startVersion = _runVersion;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            while (!IsFinished && _runVersion == startVersion)
            {
                Step();
                if (IsFinished || _runVersion != startVersion) break;
                await Task.Delay(280);
            }
        }
        finally
        {
            _isRunning = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
    //  Вспомогательные

    private void RefreshArray()
    {
        Array.Clear();
        for (int i = 0; i < _runner.Array.Count; i++)
            Array.Add(new ArrayItem2(i, _runner.Array[i], this));
    }

    private void RefreshCurrentCell()
    {
        foreach (var item in Array)
            item.Refresh();
    }

    private void UpdateState()
    {
        CurrentIndex = _runner.J;
        Result       = _runner.Res;
        VariantT     = _runner.Variant;
        IsFinished   = _runner.IsFinished;
        PostHeld     = IsFinished && LoopRunner.CheckPost(SelectedMode, _runner.Array.ToArray(), _runner.Res, _runner.Threshold);

        OnPropertyChanged(nameof(CurrentIndex));
        OnPropertyChanged(nameof(Result));
        OnPropertyChanged(nameof(VariantT));
        OnPropertyChanged(nameof(IsFinished));
        OnPropertyChanged(nameof(PostHeld));
        OnPropertyChanged(nameof(InvariantAfterIsGreen));
        OnPropertyChanged(nameof(InvariantAfterStatusText));

        foreach (var item in Array)
            item.Refresh();

        CommandManager.InvalidateRequerySuggested();
    }
}

//  Вспомогательные типы

/// <summary>Пара (enum-значение, отображаемое имя) для ComboBox режимов.</summary>
public record LoopModeItem(LoopMode Mode, string DisplayName);

/// <summary>
/// Ячейка массива для новой панели (аналог ArrayItem, но без зависимости от LoopViewModel).
/// </summary>
public class ArrayItem2 : System.ComponentModel.INotifyPropertyChanged
{
    private readonly LoopPanelViewModel _parent;
    private int _value;

    public int Index { get; }

    public int Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                OnPropertyChanged();
                _parent.OnArrayCellEdited();
            }
        }
    }

    public ArrayItem2(int index, int value, LoopPanelViewModel parent)
    {
        Index   = index;
        _value  = value;
        _parent = parent;
    }

    public bool IsCurrent => _parent.CurrentIndex == Index;

    public void Refresh() => OnPropertyChanged(nameof(IsCurrent));

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}
