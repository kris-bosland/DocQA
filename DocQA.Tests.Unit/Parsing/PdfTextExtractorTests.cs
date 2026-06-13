using System.Reflection;
using DocQA.Server.Parsing;

namespace DocQA.Tests.Unit.Parsing;

public class PdfTextExtractorTests
{
    private static Stream LoadEmbedded(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        var stream = asm.GetManifestResourceStream($"DocQA.Tests.Unit.TestData.{name}");
        return stream ?? throw new InvalidOperationException(
            $"Embedded resource TestData/{name} not found.");
    }

    [Fact]
    public void SamplePdf_ExtractsKnownText()
    {
        using var stream = LoadEmbedded("sample.pdf");

        var text = PdfTextExtractor.ExtractText(stream);

        Assert.Contains("Hello PDF", text);
    }

    [Fact]
    public void EmptyContentPdf_ReturnsEmptyString()
    {
        using var stream = LoadEmbedded("empty.pdf");

        var text = PdfTextExtractor.ExtractText(stream);

        Assert.True(string.IsNullOrWhiteSpace(text));
    }
}
