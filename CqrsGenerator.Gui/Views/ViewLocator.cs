using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CqrsGenerator.Gui.ViewModels.Generators;

namespace CqrsGenerator.Gui.Views;

public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null)
        {
            return null;
        }

        var viewType = ResolveViewType(data);

        if (viewType is null || !typeof(Control).IsAssignableFrom(viewType))
        {
            return CreateFallback(data);
        }

        return Activator.CreateInstance(viewType) as Control ?? CreateFallback(data);
    }

    public bool Match(object? data) => data is IGeneratorSessionViewModel;

    public static Type? ResolveViewType(object data)
    {
        var viewTypeName = data.GetType().FullName?
            .Replace("ViewModels", "Views", StringComparison.Ordinal)
            .Replace(".Views.Generators.", ".Views.", StringComparison.Ordinal)
            .Replace("ViewModel", "View", StringComparison.Ordinal);

        return viewTypeName is null
            ? null
            : data.GetType().Assembly.GetType(viewTypeName);
    }

    private static TextBlock CreateFallback(object data)
    {
        return new TextBlock
        {
            Text = $"View not found for {data.GetType().Name}."
        };
    }
}
