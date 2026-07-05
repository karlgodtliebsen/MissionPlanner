using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.App.AppViewModels;

public partial class ThemeChangeViewModel : ObservableObject
{
    /// <summary>
    ///  
    /// </summary>
    public AppTheme[] AppThemeList { get; } = [AppTheme.Light, AppTheme.Dark];

    [ObservableProperty] public partial AppTheme SelectedTheme { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public ThemeChangeViewModel()
    {
        var current = App.Current!;
        SelectedTheme = current.RequestedTheme == AppTheme.Dark ? AppTheme.Dark : AppTheme.Light;

        current.RequestedThemeChanged += (s, e) =>
        {
            if (SelectedTheme != current.RequestedTheme) SelectedTheme = current.RequestedTheme;
        };
    }

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        var current = App.Current!;

        if (current.UserAppTheme != value) current.UserAppTheme = value;
    }
}


///// <summary>
///// 
///// </summary>
////[RegisterAs(typeof(ThemeChangeViewModel), ServiceLifetime.Singleton)]
//public class ThemeChangeViewModel : ReactiveObject
//{
//    /// <summary>
//    ///  
//    /// </summary>
//    public AppTheme[] AppThemeList { get; } = [AppTheme.Light, AppTheme.Dark];

//    /// <summary>
//    /// 
//    /// </summary>
//    [Reactive]
//    public AppTheme SelectedTheme { get; set; }

//    /// <summary>
//    /// 
//    /// </summary>
//    public ThemeChangeViewModel()
//    {
//        var current = App.Current;
//        SelectedTheme = current.RequestedTheme == AppTheme.Dark ? AppTheme.Dark : AppTheme.Light;

//        current.RequestedThemeChanged += (s, e) =>
//        {
//            if (SelectedTheme != current.RequestedTheme)
//            {
//                SelectedTheme = current.RequestedTheme;
//            }
//        };

//        this.WhenAnyValue(x => x.SelectedTheme).Subscribe(theme =>
//        {
//            if (current.UserAppTheme != theme)
//            {
//                current.UserAppTheme = theme;
//            }
//        });
//    }
//}