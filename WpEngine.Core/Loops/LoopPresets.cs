using WpEngine.Core.AST;

namespace WpEngine.Core.Loops;

/// <summary>
/// Пресеты формальных спецификаций для трёх обязательных режимов ЛР-3:
/// PrefixSum, Count &gt; T и PrefixMax.
/// </summary>
public static class LoopPresets
{
    public static LoopSpec GetSpec(LoopMode mode) => mode switch
    {
        LoopMode.PrefixSum => CreatePrefixSumSpec(),
        LoopMode.CountGreaterThan => CreateCountGreaterThanSpec(),
        LoopMode.PrefixMax => CreatePrefixMaxSpec(),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Неизвестный режим цикла.")
    };

    public static IReadOnlyList<LoopSpec> GetAllSpecs() =>
    [
        CreatePrefixSumSpec(),
        CreateCountGreaterThanSpec(),
        CreatePrefixMaxSpec()
    ];

    /// <summary>
    /// Режим 1: PrefixSum — сумма префикса a[0..j-1].
    /// init: res := 0; j := 0
    /// while (j &lt; n) { res := res + a[j]; j := j + 1 }
    /// </summary>
    private static LoopSpec CreatePrefixSumSpec()
    {
        var j = new VariableExpr("j");
        var n = new VariableExpr("n");
        var res = new VariableExpr("res");
        var zero = new NumberExpr(0);
        var one = new NumberExpr(1);
        var aj = new ArrayAccessExpr("a", j);

        var init = new SequenceStatement(
        [
            new AssignmentStatement("res", zero),
            new AssignmentStatement("j", zero)
        ]);

        var guard = new RelationalCondition(j, RelationalOp.Less, n);

        var body = new SequenceStatement(
        [
            new AssignmentStatement("res", new BinaryExpr(res, BinaryOp.Add, aj)),
            new AssignmentStatement("j", new BinaryExpr(j, BinaryOp.Add, one))
        ]);

        var pre = new RelationalCondition(n, RelationalOp.GreaterOrEqual, zero);

        var sumPrefix = new PrefixAggregateExpr(PrefixAggregateKind.Sum, "a", j);
        var inv = new LogicalCondition(
            new RelationalCondition(res, RelationalOp.Equal, sumPrefix),
            LogicalOp.And,
            new LogicalCondition(
                new RelationalCondition(zero, RelationalOp.LessOrEqual, j),
                LogicalOp.And,
                new RelationalCondition(j, RelationalOp.LessOrEqual, n)
            )
        );

        var post = new RelationalCondition(
            res,
            RelationalOp.Equal,
            new PrefixAggregateExpr(PrefixAggregateKind.Sum, "a", n)
        );

        var variant = new BinaryExpr(n, BinaryOp.Sub, j);

        return new LoopSpec(
            Mode: LoopMode.PrefixSum,
            Name: "Сумма транзакций (PrefixSum)",
            InitCode: "res := 0; j := 0;",
            LoopGuardText: "j < n",
            LoopBodyCode: "res += a[j]; j++;",
            InitStatement: init,
            LoopGuard: guard,
            LoopBody: body,
            PreCondition: pre,
            Invariant: inv,
            PostCondition: post,
            VariantExpr: variant,
            InvariantFormula: "res = Σ(a[0..j-1]) ∧ 0 ≤ j ≤ n",
            InvariantWords: "Переменная res равна сумме первых j транзакций a[0..j-1] (в ₽), а индекс j лежит от 0 до n.",
            VariantFormula: "t = n - j",
            VariantWords: "Сколько транзакций осталось обработать (t ≥ 0, уменьшается на 1 на каждом шаге)."
        );
    }

