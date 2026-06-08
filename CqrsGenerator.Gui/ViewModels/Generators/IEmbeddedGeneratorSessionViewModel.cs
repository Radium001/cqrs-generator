namespace CqrsGenerator.Gui.ViewModels.Generators;

public interface IEmbeddedGeneratorSessionViewModel : IGeneratorSessionViewModel
{
    bool CanComplete { get; }
}

public interface IEmbeddedGeneratorSessionViewModel<out TDraft> : IEmbeddedGeneratorSessionViewModel
{
    TDraft BuildDraft();
}
