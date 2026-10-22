using Avalonia;
using Avalonia.Controls;
using MissionPlanner.Themes.Documents;

namespace MissionPlanner.Themes.Controls;

/// <summary>Displays setup documents independently of application services and view-model lifecycles.</summary>
public partial class SetupInformationView : UserControl
{
    /// <summary>Identifies the <see cref="Overview"/> styled property.</summary>
    public static readonly StyledProperty<UserDocument?> OverviewProperty =
        AvaloniaProperty.Register<SetupInformationView, UserDocument?>(nameof(Overview));
    /// <summary>Identifies the <see cref="AdditionalDocument"/> styled property.</summary>
    public static readonly StyledProperty<UserDocument?> AdditionalDocumentProperty =
        AvaloniaProperty.Register<SetupInformationView, UserDocument?>(nameof(AdditionalDocument));
    /// <summary>Identifies the <see cref="Parameters"/> styled property.</summary>
    public static readonly StyledProperty<UserDocument?> ParametersProperty =
        AvaloniaProperty.Register<SetupInformationView, UserDocument?>(nameof(Parameters));

    /// <summary>Gets or sets the overview document.</summary>
    public UserDocument? Overview
    {
        get => GetValue(OverviewProperty);
        set => SetValue(OverviewProperty, value);
    }

    /// <summary>Gets or sets the additional document.</summary>
    public UserDocument? AdditionalDocument
    {
        get => GetValue(AdditionalDocumentProperty);
        set => SetValue(AdditionalDocumentProperty, value);
    }

    /// <summary>Gets or sets the parameters document.</summary>
    public UserDocument? Parameters
    {
        get => GetValue(ParametersProperty);
        set => SetValue(ParametersProperty, value);
    }

    /// <summary>Creates the view.</summary>
    public SetupInformationView() => InitializeComponent();
}
