using RigKit.Infrastructure.Configuration;

namespace RigKit.UnitTests;

public sealed class JsonConfigStoreTests
{
    [Fact]
    public void LoadOrCreate_WritesDefaults_WhenFileIsMissing()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rigkit-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "app.json");
        try
        {
            var store = new JsonConfigStore(path);
            var config = store.LoadOrCreate();

            Assert.True(File.Exists(path));
            Assert.Equal(1, config.PollIntervalSeconds);
            Assert.Equal("light", config.Theme);
        }
        finally
        {
            Delete(path);
        }
    }

    [Fact]
    public void Save_RoundTripsAndNormalizes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rigkit-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "app.json");
        try
        {
            var store = new JsonConfigStore(path);
            store.Save(new AppConfig(
                PollIntervalSeconds: 0,
                Theme: "dark",
                HistoryCapacity: 10,
                SidebarCollapsed: true));

            var config = store.LoadOrCreate();

            Assert.Equal(1, config.PollIntervalSeconds);
            Assert.Equal("dark", config.Theme);
            Assert.Equal(60, config.HistoryCapacity);
            Assert.True(config.SidebarCollapsed);
        }
        finally
        {
            Delete(path);
        }
    }

    private static void Delete(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
