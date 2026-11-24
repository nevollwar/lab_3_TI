namespace WpEngine.Core.AST;

/// <summary>
/// Базовый абстрактный узел синтаксического дерева для арифметических выражений.
/// </summary>
public abstract record Expr
{
    public abstract override string ToString();
}

/// <summary>
/// Числовая константа в выражении.
/// </summary>
/// <param name="Value">Вещественное значение константы.</param>
public record NumberExpr(double Value) : Expr
{
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Переменная состояния программы (идентификатор).
/// </summary>
/// <param name="Name">Имя переменной (например, x, max, D).</param>
public record VariableExpr(string Name) : Expr
{
    public override string ToString() => Name;
}

/// <summary>
/// Поддерживаемые бинарные арифметические операции.
/// </summary>
public enum BinaryOp
{
    Add, // Сложение (+)
    Sub, // Вычитание (-)
    Mul, // Умножение (*)
    Div  // Деление (/)
}

/// <summary>
/// Бинарная арифметическая операция над двумя подвыражениями.
/// </summary>
public record BinaryExpr(Expr Left, BinaryOp Op, Expr Right) : Expr
{
    public override string ToString() => Op switch
    {
        BinaryOp.Add => $"({Left} + {Right})",
        BinaryOp.Sub => $"({Left} - {Right})",
        BinaryOp.Mul => $"({Left} * {Right})",
        BinaryOp.Div => $"({Left} / {Right})",
        _ => throw new NotImplementedException()
    };
}

/// <summary>
/// Унарная функция вычисления абсолютного значения: abs(expr).
/// </summary>
public record AbsExpr(Expr Inner) : Expr
{
    public override string ToString() => $"abs({Inner})";
}

/// <summary>
/// Унарная функция извлечения квадратного корня: sqrt(expr).
/// Требует условия определённости Def: expr >= 0.
/// </summary>
public record SqrtExpr(Expr Inner) : Expr
{
    public override string ToString() => $"sqrt({Inner})";
}

/// <summary>
/// Индексированный доступ к элементу массива: a[index].
/// Требует условия определённости Def: 0 <= index && index < n.
/// </summary>
public record ArrayAccessExpr(string ArrayName, Expr Index) : Expr
{
    public override string ToString() => $"{ArrayName}[{Index}]";
}

/// <summary>
/// Бинарная функция максимума двух выражений: max(left, right).
/// </summary>
public record MaxExpr(Expr Left, Expr Right) : Expr
{
    public override string ToString() => $"max({Left}, {Right})";
}

/// <summary>
/// Вид агрегатной функции по префиксу массива a[0..endIndex).
/// </summary>
public enum PrefixAggregateKind
{
    Sum,              // Сумма элементов префикса: sum(a[0..j))
    CountGreaterThan, // Число элементов префикса > T: count(a[0..j) > T)
    Max               // Максимум префикса: max(a[0..j))
}

/// <summary>
/// Символьная агрегатная функция по полуоткрытому префиксу массива a[0..EndIndex).
/// Используется в инвариантах и постусловиях циклов для символьной подстановки wp.
/// </summary>
public record PrefixAggregateExpr(
    PrefixAggregateKind Kind,
    string ArrayName,
    Expr EndIndex,
    Expr? Threshold = null
) : Expr
{
    public override string ToString() => Kind switch
    {
        PrefixAggregateKind.Sum => $"sum({ArrayName}[0..{EndIndex}))",
        PrefixAggregateKind.CountGreaterThan => $"count({ArrayName}[0..{EndIndex}) > {Threshold})",
        PrefixAggregateKind.Max => $"max({ArrayName}[0..{EndIndex}))",
        _ => throw new NotImplementedException()
    };
}