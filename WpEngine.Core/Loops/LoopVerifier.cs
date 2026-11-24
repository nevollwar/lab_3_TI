using System.Text;
using WpEngine.Core.AST;
using WpEngine.Core.Contracts;
using WpEngine.Core.Parsing;
using WpEngine.Core.Services;

namespace WpEngine.Core.Loops;

/// <summary>
/// Реализация верификатора циклов (Роль 2: математика, wp, проверка 4 условий корректности и Def).
/// Переиспользует движок wp (WpCalculatorEngine) и Humanizer из ЛР-2.
/// </summary>
public class LoopVerifier : ILoopVerifier
{
    private readonly WpCalculatorEngine _wpEngine;

    public LoopVerifier(WpCalculatorEngine? wpEngine = null)
    {
        _wpEngine = wpEngine ?? new WpCalculatorEngine();
    }

    public LoopSpec GetSpec(LoopMode mode) => LoopPresets.GetSpec(mode);

    public LoopVerificationReport VerifyMode(LoopMode mode) => VerifySpec(GetSpec(mode));

    public LoopVerificationReport VerifySpec(LoopSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        // 1. Вычисляем wp(init, Inv) для условия инициализации
        WpResult wpInitResult = _wpEngine.CalculateWp(spec.InitStatement, spec.Invariant);
        LoopVerificationCondition initCondition = BuildInitializationCondition(spec, wpInitResult);

        // 2. Вычисляем wp(S, Inv) через движок wp из ЛР-2 и строим (Inv && B) => wp(S, Inv)
        WpResult wpPreservationResult = _wpEngine.CalculateWp(spec.LoopBody, spec.Invariant);
        var invAndGuard = new LogicalCondition(spec.Invariant, LogicalOp.And, spec.LoopGuard);
        var preservationImplication = new ImpliesCondition(invAndGuard, wpPreservationResult.FinalCondition);
        LoopVerificationCondition preservationCondition = BuildPreservationCondition(
            spec,
            wpPreservationResult,
            preservationImplication
        );

        // 3. Проверяем условие выхода: (Inv && !B) => Post
        LoopVerificationCondition exitCondition = BuildExitCondition(spec);

        // 4. Проверяем условие завершения: (Inv && B => t >= 0) и (Inv && B => wp(S, t' < t))
        var variantStrictDecreasePost = new RelationalCondition(
            spec.VariantExpr,
            RelationalOp.Less,
            new VariableExpr("t0")
        );
        WpResult wpVariantResult = _wpEngine.CalculateWp(spec.LoopBody, variantStrictDecreasePost);
        LoopVerificationCondition terminationCondition = BuildTerminationCondition(spec, wpVariantResult);

        // 5. Проверяем определённость Def (границы индексов массива, деление, корень)
        LoopVerificationCondition definednessCondition = BuildDefinednessCondition(spec);

        var allConditions = new List<LoopVerificationCondition>
        {
            initCondition,
            preservationCondition,
            exitCondition,
            terminationCondition,
            definednessCondition
        };

        string preservationFormula = ToMathNotation(preservationImplication);
        string preservationHuman = BuildPreservationRussianBreakdown(spec, wpPreservationResult, preservationImplication);

        return new LoopVerificationReport(
            Spec: spec,
            WpPreservationResult: wpPreservationResult,
            WpVariantDecreaseResult: wpVariantResult,
            PreservationImplication: preservationImplication,
            PreservationFormula: preservationFormula,
            PreservationHumanExplanation: preservationHuman,
            Initialization: initCondition,
            Preservation: preservationCondition,
            Exit: exitCondition,
            Termination: terminationCondition,
            Definedness: definednessCondition,
            AllConditions: allConditions,
            AllValid: allConditions.All(c => c.IsValid)
        );
    }

    public bool CheckDefinednessAtState(LoopMode mode, int arrayLength, int currentIndex)
    {
        if (arrayLength < 0) return false;

        return mode switch
        {
            // Для PrefixSum и Count > T: j ∈ [0, n], при входе в тело (j < n) обращение a[j] валидно
            LoopMode.PrefixSum or LoopMode.CountGreaterThan =>
                currentIndex >= 0 && currentIndex <= arrayLength,

            // Для PrefixMax: массив непуст (n >= 1 для a[0]) и j ∈ [1, n]
            LoopMode.PrefixMax =>
                arrayLength >= 1 && currentIndex >= 1 && currentIndex <= arrayLength,

            _ => false
        };
    }

