using CqrsGenerator.Core.Configuration;
using CqrsGenerator.Core.Validation;

namespace CqrsGenerator.Core.Generation;

public sealed class RazorImportsGenerator(GeneratorConfig config)
{
    public GenerationPlan AddUsing(string featurePath, string namespaceToImport)
    {
        return AddUsings(featurePath, [namespaceToImport]);
    }

    public GenerationPlan AddUsings(string featurePath, IReadOnlyList<string> namespacesToImport)
    {
        var plan = new GenerationPlan();
        AddUsingsToPlan(plan, featurePath, namespacesToImport);
        return plan;
    }

    public void AddUsingsToPlan(GenerationPlan plan, string featurePath, IReadOnlyList<string> namespacesToImport)
    {
        CSharpNameValidator.EnsureFeaturePath(featurePath);
        if (namespacesToImport.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Namespace is required.", nameof(namespacesToImport));
        }

        var normalizedPath = StringUtilities.NormalizeFeaturePath(featurePath)
            .Replace('/', Path.DirectorySeparatorChar);
        var importsPath = Path.Combine(config.WebFeatureRootPath, normalizedPath, "_Imports.razor");
        var usingLines = namespacesToImport
            .Distinct(StringComparer.Ordinal)
            .Select(ns => $"@using {ns}")
            .ToArray();

        if (plan.TryGetPlannedFileContent(importsPath, out var plannedContent))
        {
            var missingLines = GetMissingLines(plannedContent, usingLines);
            if (missingLines.Length > 0)
            {
                plan.TransformFile(importsPath, content => AppendLines(content, missingLines));
            }

            return;
        }

        if (File.Exists(importsPath))
        {
            var content = File.ReadAllText(importsPath);
            var missingLines = GetMissingLines(content, usingLines);
            if (missingLines.Length == 0)
            {
                return;
            }

            plan.AddUpdateFile(importsPath, AppendLines(content, missingLines));
            return;
        }

        plan.AddCreateFile(importsPath, string.Join(Environment.NewLine, usingLines));
    }

    private static string[] GetMissingLines(string content, IReadOnlyList<string> usingLines)
    {
        var existingLines = content.Split(["\r\n", "\n"], StringSplitOptions.None).ToHashSet(StringComparer.Ordinal);
        return usingLines.Where(line => !existingLines.Contains(line)).ToArray();
    }

    private static string AppendLines(string content, IReadOnlyList<string> missingLines) =>
        content.TrimEnd() + Environment.NewLine + string.Join(Environment.NewLine, missingLines);
}
