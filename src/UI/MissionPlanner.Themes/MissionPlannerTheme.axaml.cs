using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Styling;

namespace MissionPlanner.Themes;

/// <summary>Composes palette variants with shared resources and installed Semi and Ursa templates.</summary>
public partial class MissionPlannerTheme : Styles
{
    private bool initialized;

    /// <summary>Loads variant dictionaries; attach after the Semi and Ursa providers.</summary>
    public MissionPlannerTheme()
    {
        AvaloniaXamlLoader.Load(this);
        OwnerChanged += (_, _) => InitializeAdapters();
    }

    private void InitializeAdapters()
    {
        if (initialized || Owner is not Application application)
        {
            return;
        }
        initialized = true;
        foreach (var variant in Resources.ThemeDictionaries.Keys.ToArray())
        {
            var resources = new ResourceDictionary();
            resources.MergedDictionaries.Add(Resources.ThemeDictionaries[variant]);
            // Resolve only ordinary base variants: the new theme cannot resolve itself here.
            foreach (var key in new object[] { "SolidButton", "SolidIconButton", "SolidIconDropDownButton", typeof(ToggleButton), typeof(CheckBox), typeof(RadioButton) })
            {
                if (!application.TryFindResource(key, variant.InheritVariant, out var value) || value is not ControlTheme original)
                {
                    throw new InvalidOperationException($"Load SemiTheme and UrsaSemiTheme before MissionPlannerTheme: missing {key}.");
                }
                var theme = new ControlTheme(typeof(ContentControl)) { BasedOn = original };
                if (Equals(key, typeof(CheckBox)) || Equals(key, typeof(RadioButton)))
                {
                    AddGlyph(theme, Equals(key, typeof(CheckBox)), false);
                    AddGlyph(theme, Equals(key, typeof(CheckBox)), true);
                }
                else
                {
                    AddForeground(theme, null, "MissionOnPrimary", Equals(key, typeof(ToggleButton)));
                    AddForeground(theme, ":pointerover", "MissionOnPrimaryHover", Equals(key, typeof(ToggleButton)));
                    AddForeground(theme, ":pressed", "MissionOnPrimaryPressed", Equals(key, typeof(ToggleButton)));
                }
                resources[key] = theme;
            }
            Resources.ThemeDictionaries[variant] = resources;
        }
    }

    private static void AddForeground(ControlTheme theme, string? state, string key, bool checkedOnly)
    {
        var style = new Style(s =>
        {
            var selector = s.Nesting().Not(x => x.Class(":disabled"));
            foreach (var semantic in new[] { "Tertiary", "Success", "Warning", "Danger", "Colorful" })
            {
                selector = selector.Not(x => x.Class(semantic));
            }
            if (checkedOnly)
            {
                selector = selector.Class(":checked");
            }
            if (state is not null)
            {
                selector = selector.Class(state);
            }
            return selector.Template().OfType<ContentPresenter>().Name("PART_ContentPresenter");
        });
        style.Setters.Add(new Setter(ContentPresenter.ForegroundProperty, new DynamicResourceExtension(key)));
        theme.Children.Add(style);
    }

    private static void AddGlyph(ControlTheme theme, bool checkBox, bool pressed)
    {
        var style = new Style(s =>
        {
            var selector = s.Nesting().Not(x => x.Class(":disabled"));
            if (pressed)
            {
                selector = selector.Class(":pressed");
            }
            return selector.Template().OfType(checkBox ? typeof(PathIcon) : typeof(Ellipse)).Name("CheckGlyph");
        });
        var brush = new DynamicResourceExtension(pressed ? "MissionOnPrimaryPressed" : "MissionOnPrimary");
        style.Setters.Add(new Setter(checkBox ? PathIcon.ForegroundProperty : Shape.FillProperty, brush));
        if (!checkBox)
        {
            style.Setters.Add(new Setter(Shape.StrokeProperty, brush));
        }
        theme.Children.Add(style);
    }
}
