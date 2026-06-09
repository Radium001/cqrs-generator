namespace CqrsGenerator.Gui.Session;

public sealed class GenerationSessionValidationService
{
    private readonly GeneratorDefinitionCatalog _definitionCatalog;

    public GenerationSessionValidationService(GeneratorDefinitionCatalog definitionCatalog)
    {
        _definitionCatalog = definitionCatalog;
    }

    public SessionValidationResult Validate(GenerationSession session)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var node in session.Traverse())
        {
            if (!_definitionCatalog.HasDefinition(node.Kind))
            {
                node.Status = GeneratorNodeStatus.Invalid;
                errors.Add($"No generator definition registered for node '{node.Title}' ({node.Kind}).");
                continue;
            }

            var definition = _definitionCatalog.GetDefinition(node.Kind);
            var result = definition.Validate(node, session);

            if (!result.IsValid)
            {
                node.Status = GeneratorNodeStatus.Invalid;
                errors.AddRange(result.Errors.Select(error => $"{node.Kind}: {error}"));
            }
            else
            {
                node.Status = GeneratorNodeStatus.Valid;
            }

            warnings.AddRange(result.Warnings.Select(warning => $"{node.Kind}: {warning}"));
        }

        return new SessionValidationResult(errors.Count == 0, errors, warnings);
    }
}
