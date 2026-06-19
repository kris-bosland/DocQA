namespace DocQA.Tests.Browser.Fixtures;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BrowserCollection : ICollectionFixture<BrowserFixture>
{
    public const string Name = "Browser tests";
}