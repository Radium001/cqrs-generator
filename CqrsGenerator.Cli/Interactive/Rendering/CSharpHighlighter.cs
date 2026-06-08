using System.Text;
using CodePunk.Highlight.Core.SyntaxHighlighting;
using CodePunk.Highlight.Core.SyntaxHighlighting.Languages;
using CodePunk.Highlight.Spectre.Rendering;

namespace CqrsGenerator.Cli.Interactive.Rendering;

public static class CSharpHighlighter
{
    private static readonly SyntaxHighlighter Highlighter = new([
        new CSharpLanguageDefinition()
    ]);

    public static string Highlight(string code)
    {
        var builder = new StringBuilder();
        var renderer = new MarkupTokenRenderer(builder);
        Highlighter.Highlight(code, "csharp", renderer);
        return builder.ToString();
    }
}
