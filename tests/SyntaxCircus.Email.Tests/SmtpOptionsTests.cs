namespace SyntaxCircus.Email.Tests;

public class SmtpOptionsTests
{
    [Fact]
    public void Defaults_AreExpected()
    {
        var options = new SmtpOptions();

        options.Host.ShouldBe(string.Empty);
        options.Port.ShouldBe(587);
        options.Username.ShouldBeNull();
        options.Password.ShouldBeNull();
        options.UseStartTls.ShouldBeTrue();
        options.DefaultFrom.ShouldBe(string.Empty);
        options.MaxRetryAttempts.ShouldBe(3);
    }

    [Fact]
    public void SectionName_IsEmailSmtp()
    {
        SmtpOptions.SectionName.ShouldBe("Email:Smtp");
    }
}
