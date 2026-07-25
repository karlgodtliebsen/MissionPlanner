#nullable enable

using Shouldly;
using UraniumUI.Material.Controls;
using UraniumUI.Tests.Core;
using Xunit;

namespace UraniumUI.Material.Tests;

public class NumericUpDownField_AttachmentRegression_Test
{
    public NumericUpDownField_AttachmentRegression_Test()
    {
        ApplicationExtensions.CreateAndSetMockApplication();
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
