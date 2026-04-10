using System;
using System.Collections.Generic;
using Complex = System.Numerics.Complex;

/// <summary>
/// Parses a complex function string (e.g. "sqrt(z^2+1)") into an evaluable expression tree.
/// Supports standard evaluation and "lifted" evaluation for Riemann surface rendering.
/// 
/// Supported syntax:
///   Variables: z
///   Constants: pi, e, i, numeric literals
///   Operators: +, -, *, /, ^ (power)
///   Functions: sqrt, cbrt, log, ln, exp, sin, cos, abs
///   Grouping: ( )
///   Implicit multiplication: 2z, 3(z+1), z(z+1)
/// </summary>
public static class ComplexFunctionParser
{
    // ════════════════════════════════════════════════════════════
    // PUBLIC API
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Parse a function string and return a ParsedFunction with evaluation + sheet info.
    /// </summary>
    public static ParsedFunction Parse(string expression)
    {
        var tokens = Tokenize(expression.Trim().ToLowerInvariant());
        int pos = 0;
        var tree = ParseExpression(tokens, ref pos);

        if (pos < tokens.Count)
            throw new FormatException($"Unexpected token at position {pos}: '{tokens[pos].Text}'");

        return new ParsedFunction(tree);
    }

    // ════════════════════════════════════════════════════════════
    // TOKEN TYPES
    // ════════════════════════════════════════════════════════════

    enum TokenType { Number, Variable, Func, Op, LParen, RParen, Comma }

    struct Token
    {
        public TokenType Type;
        public string Text;
        public Token(TokenType t, string text) { Type = t; Text = text; }
        public override string ToString() => $"{Type}:{Text}";
    }

    // ════════════════════════════════════════════════════════════
    // TOKENIZER
    // ════════════════════════════════════════════════════════════

    static readonly HashSet<string> Functions = new HashSet<string>
        { "sqrt", "cbrt", "log", "ln", "exp", "sin", "cos", "tan", "abs", "conj", "arg" };

    static List<Token> Tokenize(string input)
    {
        var tokens = new List<Token>();
        int i = 0;

        while (i < input.Length)
        {
            char c = input[i];

            // Whitespace
            if (char.IsWhiteSpace(c)) { i++; continue; }

            // Number
            if (char.IsDigit(c) || (c == '.' && i + 1 < input.Length && char.IsDigit(input[i + 1])))
            {
                int start = i;
                while (i < input.Length && (char.IsDigit(input[i]) || input[i] == '.')) i++;
                tokens.Add(new Token(TokenType.Number, input.Substring(start, i - start)));
                continue;
            }

            // Identifier (variable, constant, or function)
            if (char.IsLetter(c))
            {
                int start = i;
                while (i < input.Length && char.IsLetterOrDigit(input[i])) i++;
                string word = input.Substring(start, i - start);

                if (Functions.Contains(word))
                    tokens.Add(new Token(TokenType.Func, word));
                else if (word == "z")
                    tokens.Add(new Token(TokenType.Variable, "z"));
                else if (word == "i")
                    tokens.Add(new Token(TokenType.Number, "i"));
                else if (word == "pi")
                    tokens.Add(new Token(TokenType.Number, "pi"));
                else if (word == "e")
                    tokens.Add(new Token(TokenType.Number, "e"));
                else
                    throw new FormatException($"Unknown identifier: '{word}'");
                continue;
            }

            // Operators and parentheses
            switch (c)
            {
                case '+': case '-': case '*': case '/': case '^':
                    tokens.Add(new Token(TokenType.Op, c.ToString())); i++; break;
                case '(':
                    tokens.Add(new Token(TokenType.LParen, "(")); i++; break;
                case ')':
                    tokens.Add(new Token(TokenType.RParen, ")")); i++; break;
                case ',':
                    tokens.Add(new Token(TokenType.Comma, ",")); i++; break;
                default:
                    throw new FormatException($"Unexpected character: '{c}'");
            }
        }

        // Insert implicit multiplication tokens:
        // e.g. "2z" → "2 * z", "z(" → "z * (", ")z" → ") * z"
        var result = new List<Token>();
        for (int j = 0; j < tokens.Count; j++)
        {
            result.Add(tokens[j]);

            if (j + 1 < tokens.Count)
            {
                var curr = tokens[j];
                var next = tokens[j + 1];
                bool needsMul =
                    (curr.Type == TokenType.Number || curr.Type == TokenType.Variable || curr.Type == TokenType.RParen) &&
                    (next.Type == TokenType.Number || next.Type == TokenType.Variable || next.Type == TokenType.Func || next.Type == TokenType.LParen);

                if (needsMul)
                    result.Add(new Token(TokenType.Op, "*"));
            }
        }

        return result;
    }

