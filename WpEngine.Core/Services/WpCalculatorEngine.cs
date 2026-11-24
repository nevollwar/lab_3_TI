using System.Text;
using WpEngine.Core.AST;
using WpEngine.Core.Contracts;
using WpEngine.Core.Parsing;

namespace WpEngine.Core.Services;

/// <summary>
/// Базовая реализация движка слабейшего предусловия (IWpEngine) в WpEngine.Core.
/// Переиспользует SubstitutionEngine и Humanizer из ЛР-2.
/// Поддерживает присваивание, последовательности (Seq), ветвления (If) и условия определённости (Def):
/// деление на ноль, квадратный корень (arg >= 0) и границы индексов массива (0 <= idx && idx < n).
/// </summary>
public class WpCalculatorEngine : IWpEngine
{
    private readonly SubstitutionEngine _substitutor;
    private readonly string _arrayLengthVariable;

    public WpCalculatorEngine(SubstitutionEngine? substitutor = null, string arrayLengthVariable = "n")
    {
        _substitutor = substitutor ?? new SubstitutionEngine();
        _arrayLengthVariable = arrayLengthVariable;
    }

    /// <summary>
    /// Вычисляет слабейшее предусловие wp(program, postCondition) пошагово с конца к началу.
    /// </summary>
    public WpResult CalculateWp(Statement program, Condition postCondition)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(postCondition);

        var trace = new List<WpStep>();
        Condition rawWp = ComputeWp(program, postCondition, trace);
        Condition simplified = _substitutor.Simplify(rawWp);

        string shortNotation = Humanizer.Format(simplified);
        string humanNotation = Humanizer.Describe(simplified);
        string triad = $"{{ {shortNotation} }} {Humanizer.Format(program)} {{ {Humanizer.Format(postCondition)} }}";

