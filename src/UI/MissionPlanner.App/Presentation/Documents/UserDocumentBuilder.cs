using System.Text;

namespace MissionPlanner.App.Presentation.Documents;

/// <summary>Composes small reports; all public text inputs are escaped, never interpreted as markup.</summary>
public sealed class UserDocumentBuilder
{
    private readonly StringBuilder source = new();

    /// <summary>Escapes Markdown punctuation, HTML delimiters, and multiline dynamic text.</summary>
    public static string Escape(string? text)
    {
        var result = new StringBuilder();
        foreach (var character in Normalize(text))
        {
            if ("\\`*_{}[]<>()#+-.!|&~=".Contains(character))
            {
                result.Append('\\');
            }
            result.Append(character);
        }
        return result.ToString();
    }

    /// <summary>Produces inline code with a delimiter longer than any backtick run in the value.</summary>
    public static string Code(string? text)
    {
        var value = Normalize(text);
        if (value.Length == 0)
        {
            return "—";
        }
        var longest = 0;
        var run = 0;
        foreach (var character in value)
        {
            run = character == '`' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }
        var delimiter = new string('`', longest + 1);
        return delimiter + " " + value + " " + delimiter;
    }

    /// <summary>Adds an escaped heading.</summary>
    public UserDocumentBuilder Heading(string text, int level = 2)
    {
        SeparateBlock();
        source.Append('#', Math.Clamp(level, 1, 6)).Append(' ').Append(Escape(text)).Append("\n\n");
        return this;
    }

    /// <summary>Adds an escaped paragraph, omitting absent optional content.</summary>
    public UserDocumentBuilder Paragraph(string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            source.Append(Escape(text)).Append("\n\n");
        }
        return this;
    }

    /// <summary>Adds an escaped bullet.</summary>
    public UserDocumentBuilder Bullet(string? text)
    {
        source.Append("- ").Append(Escape(text)).Append('\n');
        return this;
    }

    /// <summary>Adds a read-only note or warning.</summary>
    public UserDocumentBuilder Note(string text)
    {
        SeparateBlock();
        source.Append("> ").Append(Escape(text)).Append("\n\n");
        return this;
    }

    /// <summary>Adds a simple two-column table of escaped text or raw code values.</summary>
    public UserDocumentBuilder Table(string first, string second,
        IEnumerable<(string Name, string Value)> rows, bool codeValues = false)
    {
        SeparateBlock();
        source.Append("| ").Append(Escape(first)).Append(" | ").Append(Escape(second))
            .Append(" |\n| --- | --- |\n");
        foreach (var (name, value) in rows)
        {
            // Markdig's pipe-table parser recognizes code spans and retains their literal content.
            source.Append("| ").Append(codeValues ? Code(name) : Escape(name)).Append(" | ")
                .Append(codeValues ? Code(value) : Escape(value)).Append(" |\n");
        }
        source.Append('\n');
        return this;
    }

    /// <summary>Creates an immutable completed document.</summary>
    public UserDocument Build(string? title = null) => new(source.ToString().TrimEnd() + "\n", title);

    private void SeparateBlock()
    {
        if (source.Length > 0 && source[^1] != '\n')
        {
            source.Append('\n');
        }
        if (source.Length > 1 && source[^2] != '\n')
        {
            source.Append('\n');
        }
    }

    private static string Normalize(string? text) => (text ?? string.Empty)
        .Replace("\r\n", " ", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ');
}
