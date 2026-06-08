using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit.Highlighting;

namespace CqrsGenerator.Gui.Services;

public sealed class SyntaxHighlightingService : ISyntaxHighlightingService
{
    private static readonly IReadOnlyDictionary<string, Color> DarkPalette = new ReadOnlyDictionary<string, Color>(
        new Dictionary<string, Color>(StringComparer.Ordinal)
        {
            ["Comment"] = Color.Parse("#6A9955"),
            ["String"] = Color.Parse("#CE9178"),
            ["StringInterpolation"] = Color.Parse("#D7BA7D"),
            ["Char"] = Color.Parse("#D7BA7D"),
            ["Preprocessor"] = Color.Parse("#C586C0"),
            ["Punctuation"] = Color.Parse("#D4D4D4"),
            ["ValueTypeKeywords"] = Color.Parse("#4EC9B0"),
            ["ReferenceTypeKeywords"] = Color.Parse("#4EC9B0"),
            ["MethodCall"] = Color.Parse("#DCDCAA"),
            ["NumberLiteral"] = Color.Parse("#B5CEA8"),
            ["ThisOrBaseReference"] = Color.Parse("#569CD6"),
            ["NullOrValueKeywords"] = Color.Parse("#569CD6"),
            ["Keywords"] = Color.Parse("#569CD6"),
            ["GotoKeywords"] = Color.Parse("#C586C0"),
            ["ContextKeywords"] = Color.Parse("#569CD6"),
            ["ExceptionKeywords"] = Color.Parse("#C586C0"),
            ["CheckedKeyword"] = Color.Parse("#D4D4D4"),
            ["UnsafeKeywords"] = Color.Parse("#C586C0"),
            ["OperatorKeywords"] = Color.Parse("#C586C0"),
            ["ParameterModifiers"] = Color.Parse("#C586C0"),
            ["Modifiers"] = Color.Parse("#569CD6"),
            ["Visibility"] = Color.Parse("#569CD6"),
            ["NamespaceKeywords"] = Color.Parse("#4EC9B0"),
            ["GetSetAddRemove"] = Color.Parse("#569CD6"),
            ["TrueFalse"] = Color.Parse("#569CD6"),
            ["TypeKeywords"] = Color.Parse("#4EC9B0"),
            ["SemanticKeywords"] = Color.Parse("#4FC1FF"),
            ["Digits"] = Color.Parse("#B5CEA8"),
            ["Tags"] = Color.Parse("#569CD6"),
            ["HtmlTag"] = Color.Parse("#569CD6"),
            ["ScriptTag"] = Color.Parse("#569CD6"),
            ["JavaScriptTag"] = Color.Parse("#569CD6"),
            ["JScriptTag"] = Color.Parse("#569CD6"),
            ["VBScriptTag"] = Color.Parse("#569CD6"),
            ["UnknownScriptTag"] = Color.Parse("#569CD6"),
            ["Attributes"] = Color.Parse("#9CDCFE"),
            ["UnknownAttribute"] = Color.Parse("#9CDCFE"),
            ["Assignment"] = Color.Parse("#D4D4D4"),
            ["Slash"] = Color.Parse("#808080"),
            ["Entities"] = Color.Parse("#D7BA7D"),
            ["EntityReference"] = Color.Parse("#D7BA7D"),
        });

