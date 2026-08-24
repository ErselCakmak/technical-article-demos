using System.Text.RegularExpressions;

namespace DocumentDslLesson;

public interface IDocumentTarget
{
    void EmitPage(string name);
}

public sealed class InMemoryDocumentTarget : IDocumentTarget
{
    private readonly List<string> _pages = [];

    public IReadOnlyList<string> Pages => _pages;

    public void EmitPage(string name) => _pages.Add(name);
}

public sealed class PlanningDocumentTarget : IDocumentTarget
{
    private readonly List<string> _plannedOperations = [];

    public IReadOnlyList<string> PlannedOperations => _plannedOperations;

    public void EmitPage(string name) => _plannedOperations.Add($"Page: {name}");
}

public sealed class DocumentScriptEngine
{
    public void Execute(string script, IDocumentTarget target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        ArgumentNullException.ThrowIfNull(target);

        InstructionBlock program = DocumentScriptCompiler.Compile(script);
        program.Execute(new ScopedValues(), target);
    }
}

public sealed class DocumentScriptException(string message) : Exception(message);

internal interface IInstruction
{
    void Execute(ScopedValues values, IDocumentTarget target);
}

internal sealed class InstructionBlock(IReadOnlyList<IInstruction> instructions) : IInstruction
{
    public void Execute(ScopedValues values, IDocumentTarget target)
    {
        foreach (IInstruction instruction in instructions)
        {
            instruction.Execute(values, target);
        }
    }
}

internal sealed class ForEachInstruction(
    string alias,
    IReadOnlyList<string> items,
    InstructionBlock recordedBody) : IInstruction
{
    public void Execute(ScopedValues values, IDocumentTarget target)
    {
        foreach (string item in items)
        {
            using IDisposable scope = values.BeginScope();
            values.Define(alias, item);

            // The compiled body is recorded once and replayed for every item.
            recordedBody.Execute(values, target);
        }
    }
}

internal sealed partial class PageInstruction(string nameTemplate) : IInstruction
{
    public void Execute(ScopedValues values, IDocumentTarget target)
    {
        string pageName = PlaceholderPattern().Replace(
            nameTemplate,
            match => values.GetRequired(match.Groups[1].Value));

        target.EmitPage(pageName);
    }

    [GeneratedRegex("<([A-Za-z][A-Za-z0-9_]*)>")]
    private static partial Regex PlaceholderPattern();
}

internal sealed class ScopedValues
{
    private readonly Dictionary<string, Stack<string>> _values =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<List<string>> _scopeDefinitions = new();

    public IDisposable BeginScope()
    {
        _scopeDefinitions.Push([]);
        return new ScopeLease(this);
    }

    public void Define(string name, string value)
    {
        if (_scopeDefinitions.Count == 0)
        {
            throw new InvalidOperationException("A scope must be active before defining a value.");
        }

        if (!_values.TryGetValue(name, out Stack<string>? stack))
        {
            stack = new Stack<string>();
            _values.Add(name, stack);
        }

        stack.Push(value);
        _scopeDefinitions.Peek().Add(name);
    }

    public string GetRequired(string name)
    {
        if (_values.TryGetValue(name, out Stack<string>? stack) && stack.Count > 0)
        {
            return stack.Peek();
        }

        throw new DocumentScriptException($"Value '{name}' is not available in the current scope.");
    }

    private void EndScope()
    {
        foreach (string name in _scopeDefinitions.Pop())
        {
            Stack<string> stack = _values[name];
            stack.Pop();

            if (stack.Count == 0)
            {
                _values.Remove(name);
            }
        }
    }

    private sealed class ScopeLease(ScopedValues owner) : IDisposable
    {
        private ScopedValues? _owner = owner;

        public void Dispose()
        {
            _owner?.EndScope();
            _owner = null;
        }
    }
}

internal static class DocumentScriptCompiler
{
    public static InstructionBlock Compile(string script)
    {
        string[] lines = script
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();

        int index = 0;
        InstructionBlock program = ParseBlock(lines, ref index, expectsEnd: false);

        if (index != lines.Length)
        {
            throw new DocumentScriptException($"Unexpected instruction at line {index + 1}.");
        }

        return program;
    }

    private static InstructionBlock ParseBlock(string[] lines, ref int index, bool expectsEnd)
    {
        List<IInstruction> instructions = [];

        while (index < lines.Length)
        {
            string line = lines[index];

            if (line.Equals("end", StringComparison.OrdinalIgnoreCase))
            {
                if (!expectsEnd)
                {
                    throw new DocumentScriptException($"Unexpected 'end' at line {index + 1}.");
                }

                index++;
                return new InstructionBlock(instructions);
            }

            if (line.StartsWith("for ", StringComparison.OrdinalIgnoreCase))
            {
                (string alias, IReadOnlyList<string> items) = ParseFor(line, index + 1);
                index++;
                InstructionBlock body = ParseBlock(lines, ref index, expectsEnd: true);
                instructions.Add(new ForEachInstruction(alias, items, body));
                continue;
            }

            if (line.StartsWith("page ", StringComparison.OrdinalIgnoreCase))
            {
                instructions.Add(new PageInstruction(ParsePageName(line, index + 1)));
                index++;
                continue;
            }

            throw new DocumentScriptException($"Unknown instruction at line {index + 1}: {line}");
        }

        if (expectsEnd)
        {
            throw new DocumentScriptException("A block reached the end of the script without a matching 'end'.");
        }

        return new InstructionBlock(instructions);
    }

    private static (string Alias, IReadOnlyList<string> Items) ParseFor(string line, int lineNumber)
    {
        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 4 || !parts[2].Equals("in", StringComparison.OrdinalIgnoreCase))
        {
            throw new DocumentScriptException(
                $"Invalid loop at line {lineNumber}. Expected: for <alias> in <item...>");
        }

        return (parts[1], parts[3..]);
    }

    private static string ParsePageName(string line, int lineNumber)
    {
        string value = line["page ".Length..].Trim();

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DocumentScriptException($"A page name is required at line {lineNumber}.");
        }

        return value;
    }
}