    // ════════════════════════════════════════════════════════════
    // RECURSIVE DESCENT PARSER
    // Grammar:
    //   Expression = Term (('+' | '-') Term)*
    //   Term       = Unary (('*' | '/') Unary)*
    //   Unary      = ('-')? Power
    //   Power      = Atom ('^' Unary)?
    //   Atom       = Number | Variable | Function '(' Expression ')' | '(' Expression ')'
    // ════════════════════════════════════════════════════════════

    static ExprNode ParseExpression(List<Token> tokens, ref int pos)
    {
        var left = ParseTerm(tokens, ref pos);

        while (pos < tokens.Count && tokens[pos].Type == TokenType.Op &&
               (tokens[pos].Text == "+" || tokens[pos].Text == "-"))
        {
            string op = tokens[pos++].Text;
            var right = ParseTerm(tokens, ref pos);
            left = new ExprNode(op == "+" ? NodeType.Add : NodeType.Subtract, left, right);
        }

        return left;
    }

    static ExprNode ParseTerm(List<Token> tokens, ref int pos)
    {
        var left = ParseUnary(tokens, ref pos);

        while (pos < tokens.Count && tokens[pos].Type == TokenType.Op &&
               (tokens[pos].Text == "*" || tokens[pos].Text == "/"))
        {
            string op = tokens[pos++].Text;
            var right = ParseUnary(tokens, ref pos);
            left = new ExprNode(op == "*" ? NodeType.Multiply : NodeType.Divide, left, right);
        }

        return left;
    }

    static ExprNode ParseUnary(List<Token> tokens, ref int pos)
    {
        if (pos < tokens.Count && tokens[pos].Type == TokenType.Op && tokens[pos].Text == "-")
        {
            pos++;
            var inner = ParsePower(tokens, ref pos);
            return new ExprNode(NodeType.Negate, inner);
        }
        return ParsePower(tokens, ref pos);
    }

    static ExprNode ParsePower(List<Token> tokens, ref int pos)
    {
        var base_ = ParseAtom(tokens, ref pos);

        if (pos < tokens.Count && tokens[pos].Type == TokenType.Op && tokens[pos].Text == "^")
        {
            pos++;
            var exp = ParseUnary(tokens, ref pos);
            return new ExprNode(NodeType.Power, base_, exp);
        }

        return base_;
    }

    static ExprNode ParseAtom(List<Token> tokens, ref int pos)
    {
        if (pos >= tokens.Count)
            throw new FormatException("Unexpected end of expression");

        var tok = tokens[pos];

        // Number / constant
        if (tok.Type == TokenType.Number)
        {
            pos++;
            switch (tok.Text)
            {
                case "i": return new ExprNode(Complex.ImaginaryOne);
                case "pi": return new ExprNode(new Complex(Math.PI, 0));
                case "e": return new ExprNode(new Complex(Math.E, 0));
                default:
                    if (double.TryParse(tok.Text, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double val))
                        return new ExprNode(new Complex(val, 0));
                    throw new FormatException($"Invalid number: '{tok.Text}'");
            }
        }

        // Variable z
        if (tok.Type == TokenType.Variable)
        {
            pos++;
            return new ExprNode(NodeType.Variable);
        }

        // Function call
        if (tok.Type == TokenType.Func)
        {
            string fname = tok.Text;
            pos++;
            if (pos >= tokens.Count || tokens[pos].Type != TokenType.LParen)
                throw new FormatException($"Expected '(' after function '{fname}'");
            pos++; // skip '('
            var arg = ParseExpression(tokens, ref pos);
            if (pos >= tokens.Count || tokens[pos].Type != TokenType.RParen)
                throw new FormatException($"Expected ')' after function argument");
            pos++; // skip ')'

            switch (fname)
            {
                case "sqrt": return new ExprNode(NodeType.Sqrt, arg);
                case "cbrt": return new ExprNode(NodeType.Cbrt, arg);
                case "log":
                case "ln": return new ExprNode(NodeType.Log, arg);
                case "exp": return new ExprNode(NodeType.Exp, arg);
                case "sin": return new ExprNode(NodeType.Sin, arg);
                case "cos": return new ExprNode(NodeType.Cos, arg);
                case "tan": return new ExprNode(NodeType.Tan, arg);
                case "abs": return new ExprNode(NodeType.Abs, arg);
                case "conj": return new ExprNode(NodeType.Conj, arg);
                case "arg": return new ExprNode(NodeType.Arg, arg);
                default: throw new FormatException($"Unknown function: '{fname}'");
            }
        }

        // Parenthesized expression
        if (tok.Type == TokenType.LParen)
        {
            pos++; // skip '('
            var inner = ParseExpression(tokens, ref pos);
            if (pos >= tokens.Count || tokens[pos].Type != TokenType.RParen)
                throw new FormatException("Expected ')'");
            pos++; // skip ')'
            return inner;
        }

        throw new FormatException($"Unexpected token: '{tok.Text}'");
    }
}

