using Avalonia;
using Avalonia.Controls;
using MissionPlanner.Themes.Documents;

namespace MissionPlanner.Themes.Controls;

/// <summary>Displays setup documents independently of application services and view-model lifecycles.</summary>
public partial class SetupInformationView : UserControl
{
    public static readonly StyledProperty<UserDocument?> OverviewProperty =
        AvaloniaProperty.Register<SetupInformationView, UserDocument?>(nameof(Overview));
    public static readonly StyledProperty<UserDocument?> AdditionalDocumentProperty =
        AvaloniaProperty.Register<SetupInformationView, UserDocument?>(nameof(AdditionalDocument));
    public static readonly StyledProperty<UserDocument?> ParametersProperty =
        AvaloniaProperty.Register<SetupInformationView, UserDocument?>(nameof(Parameters));

    public UserDocument? Overview
    {
        get => GetValue(OverviewProperty);
        set => SetValue(OverviewProperty, value);
    }

    public UserDocument? AdditionalDocument
    {
        get => GetValue(AdditionalDocumentProperty);
        set => SetValue(AdditionalDocumentProperty, value);
    }

    public UserDocument? Parameters
    {
        get => GetValue(ParametersProperty);
        set => SetValue(ParametersProperty, value);
    }

    public SetupInformationView() => InitializeComponent();
}