    /// <summary>
    /// Режим 2: Count &gt; T — число элементов префикса, строго превышающих порог T.
    /// init: res := 0; j := 0
    /// while (j &lt; n) { if (a[j] &gt; T) { res := res + 1 } else { skip }; j := j + 1 }
    /// </summary>
    private static LoopSpec CreateCountGreaterThanSpec()
    {
        var j = new VariableExpr("j");
        var n = new VariableExpr("n");
        var res = new VariableExpr("res");
        var threshold = new VariableExpr("T");
        var zero = new NumberExpr(0);
        var one = new NumberExpr(1);
        var aj = new ArrayAccessExpr("a", j);

        var init = new SequenceStatement(
        [
            new AssignmentStatement("res", zero),
            new AssignmentStatement("j", zero)
        ]);

        var guard = new RelationalCondition(j, RelationalOp.Less, n);

        var ifGreater = new IfStatement(
            Condition: new RelationalCondition(aj, RelationalOp.Greater, threshold),
            ThenBranch: new AssignmentStatement("res", new BinaryExpr(res, BinaryOp.Add, one)),
            ElseBranch: new SequenceStatement([])
        );

        var body = new SequenceStatement(
        [
            ifGreater,
            new AssignmentStatement("j", new BinaryExpr(j, BinaryOp.Add, one))
        ]);

        var pre = new RelationalCondition(n, RelationalOp.GreaterOrEqual, zero);

        var countPrefix = new PrefixAggregateExpr(PrefixAggregateKind.CountGreaterThan, "a", j, threshold);
        var inv = new LogicalCondition(
            new RelationalCondition(res, RelationalOp.Equal, countPrefix),
            LogicalOp.And,
            new LogicalCondition(
                new RelationalCondition(zero, RelationalOp.LessOrEqual, j),
                LogicalOp.And,
                new RelationalCondition(j, RelationalOp.LessOrEqual, n)
            )
        );

        var post = new RelationalCondition(
            res,
            RelationalOp.Equal,
            new PrefixAggregateExpr(PrefixAggregateKind.CountGreaterThan, "a", n, threshold)
        );

        var variant = new BinaryExpr(n, BinaryOp.Sub, j);

        return new LoopSpec(
            Mode: LoopMode.CountGreaterThan,
            Name: "Крупные платежи > T (Count > T)",
            InitCode: "res := 0; j := 0;",
            LoopGuardText: "j < n",
            LoopBodyCode: "if (a[j] > T) res++; j++;",
            InitStatement: init,
            LoopGuard: guard,
            LoopBody: body,
            PreCondition: pre,
            Invariant: inv,
            PostCondition: post,
            VariantExpr: variant,
            InvariantFormula: "res = |{i < j : a[i] > T}| ∧ 0 ≤ j ≤ n",
            InvariantWords: "Переменная res равна числу транзакций в a[0..j-1] строго больше порога T ₽, а индекс j лежит от 0 до n.",
            VariantFormula: "t = n - j",
            VariantWords: "Сколько транзакций осталось проверить по порогу T (t ≥ 0, уменьшается на 1 на каждом шаге)."
        );
    }

    /// <summary>
    /// Режим 3: PrefixMax — максимум непустого префикса a[0..j-1].
    /// init: res := a[0]; j := 1
    /// while (j &lt; n) { res := max(res, a[j]); j := j + 1 }
    /// </summary>
    private static LoopSpec CreatePrefixMaxSpec()
    {
        var j = new VariableExpr("j");
        var n = new VariableExpr("n");
        var res = new VariableExpr("res");
        var zero = new NumberExpr(0);
        var one = new NumberExpr(1);
        var a0 = new ArrayAccessExpr("a", zero);
        var aj = new ArrayAccessExpr("a", j);

        var init = new SequenceStatement(
        [
            new AssignmentStatement("res", a0),
            new AssignmentStatement("j", one)
        ]);

        var guard = new RelationalCondition(j, RelationalOp.Less, n);

        var body = new SequenceStatement(
        [
            new AssignmentStatement("res", new MaxExpr(res, aj)),
            new AssignmentStatement("j", new BinaryExpr(j, BinaryOp.Add, one))
        ]);

        var pre = new RelationalCondition(n, RelationalOp.GreaterOrEqual, one);

        var maxPrefix = new PrefixAggregateExpr(PrefixAggregateKind.Max, "a", j);
        var inv = new LogicalCondition(
            new RelationalCondition(res, RelationalOp.Equal, maxPrefix),
            LogicalOp.And,
            new LogicalCondition(
                new RelationalCondition(one, RelationalOp.LessOrEqual, j),
                LogicalOp.And,
                new RelationalCondition(j, RelationalOp.LessOrEqual, n)
            )
        );

        var post = new RelationalCondition(
            res,
            RelationalOp.Equal,
            new PrefixAggregateExpr(PrefixAggregateKind.Max, "a", n)
        );

        var variant = new BinaryExpr(n, BinaryOp.Sub, j);

        return new LoopSpec(
            Mode: LoopMode.PrefixMax,
            Name: "Максимальный платёж (PrefixMax)",
            InitCode: "res := a[0]; j := 1;",
            LoopGuardText: "j < n",
            LoopBodyCode: "res = max(res, a[j]); j++;",
            InitStatement: init,
            LoopGuard: guard,
            LoopBody: body,
            PreCondition: pre,
            Invariant: inv,
            PostCondition: post,
            VariantExpr: variant,
            InvariantFormula: "res = max(a[0..j)) ∧ 1 ≤ j ≤ n",
            InvariantWords: "Переменная res равна максимальной сумме среди первых j транзакций a[0..j-1] (в ₽), а индекс j лежит от 1 до n.",
            VariantFormula: "t = n - j",
            VariantWords: "Сколько транзакций осталось сравнить с максимумом (t ≥ 0, уменьшается на 1 на каждом шаге)."
        );
    }
}