// ════════════════════════════════════════════════════════════
// EXPRESSION TREE NODE TYPES
// ════════════════════════════════════════════════════════════

public enum NodeType
{
    Constant, Variable,
    Add, Subtract, Multiply, Divide, Power, Negate,
    Sqrt, Cbrt, Log, Exp, Sin, Cos, Tan, Abs, Conj, Arg
}

/// <summary>
/// Expression tree node. Supports both standard evaluation and
/// "lifted" evaluation for Riemann surface rendering.
/// </summary>
public class ExprNode
{
    public NodeType Type;
    public Complex Value;       // For Constant nodes
    public ExprNode Left, Right; // For binary ops
    public ExprNode Child;       // For unary functions

    // Constant
    public ExprNode(Complex value) { Type = NodeType.Constant; Value = value; }

    // Variable
    public ExprNode(NodeType type) { Type = type; }

    // Unary function
    public ExprNode(NodeType type, ExprNode child) { Type = type; Child = child; }

    // Binary operator
    public ExprNode(NodeType type, ExprNode left, ExprNode right)
    { Type = type; Left = left; Right = right; }

    // ════════════════════════════════════════════════════════════
    // STANDARD EVALUATION (principal values)
    // ════════════════════════════════════════════════════════════

    public Complex Evaluate(Complex z)
    {
        switch (Type)
        {
            case NodeType.Constant: return Value;
            case NodeType.Variable: return z;

            case NodeType.Add: return Left.Evaluate(z) + Right.Evaluate(z);
            case NodeType.Subtract: return Left.Evaluate(z) - Right.Evaluate(z);
            case NodeType.Multiply: return Left.Evaluate(z) * Right.Evaluate(z);
            case NodeType.Divide: return Left.Evaluate(z) / Right.Evaluate(z);
            case NodeType.Power: return Complex.Pow(Left.Evaluate(z), Right.Evaluate(z));
            case NodeType.Negate: return -Child.Evaluate(z);

            case NodeType.Sqrt: return Complex.Sqrt(Child.Evaluate(z));
            case NodeType.Cbrt:
                var cv = Child.Evaluate(z);
                return Complex.FromPolarCoordinates(
                    Math.Pow(cv.Magnitude, 1.0 / 3.0), cv.Phase / 3.0);
            case NodeType.Log: return Complex.Log(Child.Evaluate(z));
            case NodeType.Exp: return Complex.Exp(Child.Evaluate(z));
            case NodeType.Sin: return Complex.Sin(Child.Evaluate(z));
            case NodeType.Cos: return Complex.Cos(Child.Evaluate(z));
            case NodeType.Tan: return Complex.Tan(Child.Evaluate(z));
            case NodeType.Abs: return new Complex(Child.Evaluate(z).Magnitude, 0);
            case NodeType.Conj: return Complex.Conjugate(Child.Evaluate(z));
            case NodeType.Arg: return new Complex(Child.Evaluate(z).Phase, 0);

            default: return Complex.Zero;
        }
    }

    // ════════════════════════════════════════════════════════════
    // SHEET DETECTION
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the number of Riemann sheets this expression produces.
    /// Counts how many full rotations around the origin are needed
    /// before the function returns to its starting value.
    /// </summary>
    public int DetectSheets(int maxSheets = 6)
    {
        // Count the branching factor from the expression tree
        int sheets = ComputeBranchFactor();
        return Math.Min(sheets, maxSheets);
    }

