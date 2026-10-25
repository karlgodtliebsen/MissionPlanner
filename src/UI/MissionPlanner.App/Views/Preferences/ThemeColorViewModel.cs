using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Themes;

namespace MissionPlanner.App.Views.Preferences;

/// <summary>Edits a semantic theme color without persisting a live preview.</summary>
public sealed class ThemeColorViewModel : ObservableObject
{
    private readonly ThemeColorCustomizer customizer;
    private readonly ThemeColorItem item;
    private readonly Action<ThemeColorViewModel>? changed;
    private Color color;
    private string hex;
    private string? error;

    public ThemeColorViewModel(ThemeColorCustomizer customizer, ThemeColorItem item, Action<ThemeColorViewModel>? changed = null)
    {
        this.customizer = customizer;
        this.item = item;
        this.changed = changed;
        color = customizer.GetColor(item);
        hex = ThemeColorCatalog.Format(color);
        ResetCommand = new RelayCommand(() =>
        {
            customizer.SetColor(item, null);
            Refresh();
            changed?.Invoke(this);
        });
    }

    public string Name => item.Name;
    public bool IsOverridden => customizer.IsOverridden(item);
    public IRelayCommand ResetCommand { get; }
    public string? Error { get => error; private set => SetProperty(ref error, value); }

    public Color Color
    {
        get => color;
        set
        {
            if (!SetProperty(ref color, value)) return;
            customizer.SetColor(item, value);
            hex = ThemeColorCatalog.Format(value);
            OnPropertyChanged(nameof(Hex));
            OnPropertyChanged(nameof(IsOverridden));
            Error = null;
            changed?.Invoke(this);
        }
    }

    public string Hex
    {
        get => hex;
        set
        {
            if (!SetProperty(ref hex, value)) return;
            if (value is null || (value.Length != 7 && value.Length != 9) || value[0] != '#' ||
                !Color.TryParse(value, out var parsed))
            {
                Error = "Use #RRGGBB or #AARRGGBB.";
                return;
            }
            Error = null;
            color = parsed;
            customizer.SetColor(item, parsed);
            OnPropertyChanged(nameof(Color));
            OnPropertyChanged(nameof(IsOverridden));
            changed?.Invoke(this);
        }
    }

    public void Refresh()
    {
        color = customizer.GetColor(item);
        hex = ThemeColorCatalog.Format(color);
        Error = null;
        OnPropertyChanged(nameof(Color));
        OnPropertyChanged(nameof(Hex));
        OnPropertyChanged(nameof(IsOverridden));
    }
}
