using LMC.Basic;

namespace LineLauncherCs.Tests;

public class LineFileParserTests
{
    [Fact]
    public void WriteAndRead_CreatesFileAndPersistsValue()
    {
        using var scope = new TestFileSystemScope();
        var parser = new LineFileParser();
        var path = scope.GetPath("sample.line");

        parser.Write(path, "foo", "bar", "main");

        Assert.Equal("bar", parser.Read(path, "foo", "main"));
    }

    [Fact]
    public void Write_OverwritesExistingKeyInSection()
    {
        using var scope = new TestFileSystemScope();
        var parser = new LineFileParser();
        var path = scope.GetPath("sample.line");
        parser.Write(path, "foo", "bar", "main");

        parser.Write(path, "foo", "baz", "main");

        Assert.Equal("baz", parser.Read(path, "foo", "main"));
        Assert.Single(parser.GetKeySet(path, "main"));
    }

    [Fact]
    public void GetSectionsAndKeys_ReturnsStructuredContent()
    {
        using var scope = new TestFileSystemScope();
        var parser = new LineFileParser();
        var path = scope.GetPath("sample.line");
        parser.Write(path, "foo", "bar", "alpha");
        parser.Write(path, "bar", "baz", "beta");

        Assert.Equal(["alpha", "beta"], parser.GetSections(path));
        Assert.Equal(["foo"], parser.GetKeySet(path, "alpha"));
    }

    [Fact]
    public void Delete_RemovesSingleKeyButKeepsSection()
    {
        using var scope = new TestFileSystemScope();
        var parser = new LineFileParser();
        var path = scope.GetPath("sample.line");
        parser.Write(path, "foo", "bar", "alpha");
        parser.Write(path, "bar", "baz", "alpha");

        parser.Delete(path, "foo", "alpha");

        Assert.Null(parser.Read(path, "foo", "alpha"));
        Assert.Equal("baz", parser.Read(path, "bar", "alpha"));
    }

    [Fact]
    public void DeleteSection_RemovesWholeSection()
    {
        using var scope = new TestFileSystemScope();
        var parser = new LineFileParser();
        var path = scope.GetPath("sample.line");
        parser.Write(path, "foo", "bar", "alpha");
        parser.Write(path, "bar", "baz", "beta");

        parser.DeleteSection(path, "alpha");

        Assert.DoesNotContain("alpha", parser.GetSections(path));
        Assert.Equal("baz", parser.Read(path, "bar", "beta"));
    }

    [Fact]
    public void Write_RejectsPipeInTokens()
    {
        using var scope = new TestFileSystemScope();
        var parser = new LineFileParser();
        var path = scope.GetPath("sample.line");

        Assert.Throws<ArgumentException>(() => parser.Write(path, "foo|", "bar", "alpha"));
    }
}
