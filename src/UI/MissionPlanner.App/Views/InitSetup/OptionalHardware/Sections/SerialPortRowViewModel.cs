using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.App.Models;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Setup.OptionalHardware;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Groups shared parameter editors into one ArduPilot serial-port row.</summary>
public sealed class SerialPortRowViewModel : ObservableObject
{
    private readonly IParameterEditSession session;

    /// <summary>Initializes a row from reported parameter names.</summary>
    public SerialPortRowViewModel(SerialPortConfiguration port, IParameterEditSession session)
    {
        Configuration = port;
        this.session = session;
        Protocol = Create(port.Protocol);
        Speed = Create(port.Baud);
        Options = Create(port.Options);
    }

    /// <summary>Gets the reported serial group.</summary>
    public SerialPortConfiguration Configuration { get; }

    /// <summary>Gets the stable ArduPilot index, without assuming a physical UART mapping.</summary>
    public string DisplayName => $"SERIAL{Configuration.Index}";

    /// <summary>Gets the shared protocol editor.</summary>
    public ParameterItemViewModel? Protocol { get; }

    /// <summary>Gets the shared configured-baud editor; metadata labels show electrical units.</summary>
    public ParameterItemViewModel? Speed { get; }

    /// <summary>Gets the shared options editor.</summary>
    public ParameterItemViewModel? Options { get; }

    /// <summary>Gets whether protocol metadata allows editing.</summary>
    public bool CanEditProtocol => CanEdit(Protocol);

    /// <summary>Gets whether speed metadata allows editing.</summary>
    public bool CanEditSpeed => CanEdit(Speed);

    /// <summary>Gets whether option metadata allows editing.</summary>
    public bool CanEditOptions => CanEdit(Options);

    /// <summary>Updates existing editors without replacing focused controls.</summary>
    public void Synchronize()
    {
        foreach (var editor in new[] { Protocol, Speed, Options }.OfType<ParameterItemViewModel>())
        {
            if (session.GetField(editor.Name) is { } field)
            {
                editor.SetField(field);
            }
        }
        OnPropertyChanged(nameof(CanEditProtocol));
        OnPropertyChanged(nameof(CanEditSpeed));
        OnPropertyChanged(nameof(CanEditOptions));
    }

    private bool CanEdit(ParameterItemViewModel? editor) =>
        session.IsValid && editor is not null && session.GetField(editor.Name)?.Metadata.ReadOnly == false;

    private ParameterItemViewModel? Create(string? name) =>
        name is not null && session.GetField(name) is { } field ? new ParameterItemViewModel(session, field) : null;
}
