using DataConfigEditor.Workspace;

namespace DataConfigEditor.Tests.Workspace;

public class RecentDirectoryStoreTests
{
    [Fact]
    public void SaveAndLoad_PreservesMostRecentDirectories()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var store = new RecentDirectoryStore(filePath);

        store.Save(new[] { @"E:\A", @"E:\B" });

        var loaded = store.Load();

        Assert.Equal(new[] { @"E:\A", @"E:\B" }, loaded);
    }
}
