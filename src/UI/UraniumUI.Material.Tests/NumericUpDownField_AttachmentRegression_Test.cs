#nullable enable

using System.Collections.ObjectModel;
using UraniumUI.Material.Controls;
using Xunit;

namespace UraniumUI.Material.Tests;

public class NumericUpDownField_AttachmentRegression_Test
{
    public NumericUpDownField_AttachmentRegression_Test()
    {
        ApplicationExtensions.CreateAndSetMockApplication();
    }

    [Fact]
    public void ReplacingAttachments_ShouldInstallStepperExactlyOnce()
    {
        var control = AnimationReadyHandler.Prepare(new NumericUpDownField());
        var customAttachment = new Label { Text = "Unit" };

        control.Attachments = new ObservableCollection<IView> { customAttachment };

        var bindableAttachments = control
            .GetValue(InputField.AttachmentsProperty)
            .ShouldBeAssignableTo<IList<IView>>();

        bindableAttachments.Count.ShouldBe(2);
        bindableAttachments.ShouldContain(customAttachment);
        bindableAttachments.Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public void NormalValueChanges_ShouldNotRaiseAttachmentsPropertyChanged()
    {
        var control = AnimationReadyHandler.Prepare(new NumericUpDownField());
        var attachmentNotifications = 0;

        control.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(InputField.Attachments))
            {
                attachmentNotifications++;
            }
        };

        control.Value = 3;
        control.Text = "4";
        control.IncrementCommand.Execute(null);

        attachmentNotifications.ShouldBe(0);
    }
}
