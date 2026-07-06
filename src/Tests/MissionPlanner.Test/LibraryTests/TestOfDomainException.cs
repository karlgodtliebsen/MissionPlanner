using MissionPlanner.Library;

namespace MissionPlanner.Test.LibraryTests;

/// <summary>
/// 
/// </summary>
/// <param name="output"></param>
public class TestOfDomainException(ITestOutputHelper output)
{
    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public void VerifyDomainExceptionCanThrowExceptions()
    {
        var subjectText = "Test Subject";
        var failureText = "Failure Test Subject";

        var testSubject = subjectText;

        DomainException.ThrowIfNull(testSubject, subjectText, nameof(testSubject));

        testSubject = null;

        Assert.Throws<DomainException>(() => DomainException.ThrowIfNull(testSubject, failureText, nameof(testSubject)));
        Assert.Throws<DomainException>(() => DomainException.ThrowIfNull(testSubject, failureText));

        Assert.Throws<DomainException>(() => DomainException.ThrowIfNull(testSubject, () => failureText));
        Assert.Throws<DomainException>(() => DomainException.ThrowIfNull(testSubject, () => failureText, nameof(testSubject)));
        Assert.Throws<ArgumentNullException>(() => ArgumentNullException.ThrowIfNull(testSubject, nameof(testSubject)));
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public void VerifyDomainExceptionCanThrowWellFormattedExceptions()
    {
        var subjectText = "Test Subject";
        var failureText = "Failure using Test Subject";

        var testSubject = subjectText;

        DomainException.ThrowIfNull(testSubject, subjectText, nameof(testSubject));

        testSubject = null;

        Assert.Throws<DomainException>(() =>
        {
            try
            {
                DomainException.ThrowIfNull(testSubject, failureText, nameof(testSubject));
            }
            catch (Exception ex)
            {
                output.WriteLine(ex.Message);
                Assert.Contains(failureText, ex.Message);
                Assert.Contains(nameof(testSubject), ex.Message);
                throw;
            }
        });


        Assert.Throws<DomainException>(() =>
        {
            try
            {
                DomainException.ThrowIfNull(testSubject, failureText);
            }
            catch (Exception ex)
            {
                output.WriteLine(ex.Message);
                Assert.Contains(failureText, ex.Message);
                Assert.Contains(nameof(testSubject), ex.Message);
                throw;
            }
        });


        Assert.Throws<DomainException>(() =>
        {
            try
            {
                DomainException.ThrowIfNull(testSubject, () => failureText);
            }
            catch (Exception ex)
            {
                output.WriteLine(ex.Message);
                Assert.Contains(failureText, ex.Message);
                Assert.Contains(nameof(testSubject), ex.Message);
                throw;
            }
        });


        Assert.Throws<DomainException>(() =>
        {
            try
            {
                DomainException.ThrowIfNull(testSubject, () => failureText, nameof(testSubject));
            }
            catch (Exception ex)
            {
                output.WriteLine(ex.Message);
                Assert.Contains(failureText, ex.Message);
                Assert.Contains(nameof(testSubject), ex.Message);
                throw;
            }
        });
    }
}
