namespace SyntaxCircus.Email.Tests;

public class EmailMessageTests
{
    [Fact]
    public void Defaults_AreExpected()
    {
        var message = new EmailMessage("to@example.com", "Subject", "Body");

        message.IsBodyHtml.ShouldBeTrue();
        message.From.ShouldBeNull();
        message.Cc.ShouldBeNull();
        message.Bcc.ShouldBeNull();
        message.PlainTextBody.ShouldBeNull();
    }

    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var message = new EmailMessage("to@example.com", "Subject", "Body", false, "from@example.com", ["cc@example.com"], ["bcc@example.com"], "Plain text");

        message.To.ShouldBe("to@example.com");
        message.Subject.ShouldBe("Subject");
        message.Body.ShouldBe("Body");
        message.IsBodyHtml.ShouldBeFalse();
        message.From.ShouldBe("from@example.com");
        message.Cc.ShouldBe(["cc@example.com"]);
        message.Bcc.ShouldBe(["bcc@example.com"]);
        message.PlainTextBody.ShouldBe("Plain text");
    }
}