    private static readonly IReadOnlyDictionary<string, Color> LightPalette = new ReadOnlyDictionary<string, Color>(
        new Dictionary<string, Color>(StringComparer.Ordinal)
        {
            ["Comment"] = Color.Parse("#008000"),
            ["String"] = Color.Parse("#A31515"),
            ["StringInterpolation"] = Color.Parse("#795E26"),
            ["Char"] = Color.Parse("#A31515"),
            ["Preprocessor"] = Color.Parse("#AF00DB"),
            ["Punctuation"] = Color.Parse("#24292E"),
            ["ValueTypeKeywords"] = Color.Parse("#267F99"),
            ["ReferenceTypeKeywords"] = Color.Parse("#267F99"),
            ["MethodCall"] = Color.Parse("#795E26"),
            ["NumberLiteral"] = Color.Parse("#098658"),
            ["ThisOrBaseReference"] = Color.Parse("#0000FF"),
            ["NullOrValueKeywords"] = Color.Parse("#0000FF"),
            ["Keywords"] = Color.Parse("#0000FF"),
            ["GotoKeywords"] = Color.Parse("#AF00DB"),
            ["ContextKeywords"] = Color.Parse("#0000FF"),
            ["ExceptionKeywords"] = Color.Parse("#AF00DB"),
            ["CheckedKeyword"] = Color.Parse("#24292E"),
            ["UnsafeKeywords"] = Color.Parse("#AF00DB"),
            ["OperatorKeywords"] = Color.Parse("#AF00DB"),
            ["ParameterModifiers"] = Color.Parse("#AF00DB"),
            ["Modifiers"] = Color.Parse("#0000FF"),
            ["Visibility"] = Color.Parse("#0000FF"),
            ["NamespaceKeywords"] = Color.Parse("#267F99"),
            ["GetSetAddRemove"] = Color.Parse("#0000FF"),
            ["TrueFalse"] = Color.Parse("#0000FF"),
            ["TypeKeywords"] = Color.Parse("#267F99"),
            ["SemanticKeywords"] = Color.Parse("#001080"),
            ["Digits"] = Color.Parse("#098658"),
            ["Tags"] = Color.Parse("#800000"),
            ["HtmlTag"] = Color.Parse("#800000"),
            ["ScriptTag"] = Color.Parse("#800000"),
            ["JavaScriptTag"] = Color.Parse("#800000"),
            ["JScriptTag"] = Color.Parse("#800000"),
            ["VBScriptTag"] = Color.Parse("#800000"),
            ["UnknownScriptTag"] = Color.Parse("#800000"),
            ["Attributes"] = Color.Parse("#FF0000"),
            ["UnknownAttribute"] = Color.Parse("#FF0000"),
            ["Assignment"] = Color.Parse("#0000FF"),
            ["Slash"] = Color.Parse("#808080"),
            ["Entities"] = Color.Parse("#267F99"),
            ["EntityReference"] = Color.Parse("#267F99"),
        });

    private readonly Dictionary<string, IReadOnlyDictionary<string, Color>> _appliedThemeByDefinition = [];

    public IHighlightingDefinition? GetDefinition(string? relativePath, ThemeVariant themeVariant)
    {
        var definitionName = ResolveDefinitionName(relativePath);
        if (definitionName is null)
        {
            return null;
        }

        var definition = HighlightingManager.Instance.GetDefinition(definitionName);
        if (definition is null)
        {
            return null;
        }

        var palette = themeVariant == ThemeVariant.Dark ? DarkPalette : LightPalette;
        if (!_appliedThemeByDefinition.TryGetValue(definitionName, out var currentPalette) || !ReferenceEquals(currentPalette, palette))
        {
            ApplyPalette(definition, palette);
            _appliedThemeByDefinition[definitionName] = palette;
        }

        return definition;
    }

    private static string? ResolveDefinitionName(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        return Path.GetExtension(relativePath).ToLowerInvariant() switch
        {
            ".cs" or ".razor" => "C#",
            ".axaml" or ".xaml" or ".xml" => "XML",
            ".json" => "JavaScript",
            ".md" => "Markdown",
            _ => null,
        };
    }

    private static void ApplyPalette(IHighlightingDefinition definition, IReadOnlyDictionary<string, Color> palette)
    {
        foreach (var highlightingColor in definition.NamedHighlightingColors)
        {
            if (highlightingColor.Name is null || !palette.TryGetValue(highlightingColor.Name, out var color))
            {
                continue;
            }

            highlightingColor.Foreground = new SimpleHighlightingBrush(color);
        }
    }
}
