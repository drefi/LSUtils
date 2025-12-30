using System.Collections.Generic;
using NUnit.Framework;
using LSUtils.LSLocale;

namespace LSUtils.Tests.Localization;

[TestFixture]
public class FormatterTokenTests
{
    [Test]
    public void Parse_ShouldReplaceTokensForLanguage()
    {
        var formatter = FormatterToken.Instantiate();
        formatter.SetLanguage(Languages.EN_US, new Dictionary<string, string> { { "name", "World" } });

        var missing = formatter.Parse("Hello {name}!", out var parsed);

        Assert.That(parsed, Is.EqualTo("Hello World!"));
        Assert.That(missing, Is.Empty);
    }

    [Test]
    public void Parse_ShouldReturnMissingTokens()
    {
        var formatter = FormatterToken.Instantiate();
        formatter.SetLanguage(Languages.EN_US);

        var missing = formatter.Parse("Value: {missing}", out var parsed);

        Assert.That(parsed, Is.EqualTo("Value: {missing}"));
        Assert.That(missing, Does.Contain("missing"));
    }

    [Test]
    public void ParseToken_ShouldFallbackToValueLanguage()
    {
        var formatter = FormatterToken.Instantiate();
        formatter.AddToken(Languages.VALUE, "greet", "Olá");

        var token = formatter.ParseToken("greet", Languages.PT_BR);

        Assert.That(token, Is.EqualTo("Olá"));
    }
}
