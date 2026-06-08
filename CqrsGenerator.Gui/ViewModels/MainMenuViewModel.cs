using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CqrsGenerator.Gui.Services;

namespace CqrsGenerator.Gui.ViewModels;

public sealed partial class MainMenuViewModel : ObservableObject
{
    private readonly IThemeService _themeService;

    public MainMenuViewModel(IThemeService themeService)
    {
        _themeService = themeService;

        ThemeOptions = new ObservableCollection<ThemeOptionViewModel>
        {
            new(ThemeMode.System, "System"),
            new(ThemeMode.Light, "Light"),
            new(ThemeMode.Dark, "Dark"),
        };

        _selectedTheme = ThemeOptions.First(option => option.Mode == _themeService.CurrentMode);

        SetSystemThemeCommand = new RelayCommand(() => ApplyTheme(ThemeMode.System));
        SetLightThemeCommand = new RelayCommand(() => ApplyTheme(ThemeMode.Light));
        SetDarkThemeCommand = new RelayCommand(() => ApplyTheme(ThemeMode.Dark));
    }

    [ObservableProperty]
    private string _targetRootPath = "Target project is not selected";

    [ObservableProperty]
    private string _statusText = "Ready to open a target project.";

    [ObservableProperty]
    private string _selectedActionText = "No action selected";

    [ObservableProperty]
    private bool _isProjectLoaded;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private ThemeOptionViewModel _selectedTheme;

    public ObservableCollection<ThemeOptionViewModel> ThemeOptions { get; }

    public IAsyncRelayCommand? OpenProjectCommand { get; set; }

    public IAsyncRelayCommand? RescanProjectCommand { get; set; }

    public IRelayCommand SetSystemThemeCommand { get; }

    public IRelayCommand SetLightThemeCommand { get; }

    public IRelayCommand SetDarkThemeCommand { get; }

    public bool CanRescan => IsProjectLoaded && !IsScanning;

    public bool IsSystemThemeSelected => SelectedTheme.Mode == ThemeMode.System;

    public bool IsLightThemeSelected => SelectedTheme.Mode == ThemeMode.Light;

    public bool IsDarkThemeSelected => SelectedTheme.Mode == ThemeMode.Dark;

    partial void OnSelectedThemeChanged(ThemeOptionViewModel value)
    {
        _themeService.Apply(value.Mode);
        OnPropertyChanged(nameof(IsSystemThemeSelected));
        OnPropertyChanged(nameof(IsLightThemeSelected));
        OnPropertyChanged(nameof(IsDarkThemeSelected));
    }

    partial void OnIsProjectLoadedChanged(bool value) => OnPropertyChanged(nameof(CanRescan));

    partial void OnIsScanningChanged(bool value) => OnPropertyChanged(nameof(CanRescan));

    private void ApplyTheme(ThemeMode mode)
    {
        SelectedTheme = ThemeOptions.First(option => option.Mode == mode);
    }
}