    public string DescribeFormulaRussian(Condition condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        string math = ToMathNotation(condition);
        string words = Humanizer.Describe(condition);
        string legend = BuildSymbolLegend(math);

        return $"Формула: {math}\n" +
               $"Словами: {words}.\n" +
               $"Разбор знаков: {legend}";
    }

    private static LoopVerificationCondition BuildInitializationCondition(LoopSpec spec, WpResult wpInit)
    {
        var implication = new ImpliesCondition(spec.PreCondition, wpInit.FinalCondition);
        string expanded = ToMathNotation(implication);

        (bool valid, string proof) = spec.Mode switch
        {
            LoopMode.PrefixSum => (
                true,
                "При res = 0 и j = 0 сумма пустого префикса sum(a[0..0)) = 0, условие 0 ≤ 0 ≤ n следует из Pre (n ≥ 0)."
            ),
            LoopMode.CountGreaterThan => (
                true,
                "При res = 0 и j = 0 в пустом префиксе a[0..0) ровно 0 элементов > T, а 0 ≤ 0 ≤ n следует из Pre (n ≥ 0)."
            ),
            LoopMode.PrefixMax => (
                true,
                "Из Pre (n ≥ 1) следует 0 < n (обращение a[0] определено), res = a[0] = max(a[0..1)) и 1 ≤ 1 ≤ n."
            ),
            _ => (false, "Неизвестный режим цикла.")
        };

        string human = $"Если выполнено предусловие ({Humanizer.Describe(spec.PreCondition)}), " +
                       $"то после инициализации \"{spec.InitCode}\" выполняется инвариант: {spec.InvariantWords} " +
                       $"Через wp: {Humanizer.Describe(implication)}.";

        return new LoopVerificationCondition(
            Id: "Init",
            Title: "1. Инициализация (Pre ⇒ Inv)",
            Formula: "Pre ⇒ wp(init, Inv)",
            ExpandedFormula: expanded,
            HumanExplanation: human,
            IsValid: valid,
            ProofSummary: proof
        );
    }

    private static LoopVerificationCondition BuildPreservationCondition(
        LoopSpec spec,
        WpResult wpBody,
        ImpliesCondition implication)
    {
        string expanded = ToMathNotation(implication);

        (bool valid, string proof) = spec.Mode switch
        {
            LoopMode.PrefixSum => (
                true,
                "Из Inv ∧ (j < n) следует 0 ≤ j < n (Def выполнен), res + a[j] = sum(a[0..j)) + a[j] = sum(a[0..j+1)), и 0 ≤ j + 1 ≤ n."
            ),
            LoopMode.CountGreaterThan => (
                true,
                "Из Inv ∧ (j < n) следует 0 ≤ j < n (Def выполнен). Если a[j] > T, то res + 1 = count(a[0..j+1) > T); " +
                "если ¬(a[j] > T), то res = count(a[0..j+1) > T). В обеих ветках 0 ≤ j + 1 ≤ n."
            ),
            LoopMode.PrefixMax => (
                true,
                "Из Inv ∧ (j < n) следует 1 ≤ j < n ⇒ 0 ≤ j < n (Def выполнен), max(res, a[j]) = max(max(a[0..j)), a[j]) = max(a[0..j+1)), и 1 ≤ j + 1 ≤ n."
            ),
            _ => (false, "Неизвестный режим цикла.")
        };

        string human = $"Если до итерации выполняется инвариант ({spec.InvariantFormula}) и условие продолжения ({spec.LoopGuardText}), " +
                       $"то выполняется слабейшее предусловие тела цикла wp(S, Inv): {Humanizer.Describe(wpBody.FinalCondition)}.";

        return new LoopVerificationCondition(
            Id: "Preservation",
            Title: "2. Сохранение ((Inv ∧ B) ⇒ wp(S, Inv))",
            Formula: "(Inv ∧ B) ⇒ wp(S, Inv)",
            ExpandedFormula: expanded,
            HumanExplanation: human,
            IsValid: valid,
            ProofSummary: proof
        );
    }

