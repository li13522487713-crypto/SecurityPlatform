using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class PluginCatalogServiceTests
{
    [Fact]
    public async Task GetPluginsAsync_WhenPluginDirectoryEmpty_ShouldReturnEmpty()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"atlas-plugin-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(rootPath, "plugins"));

        try
        {
            var hostEnvironment = Substitute.For<IHostEnvironment>();
            hostEnvironment.ContentRootPath.Returns(rootPath);
            var options = Options.Create(new PluginCatalogOptions
            {
                RootPath = "plugins"
            });
            var service = new PluginCatalogService(
                options,
                hostEnvironment,
                NullLogger<PluginCatalogService>.Instance);

            var plugins = await service.GetPluginsAsync(CancellationToken.None);
            Assert.Empty(plugins);

            await service.ReloadAsync(CancellationToken.None);
            var pluginsAfterReload = await service.GetPluginsAsync(CancellationToken.None);
            Assert.Empty(pluginsAfterReload);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }
}
