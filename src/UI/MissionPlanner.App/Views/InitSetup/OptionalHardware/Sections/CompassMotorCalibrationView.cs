using UraniumUI.Material.TabViews;
namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;
/// <summary>Displays CompassMot status, samples, and controls.</summary>
public sealed class CompassMotorCalibrationView : TabViewLifecycleContent<CompassMotorCalibrationViewModel>
{
    public CompassMotorCalibrationView(){var instruction=new Label();instruction.SetBinding(Label.TextProperty,"Instruction");var compensation=new Label();compensation.SetBinding(Label.TextProperty,"Compensation");var start=new Button{Text="Start CompassMot"};start.SetBinding(Button.CommandProperty,"StartCommand");var stop=new Button{Text="Stop",TextColor=Colors.OrangeRed};stop.SetBinding(Button.CommandProperty,"StopCommand");var samples=new VerticalStackLayout();samples.SetBinding(BindableLayout.ItemsSourceProperty,"Samples");BindableLayout.SetItemTemplate(samples,new DataTemplate(()=>{var label=new Label();label.SetBinding(Label.TextProperty,new Binding("ThrottlePercent",stringFormat:"Throttle {0:0.0}%"));return label;}));Content=new ScrollView{Content=new VerticalStackLayout{Padding=16,Spacing=10,Children={new Label{Text="Compass / Motor Calibration",FontSize=20,FontAttributes=FontAttributes.Bold},new Label{Text="Remove ALL propellers. CompassMot may spin motors.",TextColor=Colors.OrangeRed},instruction,compensation,new HorizontalStackLayout{Children={start,stop}},samples}}};}
}
