namespace CqrsGenerator.Gui.ViewModels.Generators;

public interface IEmbeddedSessionHost
{
    void Open<TDraft>(IEmbeddedGeneratorSessionViewModel<TDraft> session, Action<TDraft> onCompleted);
}
