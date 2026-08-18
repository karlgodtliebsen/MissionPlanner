using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Displays frame-derived individual and sequence motor tests.</summary>
public sealed class MotorTestView : TabViewLifecycleContent<MotorTestViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MotorTestView"/> class.
    /// </summary>
    public MotorTestView()
    {
        var motors = new VerticalStackLayout { Spacing = 6 };
        motors.SetBinding(BindableLayout.ItemsSourceProperty, "Motors");
        BindableLayout.SetItemTemplate(motors, new DataTemplate(() =>
        {
            var button = new Button();
            button.SetBinding(Button.TextProperty, "Label");
            button.SetBinding(Button.CommandProperty, new Binding("BindingContext.TestMotorCommand", source: this));
            button.SetBinding(Button.CommandParameterProperty, ".");
            return button;
        }));
        var frame = new Label { FontAttributes = FontAttributes.Bold };
        frame.SetBinding(Label.TextProperty, "FrameDisplay");
        var status = new Label();
        status.SetBinding(Label.TextProperty, "Status");
        var sequence = new Button { Text = "Test all in sequence" };
        sequence.SetBinding(Button.CommandProperty, "TestSequenceCommand");
        var stop = new Button { Text = "STOP", TextColor = Colors.OrangeRed };
        stop.SetBinding(Button.CommandProperty, "StopCommand");
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 10,
                Children =
                {
                    new Label { Text = "Motor Test", FontSize = 20, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "Remove ALL propellers. Motors will spin.", TextColor = Colors.OrangeRed },
                    frame,
                    status,
                    motors,
                    new HorizontalStackLayout { Children = { sequence, stop } }
                }
            }
        };
    }
}
