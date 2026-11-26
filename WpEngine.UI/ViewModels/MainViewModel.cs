using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WpEngine.Core.AST;
using WpEngine.Core.Contracts;
using WpEngine.UI.Models;
using WpEngine.UI.Services;

namespace WpEngine.UI.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly WpRunnerService _engine = new();

    private string _postConditionText = string.Empty;
    private string _postConditionHumanText = string.Empty;
    private string _programCodeText = string.Empty;
    private string _finalConditionShort = string.Empty;
    private string _finalConditionHuman = string.Empty;
    private string _finalConditionLatex = string.Empty;
    private string _hoareTriadText = string.Empty;
    private string _hoareTriadLatex = string.Empty;
    private string _postConditionLatex = string.Empty;
    private string _currentRealLifeAnalogy = string.Empty;
    private string _statusMessage = "Готов к расчёту wp";
    private string _elapsedTimeText = string.Empty;
    private string _errorHeaderText = string.Empty;
    private string _errorMessage = string.Empty;
    private string _errorLineSnippet = string.Empty;
    private int _errorLineNumber;
    private bool _hasErrorSnippet;
    private bool _hasResult;
    private bool _hasError;
    private bool _hasDivisionConstraint;
    private string _divisionConstraintMessage = string.Empty;
    private PresetModel? _selectedPreset;

    public ObservableCollection<PresetModel> Presets { get; } = new();
    public ObservableCollection<WpStepViewModel> TraceSteps { get; } = new();
    public ObservableCollection<CodeLineItem> EditorLines { get; } = new();

    public string PostConditionText
    {
        get => _postConditionText;
        set => SetProperty(ref _postConditionText, value);
    }
    public string PostConditionHumanText
    {
        get => _postConditionHumanText;
        set => SetProperty(ref _postConditionHumanText, value);
    }

    public string ProgramCodeText
    {
        get => _programCodeText;
        set
        {
            if (SetProperty(ref _programCodeText, value))
            {
                SyncEditorLines();
                if (_hasError && _errorLineNumber > 0)
                {
                    RevalidateCodeLive();
                }
            }
        }
    }

    public string FinalConditionShort
    {
        get => _finalConditionShort;
        set => SetProperty(ref _finalConditionShort, value);
    }

    public string FinalConditionLatex
    {
        get => _finalConditionLatex;
        set => SetProperty(ref _finalConditionLatex, value);
    }

    public string FinalConditionHuman
    {
        get => _finalConditionHuman;
        set => SetProperty(ref _finalConditionHuman, value);
    }

    public string HoareTriadText
    {
        get => _hoareTriadText;
        set => SetProperty(ref _hoareTriadText, value);
    }

    public string HoareTriadLatex
    {
        get => _hoareTriadLatex;
        set => SetProperty(ref _hoareTriadLatex, value);
    }

    public string PostConditionLatex
    {
        get => _postConditionLatex;
        set => SetProperty(ref _postConditionLatex, value);
    }

    public string CurrentRealLifeAnalogy
    {
        get => _currentRealLifeAnalogy;
        set => SetProperty(ref _currentRealLifeAnalogy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ElapsedTimeText
    {
        get => _elapsedTimeText;
        set => SetProperty(ref _elapsedTimeText, value);
    }

    public string ErrorHeaderText
    {
        get => _errorHeaderText;
        set => SetProperty(ref _errorHeaderText, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public string ErrorLineSnippet
    {
        get => _errorLineSnippet;
        set => SetProperty(ref _errorLineSnippet, value);
    }

    public int ErrorLineNumber
    {
        get => _errorLineNumber;
        set => SetProperty(ref _errorLineNumber, value);
    }

    public bool HasErrorSnippet
    {
        get => _hasErrorSnippet;
        set => SetProperty(ref _hasErrorSnippet, value);
    }

    public bool HasResult
    {
        get => _hasResult;
        set => SetProperty(ref _hasResult, value);
    }

    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    public bool HasDivisionConstraint
    {
        get => _hasDivisionConstraint;
        set => SetProperty(ref _hasDivisionConstraint, value);
    }

    public string DivisionConstraintMessage
    {
        get => _divisionConstraintMessage;
        set => SetProperty(ref _divisionConstraintMessage, value);
    }

    // --- Свойства вкладки «Конструктор фрагмента» ---

    public PresetModel? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value))
            {
                foreach (var p in Presets)
                {
                    p.IsSelected = ReferenceEquals(p, value);
                }

                if (value != null)
                {
                    ApplyPreset(value);
                }
            }
        }
    }

    // Commands
    public ICommand CalculateCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand CopyTriadCommand { get; }
    public ICommand CopyWpCommand { get; }
    public ICommand CopyLatexCommand { get; }
    public ICommand InsertSnippetCommand { get; }
    public ICommand SelectPresetCommand { get; }
    public ICommand ExportReportCommand { get; }

    public MainViewModel()
    {
        CalculateCommand = new RelayCommand(ExecuteCalculate);
        ClearCommand = new RelayCommand(ExecuteClear);
        CopyTriadCommand = new RelayCommand(ExecuteCopyTriad);
        CopyWpCommand = new RelayCommand(ExecuteCopyWp);
        CopyLatexCommand = new RelayCommand(ExecuteCopyLatex);
        InsertSnippetCommand = new RelayCommand(ExecuteInsertSnippet);
        SelectPresetCommand = new RelayCommand(param =>
        {
            if (param is PresetModel preset)
            {
                SelectedPreset = preset;
                ExecuteCalculate();
            }
        });
        ExportReportCommand = new RelayCommand(ExecuteExportReport, _ => HasResult);

        SyncEditorLines();
        InitPresets();

        if (Presets.Count > 0)
        {
            SelectedPreset = Presets[0];
            ExecuteCalculate();
        }
    }

    private void SyncEditorLines()
    {
        int lineCount = 1;
        if (!string.IsNullOrEmpty(_programCodeText))
        {
            for (int i = 0; i < _programCodeText.Length; i++)
            {
                if (_programCodeText[i] == '\n')
                    lineCount++;
            }
        }

        while (EditorLines.Count < lineCount)
        {
            int nextNum = EditorLines.Count + 1;
            EditorLines.Add(new CodeLineItem
            {
                LineNumber = nextNum,
                IsError = nextNum == _errorLineNumber,
                ErrorMessage = nextNum == _errorLineNumber ? _errorMessage : string.Empty
            });
        }

        while (EditorLines.Count > lineCount)
        {
            EditorLines.RemoveAt(EditorLines.Count - 1);
        }
    }

    private void UpdateLineHighlights(int errorLine, string tooltipText)
    {
        ErrorLineNumber = errorLine;
        SyncEditorLines();
        foreach (var lineItem in EditorLines)
        {
            bool isErr = errorLine > 0 && lineItem.LineNumber == errorLine;
            lineItem.IsError = isErr;
            lineItem.ErrorMessage = isErr ? tooltipText : string.Empty;
        }
    }

    private void RevalidateCodeLive()
    {
        if (string.IsNullOrWhiteSpace(_programCodeText))
        {
            ClearErrorState();
            return;
        }

        try
        {
            _engine.ParseProgram(_programCodeText);
            ClearErrorState();
            StatusMessage = "Синтаксическая ошибка исправлена — нажмите «Рассчитать wp»";
        }
        catch (CodeParseException cex)
        {
            SetCodeError(cex.Line, cex.Column, cex.Message, cex.LineSnippet);
        }
        catch
        {
            // Игнорируем прочие исключения при живом вводе
        }
    }

    private void ClearErrorState()
    {
        HasError = false;
        ErrorHeaderText = string.Empty;
        ErrorMessage = string.Empty;
        ErrorLineSnippet = string.Empty;
        HasErrorSnippet = false;
        UpdateLineHighlights(0, string.Empty);
    }

    private void InitPresets()
    {
        Presets.Add(new PresetModel
        {
            Id = "transfer",
            Title = "1. Перевод (инвариант ЛР-1)",
            Subtitle = "Последовательность S1; S2 · Bank.Transfer",
            Description = "Двухшаговый перевод со счёта на счёт с проверкой отсутствия овердрафта и инварианта общей суммы балансов.",
            RealLifeAnalogy = "Прямая связь с ЛР-1 (Bank.Transfer): перевод со счёта Иванова (srcBal) на счёт Петрова (dstBal). Постусловие требует, чтобы счёт списания не ушёл в минус, а суммарный баланс банка total не изменился.",
            Code = "srcBal := srcBal - amount;\ndstBal := dstBal + amount;",
            PostConditionShort = "(srcBal >= 0) && (srcBal + dstBal == total)",
            PostConditionHuman = "Счёт списания не ушёл в минус и сохранилась сумма балансов банка",
            ExpectedWp = "(srcBal - amount >= 0) && ((srcBal - amount) + (dstBal + amount) == total)"
        });

        Presets.Add(new PresetModel
        {
            Id = "limit",
            Title = "2. Выбор лимита карты",
            Subtitle = "Ветвление if-else · DailyLimitAccount",
            Description = "Выбор наибольшего из базового и акционного лимита операции с проверкой покрытия крупного перевода.",
            RealLifeAnalogy = "Связь с ЛР-1 (DailyLimitAccount): система сравнивает базовый лимит карты baseLimit и промо-лимит promoLimit. Чтобы одобрить крупный перевод свыше 5000 ₽, выбранный максимум обязан превышать 5000 ₽.",
            Code = "if (baseLimit >= promoLimit) {\n    limit := baseLimit;\n} else {\n    limit := promoLimit;\n}",
            PostConditionShort = "limit > 5000",
            PostConditionHuman = "Одобренный лимит карты превышает 5000 ₽",
            ExpectedWp = "((baseLimit >= promoLimit && baseLimit > 5000) || (!(baseLimit >= promoLimit) && promoLimit > 5000))"
        });

        Presets.Add(new PresetModel
        {
            Id = "cashback",
            Title = "3. Оплата услуги и кэшбэк",
            Subtitle = "Ветвление if-else · Bank.PayService",
            Description = "При оплате услуги от 1000 ₽ начисляется мгновенный кэшбэк 50 ₽, иначе списывается полная сумма.",
            RealLifeAnalogy = "Связь с ЛР-1 (Bank.PayService): при оплате интернета или ЖКУ от 1000 ₽ банк сразу возвращает 50 ₽ кэшбэка, поэтому до операции достаточно иметь (amount - 50) ₽, чтобы не уйти в овердрафт.",
            Code = "if (amount >= 1000) {\n    balance := balance - amount + 50;\n} else {\n    balance := balance - amount;\n}",
            PostConditionShort = "balance >= 0",
            PostConditionHuman = "Баланс карты после оплаты услуги не ушёл в минус (без овердрафта)",
            ExpectedWp = "(amount >= 1000 && balance - amount + 50 >= 0) || (!(amount >= 1000) && balance - amount >= 0)"
        });

        Presets.Add(new PresetModel
        {
            Id = "installment",
            Title = "4. Рассрочка платежа (Def)",
            Subtitle = "Ветвление, деление и условие определённости",
            Description = "Проверка первого взноса и расчёт ежемесячного платежа (amount - firstPay) / months с защитой months ≠ 0.",
            RealLifeAnalogy = "Банковский скоринг рассрочки: если баланса хватает на первый взнос firstPay, остаток делится на число месяцев months (wp автоматически требует months ≠ 0), иначе выставляется код отказа err = 1.",
            Code = "if (balance >= firstPay) {\n    monthly := (amount - firstPay) / months;\n} else {\n    err := 1;\n}",
            PostConditionShort = "(monthly <= 5000) || (err == 1)",
            PostConditionHuman = "Ежемесячный платёж не превышает 5000 ₽ при достаточном взносе, либо выставлен отказ err = 1",
            ExpectedWp = "((balance >= firstPay && (months != 0 && ((amount - firstPay) / months <= 5000 || err == 1))) || (!(balance >= firstPay) && (monthly <= 5000 || 1 == 1)))"
        });

        Presets.Add(new PresetModel
        {
            Id = "avgCheck",
            Title = "5. Средний чек дня (→ ЛР-3)",
            Subtitle = "Деление и Def · Мост к аудиту транзакций",
            Description = "Расчёт среднего чека по обороту дня totalSum и числу транзакций txCount с защитой от пустой выписки.",
            RealLifeAnalogy = "Мостик к ЛР-3 (аудит транзакций): при подведении итогов банковского дня суммарный оборот totalSum делится на число операций txCount. Чтобы отчёт не упал при пустом журнале и уложился в дневной лимит 15 000 ₽, wp требует txCount ≠ 0 и 0 < totalSum / txCount ≤ 15000.",
            Code = "avgTx := totalSum / txCount;",
            PostConditionShort = "(avgTx > 0) && (avgTx <= 15000)",
            PostConditionHuman = "Средний чек транзакций за день положителен и не превышает 15 000 ₽",
            ExpectedWp = "txCount != 0 && (totalSum / txCount > 0 && totalSum / txCount <= 15000)"
        });
    }

    private void ApplyPreset(PresetModel preset)
    {
        ClearErrorState();
        ProgramCodeText = preset.Code;
        PostConditionText = preset.PostConditionShort;
        PostConditionHumanText = preset.PostConditionHuman;
        CurrentRealLifeAnalogy = preset.RealLifeAnalogy;
        StatusMessage = $"Выбран пример: {preset.Title}";
    }

    private void ExecuteCalculate()
    {
        ClearErrorState();

        if (string.IsNullOrWhiteSpace(PostConditionText))
        {
            SetGeneralError("Поле постусловия пусто", "Введите целевое постусловие R (например, balance >= 0).");
            return;
        }

        if (string.IsNullOrWhiteSpace(ProgramCodeText))
        {
            SetCodeError(1, 1, "Введите хотя бы один оператор программы (например, balance := balance - amount;).", string.Empty);
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        WpEngine.Core.AST.Condition postAst;
        try
        {
            postAst = _engine.ParseCondition(PostConditionText);
        }
        catch (CodeParseException cex)
        {
            SetGeneralError($"Ошибка в постусловии R (символ {cex.Column})", cex.Message, cex.LineSnippet);
            return;
        }
        catch (Exception ex)
        {
            SetGeneralError("Ошибка в постусловии R", ex.Message);
            return;
        }

        Statement programAst;
        try
        {
            programAst = _engine.ParseProgram(ProgramCodeText);
        }
        catch (CodeParseException cex)
        {
            SetCodeError(cex.Line, cex.Column, cex.Message, cex.LineSnippet);
            return;
        }
        catch (Exception ex)
        {
            SetCodeError(1, 1, ex.Message, string.Empty);
            return;
        }

        try
        {
            WpResult result = _engine.CalculateWp(programAst, postAst);

            FinalConditionShort = result.FinalConditionShort;
            FinalConditionLatex = _engine.ToWrappedLatex(result.FinalCondition);
            PostConditionLatex = _engine.ToWrappedLatex(postAst);
            HoareTriadLatex = _engine.HoareTriadToLatex(result.FinalCondition, programAst, postAst);
            FinalConditionHuman = string.IsNullOrWhiteSpace(PostConditionHumanText)
                ? $"Словами: {result.FinalConditionHuman}"
                : $"Словами: {result.FinalConditionHuman} (цель: {FormatLowerFirst(PostConditionHumanText)})";

            HoareTriadText = result.HoareTriadText;

            TraceSteps.Clear();
            bool foundDiv = false;
            for (int i = 0; i < result.Trace.Count; i++)
            {
                var step = result.Trace[i];
                bool isDiv = step.Description.Contains("деления");
                if (isDiv) foundDiv = true;

                string stepLatex;
                try
                {
                    var parsedStepCond = _engine.ParseCondition(step.IntermediateCondition);
                    stepLatex = _engine.ToWrappedLatex(parsedStepCond);
                }
                catch
                {
                    stepLatex = _engine.WrapLatexString(step.IntermediateCondition);
                }

                string stepRealLifeHint = BuildStepRealLifeHint(step.StatementCode, isDiv);

                TraceSteps.Add(new WpStepViewModel
                {
                    StepIndex = i + 1,
                    StepBadge = $"Шаг {i + 1}",
                    StatementCode = step.StatementCode,
                    IntermediateCondition = step.IntermediateCondition,
                    IntermediateConditionLatex = stepLatex,
                    Description = step.Description,
                    RealLifeHint = stepRealLifeHint,
                    HasDivisionConstraint = isDiv
                });
            }

            if (_selectedPreset != null && ProgramCodeText.Trim() == _selectedPreset.Code.Trim())
            {
                CurrentRealLifeAnalogy = _selectedPreset.RealLifeAnalogy;
            }
            else
            {
                CurrentRealLifeAnalogy = BuildCustomRealLifeAnalogy(programAst, foundDiv);
            }

            HasDivisionConstraint = foundDiv;
            DivisionConstraintMessage = foundDiv
                ? "Условие определенности: в программе выполняется деление, поэтому в предусловие автоматически добавлена защита от деления на ноль (знаменатель ≠ 0)."
                : string.Empty;

            HasResult = true;
            stopwatch.Stop();
            ElapsedTimeText = $"{stopwatch.ElapsedMilliseconds} мс";
            StatusMessage = $"Расчёт выполнен (шагов: {result.Trace.Count}) • {stopwatch.ElapsedMilliseconds} мс";
        }
        catch (Exception ex)
        {
            SetGeneralError("Ошибка вычисления wp", ex.Message);
        }
    }

    private static string BuildStepRealLifeHint(string statementCode, bool isDiv)
    {
        if (statementCode.StartsWith("if "))
        {
            return "Ветвление банковского правила: независимо от того, по какой ветке (Then или Else) пойдёт обработка операции — например, с кэшбэком или по обычному тарифу — контракт гарантирует выполнение цели R.";
        }
        if (isDiv)
        {
            return "Деление с проверкой определённости: как при расчёте ежемесячного платежа по рассрочке или среднего чека по журналу транзакций — сначала убеждаемся, что делитель не равен 0, и только потом подставляем формулу.";
        }
        return "Обратный проход по проводке: зная целевое состояние счёта после этой строки, мы подставляем формулу присваивания вместо переменной и получаем точное требование ДО операции.";
    }

    private static string BuildCustomRealLifeAnalogy(Statement programAst, bool foundDiv)
    {
        if (foundDiv)
        {
            return "Как расчёт платежа по рассрочке или среднего чека дня: формула корректна только тогда, когда делитель (число месяцев или транзакций) не равен нулю, а входные суммы укладываются в заданный лимит.";
        }
        if (programAst is IfStatement)
        {
            return "Как проверка тарифа или дневного лимита счёта: движок анализирует обе ветки банковского правила и выводит точные предусловия на стартовый баланс для каждого сценария.";
        }
        return "Как обратная сверка банковской выписки: зная целевой остаток после серии списаний и зачислений, мы шаг за шагом отматываем проводки назад и находим минимальный начальный баланс.";
    }

    private static string FormatLowerFirst(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        return char.ToLower(text[0]) + text[1..];
    }

    private void SetCodeError(int line, int col, string message, string snippet)
    {
        HasError = true;
        ErrorHeaderText = $"Ошибка в коде — строка {line}, символ {col}";
        ErrorMessage = message;
        ErrorLineSnippet = string.IsNullOrWhiteSpace(snippet) ? string.Empty : $"Строка {line}:  {snippet.Trim()}";
        HasErrorSnippet = !string.IsNullOrWhiteSpace(ErrorLineSnippet);
        UpdateLineHighlights(line, $"Строка {line}, символ {col}: {message}");
        StatusMessage = $"Ошибка синтаксиса в строке {line} (символ {col})";
    }

    private void SetGeneralError(string header, string message, string snippet = "")
    {
        HasError = true;
        ErrorHeaderText = header;
        ErrorMessage = message;
        ErrorLineSnippet = string.IsNullOrWhiteSpace(snippet) ? string.Empty : snippet.Trim();
        HasErrorSnippet = !string.IsNullOrWhiteSpace(ErrorLineSnippet);
        UpdateLineHighlights(0, string.Empty);
        StatusMessage = header;
    }

    private void ExecuteClear()
    {
        ClearErrorState();
        ProgramCodeText = string.Empty;
        PostConditionText = string.Empty;
        PostConditionHumanText = string.Empty;
        FinalConditionShort = string.Empty;
        FinalConditionHuman = string.Empty;
        HoareTriadText = string.Empty;
        CurrentRealLifeAnalogy = string.Empty;
        TraceSteps.Clear();
        HasResult = false;
        HasDivisionConstraint = false;
        SelectedPreset = null;
        StatusMessage = "Поля ввода очищены";
    }

    private void ExecuteCopyTriad()
    {
        if (!string.IsNullOrWhiteSpace(HoareTriadText))
        {
            Clipboard.SetText(HoareTriadText);
            StatusMessage = "Триада Хоара скопирована в буфер обмена";
        }
    }

    private void ExecuteCopyWp()
    {
        if (!string.IsNullOrWhiteSpace(FinalConditionShort))
        {
            Clipboard.SetText(FinalConditionShort);
            StatusMessage = "Формула wp скопирована в буфер обмена";
        }
    }

    private void ExecuteCopyLatex()
    {
        if (!string.IsNullOrWhiteSpace(FinalConditionLatex))
        {
            Clipboard.SetText($"$$ {FinalConditionLatex} $$");
            StatusMessage = "LaTeX-формула ($$ ... $$) скопирована в буфер обмена";
        }
    }

    private void ExecuteExportReport(object? _)
    {
        if (!HasResult)
        {
            StatusMessage = "Сначала выполните расчёт wp перед экспортом";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Экспорт отчёта wp",
            Filter = "Markdown-файл (*.md)|*.md|Все файлы (*.*)|*.*",
            DefaultExt = ".md",
            FileName = $"wp_report_{DateTime.Now:yyyyMMdd_HHmm}"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                ReportExportService.ExportToFile(this, dialog.FileName);
                StatusMessage = $"Отчёт сохранён: {System.IO.Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка экспорта: {ex.Message}";
                MessageBox.Show($"Не удалось сохранить файл:\n{ex.Message}", "Ошибка экспорта",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void ExecuteInsertSnippet(object? parameter)
    {
        if (parameter is string snippet)
        {
            ProgramCodeText = (ProgramCodeText + "\n" + snippet).Trim();
            StatusMessage = $"Вставлен шаблон: {snippet}";
        }
    }
}
