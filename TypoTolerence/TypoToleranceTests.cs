using DotNet.Testcontainers.Builders;

using Meilisearch;

using System.ComponentModel;

using Xunit;

namespace TypoTolerence;

public class TypoToleranceTests
{
    private static readonly HashSet<int> _usedPorts = [];

    public static int GetAvailablePort()
    {
        var rnd = new Random();
        var port = rnd.Next(7700, 9800);
        while (_usedPorts.Contains(port))
        {
            port = rnd.Next(7700, 9800);
        }

        _usedPorts.Add(port);
        return port;
    }

    [Theory]
    [InlineData("v1.11.2")]
    [InlineData("v1.11.3")]
    [InlineData("v1.12.0-rc.1")]
    [InlineData("v1.12.0-rc.2")]
    [InlineData("v1.12.0-rc.3")]
    [InlineData("v1.12.0-rc.4")]
    [InlineData("v1.12.0-rc.5")]
    [InlineData("v1.12.0")]
    [InlineData("v1.12.1")]
    [InlineData("v1.12.2")]
    [InlineData("v1.12.3")]
    [InlineData("v1.12.4")]
    [InlineData("v1.12.5")]
    [InlineData("v1.12.6")]
    [InlineData("v1.12.7")]
    [InlineData("v1.12.8")]
    [InlineData("v1.13.0")]
    [InlineData("nightly")]
    public async Task SearchOnTypoToleranceDisabledAttribute(string version)
    {
        var port = GetAvailablePort();
        var meiliSearchContainer = new ContainerBuilder()
            .WithName($"MeiliSearch-{version}")
            .WithImage($"getmeili/meilisearch:{version}")
            .WithPortBinding(port, 7700)
            .Build();

        await meiliSearchContainer.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(1000, TestContext.Current.CancellationToken); // ???
        var client = new MeilisearchClient($"http://localhost:{port}", "MASTER_KEY");

        var indexName = "MyIndex";

        var createIndexTask = await client.CreateIndexAsync(indexName, cancellationToken: TestContext.Current.CancellationToken);
        await client.WaitForTaskAsync(createIndexTask.TaskUid, cancellationToken: TestContext.Current.CancellationToken);
        var index = client.Index(indexName);

        await index.UpdateTypoToleranceAsync(new TypoTolerance { DisableOnAttributes = [nameof(MySearchableObject.myImportantAttribute)] }, TestContext.Current.CancellationToken);
        await index.UpdateSearchableAttributesAsync([nameof(MySearchableObject.myImportantAttribute)], TestContext.Current.CancellationToken);

        var addDocumentTask = await index.AddDocumentsAsync(
            documents: [new MySearchableObject { myImportantAttribute = "myImportantValue" }],
            primaryKey: nameof(MySearchableObject.myImportantAttribute),
            cancellationToken: TestContext.Current.CancellationToken);
        await client.WaitForTaskAsync(addDocumentTask.TaskUid, cancellationToken: TestContext.Current.CancellationToken);
        var searchResults = await index.SearchAsync<MySearchableObject>("myImportantValue", cancellationToken: TestContext.Current.CancellationToken);

        var dumpTask = await client.CreateDumpAsync(cancellationToken: TestContext.Current.CancellationToken);
        await client.WaitForTaskAsync(dumpTask.TaskUid, cancellationToken: TestContext.Current.CancellationToken);
        await meiliSearchContainer.ExecAsync(["tar", "-czvf", "/tmp/dump.tar.gz", "/meili_data/dumps"], TestContext.Current.CancellationToken);
        var bytes = await meiliSearchContainer.ReadFileAsync("/tmp/dump.tar.gz", TestContext.Current.CancellationToken);
        File.WriteAllBytes($"dump-{version}.tar.gz", bytes);

        Assert.Single(searchResults.Hits);
        Assert.Equal("myImportantValue", searchResults.Hits.Single().myImportantAttribute);

    }

    public class MySearchableObject
    {
        public required string myImportantAttribute { get; set; }
    }
}