    private int ComputeBranchFactor()
    {
        switch (Type)
        {
            case NodeType.Constant:
            case NodeType.Variable:
                return 1;

            // Arithmetic operations: take the max of operands
            // (branches propagate through arithmetic)
            case NodeType.Add:
            case NodeType.Subtract:
            case NodeType.Multiply:
            case NodeType.Divide:
                return Math.Max(Left.ComputeBranchFactor(), Right.ComputeBranchFactor());

            case NodeType.Negate:
                return Child.ComputeBranchFactor();

            // Power: if exponent is a rational number 1/n, multiply branches by n
            case NodeType.Power:
                int baseBranch = Left.ComputeBranchFactor();
                if (Right.Type == NodeType.Constant)
                {
                    double re = Right.Value.Real;
                    double im = Right.Value.Imaginary;
                    if (Math.Abs(im) < 1e-10 && Math.Abs(re) > 1e-10)
                    {
                        // Try to find denominator: if re = p/q, branches *= q
                        int denom = FindDenominator(re);
                        if (denom > 1) return baseBranch * denom;
                    }
                }
                else if (Right.Type == NodeType.Divide &&
                         Right.Left.Type == NodeType.Constant &&
                         Right.Right.Type == NodeType.Constant)
                {
                    // Explicit fraction like 1/3
                    double denom = Right.Right.Value.Real;
                    if (Math.Abs(denom) > 0.5)
                        return baseBranch * (int)Math.Round(Math.Abs(denom));
                }
                return baseBranch;

            // Multi-valued functions
            case NodeType.Sqrt: return Child.ComputeBranchFactor() * 2;
            case NodeType.Cbrt: return Child.ComputeBranchFactor() * 3;
            case NodeType.Log: return Math.Max(Child.ComputeBranchFactor() * 3, 3); // Cap log at 3 sheets

            // Single-valued functions
            case NodeType.Exp:
            case NodeType.Sin:
            case NodeType.Cos:
            case NodeType.Tan:
            case NodeType.Abs:
            case NodeType.Conj:
            case NodeType.Arg:
                return Child.ComputeBranchFactor();

            default: return 1;
        }
    }

    // Try to find denominator for common fractions
    private static int FindDenominator(double val)
    {
        double absVal = Math.Abs(val);
        for (int d = 2; d <= 8; d++)
        {
            double product = absVal * d;
            if (Math.Abs(product - Math.Round(product)) < 1e-8)
                return d;
        }
        return 1;
    }

    // ════════════════════════════════════════════════════════════
    // CANDIDATE GENERATION (for analytic continuation)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Given the principal value w = f(z), generate all possible branch values.
    /// Used by the mesh generator for numerical analytic continuation.
    /// </summary>
    public List<Complex> GenerateCandidates(Complex principalValue)
    {
        var candidates = new List<Complex> { principalValue };

        CollectCandidates(this, principalValue, candidates);

        return candidates;
    }

    private static void CollectCandidates(ExprNode node, Complex w, List<Complex> candidates)
    {
        switch (node.Type)
        {
            case NodeType.Sqrt:
                // sqrt has 2 branches: w and -w
                if (!candidates.Contains(-w)) candidates.Add(-w);
                break;

            case NodeType.Cbrt:
                // cbrt has 3 branches
                var omega = Complex.FromPolarCoordinates(1, 2 * Math.PI / 3);
                var w2 = w * omega;
                var w3 = w * omega * omega;
                if (!candidates.Contains(w2)) candidates.Add(w2);
                if (!candidates.Contains(w3)) candidates.Add(w3);
                break;

            case NodeType.Log:
                // log has infinite branches: w + 2πin
                for (int n = -2; n <= 2; n++)
                {
                    if (n == 0) continue;
                    var wn = new Complex(w.Real, w.Imaginary + 2 * Math.PI * n);
                    if (!candidates.Contains(wn)) candidates.Add(wn);
                }
                break;

            case NodeType.Power:
                // For fractional powers z^(p/q): q branches
                if (node.Right.Type == NodeType.Constant)
                {
                    int denom = FindDenominator(node.Right.Value.Real);
                    if (denom > 1)
                    {
                        for (int k = 1; k < denom; k++)
                        {
                            var rot = Complex.FromPolarCoordinates(1, 2 * Math.PI * k / denom);
                            var wk = w * rot;
                            if (!candidates.Contains(wk)) candidates.Add(wk);
                        }
                    }
                }
                break;
        }

        // Recurse into children
        if (node.Left != null) CollectCandidates(node.Left, w, candidates);
        if (node.Right != null) CollectCandidates(node.Right, w, candidates);
        if (node.Child != null) CollectCandidates(node.Child, w, candidates);
    }
}

// ════════════════════════════════════════════════════════════
// PARSED FUNCTION (public API result)
// ════════════════════════════════════════════════════════════

/// <summary>
/// Result of parsing a complex function string.
/// Contains the expression tree, evaluation function, sheet count,
/// and candidate generator for Riemann surface rendering.
/// </summary>
public class ParsedFunction
{
    public ExprNode Tree { get; private set; }
    public int Sheets { get; private set; }

    public ParsedFunction(ExprNode tree)
    {
        Tree = tree;
        Sheets = tree.DetectSheets();
    }

    /// <summary>
    /// Evaluate f(z) using principal values.
    /// </summary>
    public Complex Evaluate(Complex z)
    {
        return Tree.Evaluate(z);
    }

    /// <summary>
    /// Generate all candidate values at a point (for analytic continuation).
    /// </summary>
    public List<Complex> GetCandidates(Complex z)
    {
        var principal = Tree.Evaluate(z);
        return Tree.GenerateCandidates(principal);
    }
}
