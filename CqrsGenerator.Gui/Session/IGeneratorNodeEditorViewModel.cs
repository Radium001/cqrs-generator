namespace CqrsGenerator.Gui.Session;

public interface IGeneratorNodeEditorViewModel
{
    GeneratorNode? Node { get; set; }

    string DisplayName { get; }

    string Summary { get; }
}
