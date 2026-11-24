using WpEngine.Core.AST;
using WpEngine.Core.Contracts;

namespace WpEngine.Core.Loops;

/// <summary>
/// Поддерживаемые режимы циклов из ЛР-3 («Циклы под микроскопом»).
/// Общий enum для бэкенда (Роль 1 и Роль 2), фронтенда (Роль 3 и Роль 4) и тестов (Роль 5).
/// </summary>
public enum LoopMode
{
    /// <summary>Сумма префикса: res = sum(a[0..j))</summary>
    PrefixSum,

    /// <summary>Счётчик элементов префикса, больших порога T: res = |{i &lt; j : a[i] &gt; T}|</summary>
    CountGreaterThan,

    /// <summary>Максимум префикса: res = max(a[0..j))</summary>
    PrefixMax
}

/// <summary>
/// Полная формальная спецификация цикла вида init; while (B) { S }.
/// Содержит AST-представления для символьного вычисления wp и человекочитаемые описания.
/// </summary>
public record LoopSpec(
    LoopMode Mode,
    string Name,
    string InitCode,
    string LoopGuardText,
    string LoopBodyCode,
    Statement InitStatement,
    Condition LoopGuard,
    Statement LoopBody,
    Condition PreCondition,
    Condition Invariant,
    Condition PostCondition,
    Expr VariantExpr,
    string InvariantFormula,
    string InvariantWords,
    string VariantFormula,
    string VariantWords
);

/// <summary>
/// Результат проверки одного из формальных условий корректности цикла
/// (Инициализация, Сохранение, Выход, Завершение или Определённость Def).
/// </summary>
public record LoopVerificationCondition(
    string Id,
    string Title,
    string Formula,
    string ExpandedFormula,
    string HumanExplanation,
    bool IsValid,
    string ProofSummary
);

/// <summary>
/// Полный отчёт верификации цикла: вычисленное wp(S, Inv), формула (Inv && B) => wp(S, Inv),
/// все 4 условия тотальной корректности, условия определённости Def и текстовый разбор по-русски.
/// </summary>
public record LoopVerificationReport(
    LoopSpec Spec,
    WpResult WpPreservationResult,
    WpResult WpVariantDecreaseResult,
    Condition PreservationImplication,
    string PreservationFormula,
    string PreservationHumanExplanation,
    LoopVerificationCondition Initialization,
    LoopVerificationCondition Preservation,
    LoopVerificationCondition Exit,
    LoopVerificationCondition Termination,
    LoopVerificationCondition Definedness,
    IReadOnlyList<LoopVerificationCondition> AllConditions,
    bool AllValid
);

/// <summary>
/// Контракт сервиса формальной верификации циклов через wp-движок (Роль 2).
/// </summary>
public interface ILoopVerifier
{
    /// <summary>
    /// Возвращает формальную спецификацию (Pre, Inv, Post, B, S, t) для выбранного режима.
    /// </summary>
    LoopSpec GetSpec(LoopMode mode);

    /// <summary>
    /// Выполняет полную формальную проверку цикла для одного из стандартных режимов ЛР-3.
    /// </summary>
    LoopVerificationReport VerifyMode(LoopMode mode);

    /// <summary>
    /// Выполняет формальную проверку для произвольной спецификации цикла.
    /// </summary>
    LoopVerificationReport VerifySpec(LoopSpec spec);

    /// <summary>
    /// Проверяет выполнение условий определённости Def (границы индексов массива)
    /// для конкретного состояния цикла (длина массива n, текущий индекс j).
    /// </summary>
    bool CheckDefinednessAtState(LoopMode mode, int arrayLength, int currentIndex);

    /// <summary>
    /// Возвращает словесный разбор произвольной логической формулы на русском языке
    /// с расшифровкой логических и арифметических знаков.
    /// </summary>
    string DescribeFormulaRussian(Condition condition);
}