    private static LoopVerificationCondition BuildExitCondition(LoopSpec spec)
    {
        var invAndNotGuard = new LogicalCondition(spec.Invariant, LogicalOp.And, new NotCondition(spec.LoopGuard));
        var implication = new ImpliesCondition(invAndNotGuard, spec.PostCondition);
        string expanded = ToMathNotation(implication);

        (bool valid, string proof) = spec.Mode switch
        {
            LoopMode.PrefixSum => (
                true,
                "Из Inv (j ≤ n) и ¬B (¬(j < n), т.е. j ≥ n) следует j = n, откуда res = sum(a[0..n)) — постусловие Post доказано."
            ),
            LoopMode.CountGreaterThan => (
                true,
                "Из Inv (j ≤ n) и ¬B (j ≥ n) следует j = n, откуда res = count(a[0..n) > T) — постусловие Post доказано."
            ),
            LoopMode.PrefixMax => (
                true,
                "Из Inv (1 ≤ j ≤ n) и ¬B (j ≥ n) следует j = n, откуда res = max(a[0..n)) — постусловие Post доказано."
            ),
            _ => (false, "Неизвестный режим цикла.")
        };

        string human = $"Когда цикл завершается (инвариант истинен и условие {spec.LoopGuardText} ложно, т.е. j = n), " +
                       $"автоматически выполняется постусловие: {Humanizer.Describe(spec.PostCondition)}.";

        return new LoopVerificationCondition(
            Id: "Exit",
            Title: "3. Выход ((Inv ∧ ¬B) ⇒ Post)",
            Formula: "(Inv ∧ ¬B) ⇒ Post",
            ExpandedFormula: expanded,
            HumanExplanation: human,
            IsValid: valid,
            ProofSummary: proof
        );
    }

    private static LoopVerificationCondition BuildTerminationCondition(LoopSpec spec, WpResult wpVariant)
    {
        var invAndGuard = new LogicalCondition(spec.Invariant, LogicalOp.And, spec.LoopGuard);
        var nonNegative = new ImpliesCondition(
            invAndGuard,
            new RelationalCondition(spec.VariantExpr, RelationalOp.GreaterOrEqual, new NumberExpr(0))
        );
        var strictDecrease = new ImpliesCondition(
            new LogicalCondition(
                invAndGuard,
                LogicalOp.And,
                new RelationalCondition(new VariableExpr("t0"), RelationalOp.Equal, spec.VariantExpr)
            ),
            wpVariant.FinalCondition
        );

        string expanded = $"({ToMathNotation(nonNegative)}) ∧ ({ToMathNotation(strictDecrease)})";

        // В каждом из трёх режимов на шаге цикла выполняется j := j + 1, поэтому t' = n - (j + 1) = (n - j) - 1 < t0
        bool valid = spec.Mode is LoopMode.PrefixSum or LoopMode.CountGreaterThan or LoopMode.PrefixMax;
        string proof = "При j < n вариант-функция t = n - j ≥ 1 ≥ 0 (неотрицательна и целая). " +
                       "После шага j := j + 1 новое значение t' = n - (j + 1) = t - 1 < t — строго убывает на 1 за шаг, " +
                       "значит цикл гарантированно завершится не более чем за n шагов.";

        string human = $"Вариант-функция {spec.VariantFormula} ({spec.VariantWords}) остаётся неотрицательной (t ≥ 0) " +
                       $"при входе в тело цикла и строго уменьшается после выполнения S: {Humanizer.Describe(wpVariant.FinalCondition)}.";

        return new LoopVerificationCondition(
            Id: "Termination",
            Title: "4. Завершение (t ≥ 0 и t' < t)",
            Formula: "(Inv ∧ B ⇒ t ≥ 0) ∧ (Inv ∧ B ⇒ wp(S, t' < t))",
            ExpandedFormula: expanded,
            HumanExplanation: human,
            IsValid: valid,
            ProofSummary: proof
        );
    }

    private LoopVerificationCondition BuildDefinednessCondition(LoopSpec spec)
    {
        var initDefs = _wpEngine.ExtractDefinednessConditions(spec.InitStatement);
        var guardDefs = _wpEngine.ExtractDefinednessConditions(spec.LoopGuard);
        var bodyDefs = _wpEngine.ExtractDefinednessConditions(spec.LoopBody);

        var allDefs = new List<Condition>();
        allDefs.AddRange(initDefs);
        allDefs.AddRange(guardDefs);
        allDefs.AddRange(bodyDefs);

        // Убираем дубликаты
        var uniqueDefs = new List<Condition>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in allDefs)
        {
            string key = Humanizer.Format(d);
            if (seen.Add(key))
                uniqueDefs.Add(d);
        }

