using WATR.Cli.Services;
using Xunit;

namespace WATR.Tests.Cli;

public class ProjectContextHelperTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configFile;

    public ProjectContextHelperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "watr_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _configFile = Path.Combine(_tempDir, "config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public void ResolveProjectName_ExplicitName_TakesPrecedence()
    {
        var config = new ConfigService(_configFile);
        config.Save(new ConfigData { DefaultProject = "GlobalDefault" });

        var helper = new ProjectContextHelper(config, null!);
        var result = helper.ResolveProjectName("ExplicitProject");

        Assert.Equal("ExplicitProject", result);
    }

    [Fact]
    public void ResolveProjectName_NoExplicit_FallsBackToGlobalDefault()
    {
        var config = new ConfigService(_configFile);
        config.Save(new ConfigData { DefaultProject = "GlobalDefault" });

        var helper = new ProjectContextHelper(config, null!);
        var result = helper.ResolveProjectName(null);

        Assert.Equal("GlobalDefault", result);
    }

    [Fact]
    public async Task ResolveProjectAsync_NoProjectConfigured_ThrowsInvalidOperationException()
    {
        var config = new ConfigService(_configFile);
        var helper = new ProjectContextHelper(config, null!);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => helper.ResolveProjectAsync(null));
        Assert.Contains("No active project context found", ex.Message);
        Assert.Contains("watr_set_project", ex.Message);
    }
}
