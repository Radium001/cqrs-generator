using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Core.Generation;

namespace CqrsGenerator.Gui.ViewModels;

public sealed partial class RepositoryMethodEditorViewModel : ObservableObject
{
    private readonly IRelayCommand<RepositoryMethodEditorViewModel> _removeMethodCommand;

    public RepositoryMethodEditorViewModel(
        string name,
        string returnType,
        IReadOnlyList<PropertySpec>? parameters,
        IRelayCommand<RepositoryMethodEditorViewModel> removeMethodCommand)
    {
        _name = name;
        _returnType = returnType;
        _removeMethodCommand = removeMethodCommand;
        Parameters = [];
        AddParameterCommand = new RelayCommand(AddParameter);
        RemoveParameterCommand = new RelayCommand<PropertyEntryViewModel>(RemoveParameter);
        ParameterEditor = new PropertyEntryListEditorViewModel(
            Parameters,
            AddParameterCommand,
            RemoveParameterCommand,
            "Parameters",
            "Add Parameter",
            "string",
            "value");

        if (parameters is not null)
        {
            foreach (var parameter in parameters)
            {
                AddParameter(parameter.Type, parameter.Name);
            }
        }
    }

    public ObservableCollection<PropertyEntryViewModel> Parameters { get; }

    public PropertyEntryListEditorViewModel ParameterEditor { get; }

    public IRelayCommand AddParameterCommand { get; }

    public IRelayCommand<PropertyEntryViewModel> RemoveParameterCommand { get; }

    public IRelayCommand<RepositoryMethodEditorViewModel> RemoveMethodCommand => _removeMethodCommand;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _returnType;

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(ReturnType) &&
        Parameters.All(parameter => parameter.IsComplete);

    private void AddParameter()
    {
        AddParameter("string", $"param{Parameters.Count + 1}");
    }

    private void AddParameter(string type, string name)
    {
        var parameter = new PropertyEntryViewModel(type, name);
        parameter.PropertyChanged += OnParameterChanged;
        Parameters.Add(parameter);
        OnPropertyChanged(nameof(IsComplete));
    }

    private void RemoveParameter(PropertyEntryViewModel? parameter)
    {
        if (parameter is null)
        {
            return;
        }

        parameter.PropertyChanged -= OnParameterChanged;
        Parameters.Remove(parameter);
        OnPropertyChanged(nameof(IsComplete));
    }

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(IsComplete));

    partial void OnReturnTypeChanged(string value) => OnPropertyChanged(nameof(IsComplete));

    private void OnParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PropertyEntryViewModel.Type) or nameof(PropertyEntryViewModel.Name))
        {
            OnPropertyChanged(nameof(IsComplete));
        }
    }
}
