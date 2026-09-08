using System.Collections.ObjectModel;
using UraniumUI.Extensions;
using UraniumUI.Material.Resources;

namespace MissionPlanner.App.Resources.Styles;

/// <summary>
/// 
/// </summary>
public partial class StyleResources : ResourceDictionary
{
    private ResourceDictionary basedOn;
    private ResourceDictionary colorsOverride;

    public StyleResources()
    {
        // Retain Uranium-only compatibility resources used by controls such as
        // TabView. MissionPlanner colors are removed in ApplyColorOverride.
        MergedDictionaries.Add(new ColorResource());

        InitializeComponent();

        Overrides.CollectionChanged += (s, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                foreach (var item in e.NewItems)
                {
                    ApplyOverriddenBy(item as ResourceDictionary);
                }
            }
        };
    }

    public ResourceDictionary BasedOn
    {
        get => basedOn;
        set
        {
            basedOn = value;
            if (value != null)
            {
                ApplyBasedOn();
            }
        }
    }

    public ObservableCollection<ResourceDictionary> Overrides
    {
        get;
        set;
    } = [];

    public ResourceDictionary ColorsOverride
    {
        get => colorsOverride;
        set
        {
            colorsOverride = value;
            ApplyColorOverride();
        }
    }

    protected virtual void ApplyOverriddenBy(ResourceDictionary overriddenBy)
    {
        var thisStyleDict = MergedDictionaries.Last();

        foreach (var key in thisStyleDict.Keys)
        {
            if (overriddenBy.TryGetValue(key, out var value) && value is Style overriderStyle)
            {
                if (thisStyleDict[key] is Style thisStyle)
                {
                    thisStyle.OverrideBy(overriderStyle);
                }
            }
        }
    }

    internal virtual void ApplyColorOverride()
    {
        var uraniumColors = MergedDictionaries.First();

        foreach (var overrideKey in ColorsOverride.Keys)
        {
            uraniumColors.Remove(overrideKey);
        }
    }

    protected virtual void ApplyBasedOn()
    {
        var styleDict = MergedDictionaries.Skip(1).First();

        foreach (var key in styleDict.Keys)
        {
            if (BasedOn.TryGetValue(key, out var value) && value is Style baseStyle)
            {
                if (styleDict[key] is Style thisStyle)
                {
                    thisStyle.BaseOn(baseStyle);
                }
            }
        }
    }
}