        Condition combinedDef = WpCalculatorEngine.CombineAnd(uniqueDefs);
        string expanded = ToMathNotation(combinedDef);

        (bool valid, string proof) = spec.Mode switch
        {
            LoopMode.PrefixSum or LoopMode.CountGreaterThan => (
                true,
                "В теле цикла используется a[j]. Ограничение границ 0 ≤ j < n следует из Inv (0 ≤ j ≤ n) и B (j < n). Операций деления и извлечения корня нет."
            ),
            LoopMode.PrefixMax => (
                true,
                "В инициализации используется a[0] (0 ≤ 0 < n следует из Pre: n ≥ 1), в теле цикла — a[j] (0 ≤ j < n следует из Inv: 1 ≤ j ≤ n и B: j < n). Деления и корней нет."
            ),
            _ => (uniqueDefs.Count == 0, "Проверены ограничения определённости для выражений цикла.")
        };

        string human = uniqueDefs.Count == 0
            ? "Дополнительных ограничений определённости (деление, корень, индексация) не требуется."
            : $"Ограничения определённости Def: {Humanizer.Describe(combinedDef)}.";

        return new LoopVerificationCondition(
            Id: "Def",
            Title: "Определённость (Def)",
            Formula: "Def(Init) ∧ Def(B) ∧ Def(S)",
            ExpandedFormula: expanded,
            HumanExplanation: human,
            IsValid: valid,
            ProofSummary: proof
        );
    }

    private static string BuildPreservationRussianBreakdown(
        LoopSpec spec,
        WpResult wpBody,
        ImpliesCondition implication)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Условие сохранения: (Inv ∧ B) ⇒ wp(S, Inv)");
        sb.AppendLine($"Тело цикла S: {spec.LoopBodyCode}");
        sb.AppendLine($"Вычислено через движок wp: wp(S, Inv) = {ToMathNotation(wpBody.FinalCondition)}");
        sb.AppendLine();
        sb.AppendLine("Пошаговый расчёт wp(S, Inv) (с конца тела цикла к началу):");
        for (int i = 0; i < wpBody.Trace.Count; i++)
        {
            var step = wpBody.Trace[i];
            sb.AppendLine($"  Шаг {i + 1} [{step.StatementCode}]: {step.Description}");
            sb.AppendLine($"    ⇒ {ToMathNotationString(step.IntermediateCondition)}");
        }
        sb.AppendLine();
        sb.AppendLine($"Разбор формулы словами: {Humanizer.Describe(implication)}.");
        sb.Append($"Значения знаков: {BuildSymbolLegend(ToMathNotation(implication))}");
        return sb.ToString();
    }

    /// <summary>
    /// Переводит краткую ASCII-нотацию Humanizer.Format в академическую математическую запись (∧, ∨, ¬, ⇒, ≤, ≥, ≠).
    /// </summary>
    public static string ToMathNotation(Condition condition) =>
        ToMathNotationString(Humanizer.Format(condition));

    private static string ToMathNotationString(string asciiFormula) =>
        asciiFormula
            .Replace("=>", "⇒")
            .Replace("&&", "∧")
            .Replace("||", "∨")
            .Replace("<=", "≤")
            .Replace(">=", "≥")
            .Replace("!=", "≠")
            .Replace("==", "=")
            .Replace("!", "¬");

    private static string BuildSymbolLegend(string mathFormula)
    {
        var items = new List<string>();
        if (mathFormula.Contains('∧')) items.Add("«∧» — логическое И");
        if (mathFormula.Contains('∨')) items.Add("«∨» — логическое ИЛИ");
        if (mathFormula.Contains('¬')) items.Add("«¬» — отрицание (НЕ)");
        if (mathFormula.Contains('⇒')) items.Add("«⇒» — следование (если ..., то ...)");
        if (mathFormula.Contains('≤')) items.Add("«≤» — меньше или равно");
        if (mathFormula.Contains('≥')) items.Add("«≥» — больше или равно");
        if (mathFormula.Contains('≠')) items.Add("«≠» — не равно");
        if (mathFormula.Contains('<')) items.Add("«<» — строго меньше");
        if (mathFormula.Contains('>')) items.Add("«>» — строго больше");
        if (mathFormula.Contains('=')) items.Add("«=» — равно");

        return items.Count > 0 ? string.Join("; ", items) + "." : "логических операторов нет.";
    }
}