        return new WpResult(
            simplified,
            shortNotation,
            humanNotation,
            triad,
            trace
        );
    }

    /// <summary>
    /// Извлекает все условия определённости Def(expr):
    /// - деление на ноль: denom != 0
    /// - квадратный корень: arg >= 0
    /// - индексация массива a[i]: 0 <= i && i < n
    /// </summary>
    public List<Condition> ExtractDefinednessConditions(Expr expr)
    {
        var conditions = new List<Condition>();
        CollectExprDef(expr, conditions);
        return DeduplicateConditions(conditions);
    }

    /// <summary>
    /// Извлекает условия определённости Def из логического условия (например, из условия if).
    /// </summary>
    public List<Condition> ExtractDefinednessConditions(Condition condition)
    {
        var conditions = new List<Condition>();
        CollectConditionDef(condition, conditions);
        return DeduplicateConditions(conditions);
    }

    /// <summary>
    /// Извлекает все условия определённости Def из оператора программы (рекурсивно по Seq, If, Assignment).
    /// </summary>
    public List<Condition> ExtractDefinednessConditions(Statement statement)
    {
        var conditions = new List<Condition>();
        CollectStatementDef(statement, conditions);
        return DeduplicateConditions(conditions);
    }

    private Condition ComputeWp(Statement stmt, Condition post, List<WpStep> trace)
    {
        switch (stmt)
        {
            case AssignmentStatement assign:
            {
                Condition substituted = _substitutor.Substitute(post, assign.Variable, assign.Value);
                var defConditions = ExtractDefinednessConditions(assign.Value);

                var notes = new StringBuilder();
                notes.Append($"Подстановка {assign.Variable} := {Humanizer.FormatExpr(assign.Value)} в постусловие.");

                Condition wp = substituted;
                if (defConditions.Count > 0)
                {
                    Condition defConjunction = CombineAnd(defConditions);
                    wp = new LogicalCondition(defConjunction, LogicalOp.And, wp);
                    notes.Append($" Добавлено условие определённости Def: {Humanizer.Format(defConjunction)}.");
                }

                wp = _substitutor.Simplify(wp);

                trace.Add(new WpStep(
                    StatementCode: Humanizer.Format(assign),
                    IntermediateCondition: Humanizer.Format(wp),
                    Description: notes.ToString()
                ));

                return wp;
            }

            case SequenceStatement seq:
            {
                // Пустая последовательность (например, пустая ветка else — skip): wp(skip, R) = R
                if (seq.Statements.Count == 0)
                {
                    trace.Add(new WpStep(
                        StatementCode: "skip",
                        IntermediateCondition: Humanizer.Format(post),
                        Description: "Пустой оператор (skip): условие остаётся без изменений."
                    ));
                    return post;
                }

                Condition current = post;
                for (int i = seq.Statements.Count - 1; i >= 0; i--)
                {
                    current = ComputeWp(seq.Statements[i], current, trace);
                }

                return current;
            }

            case IfStatement ifStmt:
            {
                var thenTrace = new List<WpStep>();
                var elseTrace = new List<WpStep>();

                Condition wpThen = ComputeWp(ifStmt.ThenBranch, post, thenTrace);
                Condition wpElse = ComputeWp(ifStmt.ElseBranch, post, elseTrace);

                foreach (var step in thenTrace)
                {
                    trace.Add(new WpStep(
                        $"[Ветка Then] {step.StatementCode}",
                        step.IntermediateCondition,
                        step.Description
                    ));
                }

                foreach (var step in elseTrace)
                {
                    trace.Add(new WpStep(
                        $"[Ветка Else] {step.StatementCode}",
                        step.IntermediateCondition,
                        step.Description
                    ));
                }

                Condition thenBranchCond = new LogicalCondition(ifStmt.Condition, LogicalOp.And, wpThen);
                Condition elseBranchCond = new LogicalCondition(new NotCondition(ifStmt.Condition), LogicalOp.And, wpElse);
                Condition combined = new LogicalCondition(thenBranchCond, LogicalOp.Or, elseBranchCond);

                var guardDefs = ExtractDefinednessConditions(ifStmt.Condition);
                var notes = new StringBuilder("Правило ветвления: (B && wp(Then, R)) || (!B && wp(Else, R)).");
                if (guardDefs.Count > 0)
                {
                    Condition defConjunction = CombineAnd(guardDefs);
                    combined = new LogicalCondition(defConjunction, LogicalOp.And, combined);
                    notes.Append($" Добавлено условие определённости Def для условия ветвления: {Humanizer.Format(defConjunction)}.");
                }

                combined = _substitutor.Simplify(combined);

                trace.Add(new WpStep(
                    StatementCode: $"if ({Humanizer.Format(ifStmt.Condition)}) ...",
                    IntermediateCondition: Humanizer.Format(combined),
                    Description: notes.ToString()
                ));

                return combined;
            }

            default:
                throw new NotSupportedException($"Неподдерживаемый тип оператора: {stmt.GetType().Name}");
        }
    }

    private void CollectStatementDef(Statement stmt, List<Condition> result)
    {
        switch (stmt)
        {
            case AssignmentStatement assign:
                CollectExprDef(assign.Value, result);
                break;
            case SequenceStatement seq:
                foreach (var s in seq.Statements)
                    CollectStatementDef(s, result);
                break;
            case IfStatement ifStmt:
                CollectConditionDef(ifStmt.Condition, result);
                CollectStatementDef(ifStmt.ThenBranch, result);
                CollectStatementDef(ifStmt.ElseBranch, result);
                break;
        }
    }

    private void CollectConditionDef(Condition cond, List<Condition> result)
    {
        switch (cond)
        {
            case RelationalCondition rel:
                CollectExprDef(rel.Left, result);
                CollectExprDef(rel.Right, result);
                break;
            case LogicalCondition log:
                CollectConditionDef(log.Left, result);
                CollectConditionDef(log.Right, result);
                break;
            case ImpliesCondition imp:
                CollectConditionDef(imp.Antecedent, result);
                CollectConditionDef(imp.Consequent, result);
                break;
            case NotCondition not:
                CollectConditionDef(not.Inner, result);
                break;
        }
    }

    private void CollectExprDef(Expr expr, List<Condition> result)
    {
        switch (expr)
        {
            case BinaryExpr bin:
                if (bin.Op == BinaryOp.Div)
                {
                    result.Add(new RelationalCondition(bin.Right, RelationalOp.NotEqual, new NumberExpr(0)));
                }
                CollectExprDef(bin.Left, result);
                CollectExprDef(bin.Right, result);
                break;

            case SqrtExpr sqrt:
                result.Add(new RelationalCondition(sqrt.Inner, RelationalOp.GreaterOrEqual, new NumberExpr(0)));
                CollectExprDef(sqrt.Inner, result);
                break;

            case ArrayAccessExpr arr:
                // Условие определённости границ индекса массива: 0 <= idx && idx < n
                result.Add(new RelationalCondition(new NumberExpr(0), RelationalOp.LessOrEqual, arr.Index));
                result.Add(new RelationalCondition(arr.Index, RelationalOp.Less, new VariableExpr(_arrayLengthVariable)));
                CollectExprDef(arr.Index, result);
                break;

            case AbsExpr abs:
                CollectExprDef(abs.Inner, result);
                break;

            case MaxExpr max:
                CollectExprDef(max.Left, result);
                CollectExprDef(max.Right, result);
                break;

            case PrefixAggregateExpr agg:
                CollectExprDef(agg.EndIndex, result);
                if (agg.Threshold is not null)
                    CollectExprDef(agg.Threshold, result);
                break;
        }
    }

    private static List<Condition> DeduplicateConditions(List<Condition> conditions)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unique = new List<Condition>();
        foreach (var c in conditions)
        {
            string key = Humanizer.Format(c);
            if (seen.Add(key))
                unique.Add(c);
        }
        return unique;
    }

    public static Condition CombineAnd(IReadOnlyList<Condition> conditions)
    {
        if (conditions.Count == 0)
            return new BooleanLiteralCondition(true);

        Condition acc = conditions[0];
        for (int i = 1; i < conditions.Count; i++)
            acc = new LogicalCondition(acc, LogicalOp.And, conditions[i]);
        return acc;
    }
}
