using Atlas.Infrastructure.Services;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using System.IO.Compression;
using System.Text;

namespace Atlas.SecurityPlatform.Tests.Services;

public sealed class ComplianceEvidencePackageServiceTests
{
    [Fact]
    public async Task BuildPackageAsync_ShouldCreateZipAndIncludeEvidenceEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), $"atlas-compliance-{Guid.NewGuid():N}");
        var docsDir = Path.Combine(root, "docs");
        var webApiRoot = Path.Combine(root, "src", "backend", "Atlas.AppHost");
        var boschDir = Path.Combine(webApiRoot, "Bosch.http");
        Directory.CreateDirectory(docsDir);
        Directory.CreateDirectory(boschDir);

        await File.WriteAllTextAsync(Path.Combine(docsDir, "compliance-evidence-map.md"), "# compliance map");
        await File.WriteAllTextAsync(Path.Combine(webApiRoot, "appsettings.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(webApiRoot, "nlog.config"), "<nlog />");
        await File.WriteAllTextAsync(Path.Combine(boschDir, "ComplianceEvidence.http"), "GET /api/v1/audit");
        await File.WriteAllTextAsync(Path.Combine(boschDir, "Health.http"), "GET /api/v1/health");
        await File.WriteAllTextAsync(Path.Combine(boschDir, "ScheduledJobs.http"), "GET /api/v1/scheduled-jobs");

        try
        {
            var hostEnvironment = Substitute.For<IHostEnvironment>();
            hostEnvironment.ContentRootPath.Returns(webApiRoot);
            var service = new ComplianceEvidencePackageService(hostEnvironment);

            var result = await service.BuildPackageAsync(CancellationToken.None);

            Assert.EndsWith(".zip", result.FileName, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("application/zip", result.ContentType);
            Assert.NotEmpty(result.Content);
            Assert.Contains("docs/compliance-evidence-map.md", result.IncludedFiles);
            Assert.Empty(result.MissingFiles);

            await using var stream = new MemoryStream(result.Content);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            Assert.NotNull(zip.GetEntry("manifest.json"));
            Assert.NotNull(zip.GetEntry("docs/compliance-evidence-map.md"));
            Assert.NotNull(zip.GetEntry("backend/appsettings.json"));

            var manifestEntry = zip.GetEntry("manifest.json");
            Assert.NotNull(manifestEntry);
            await using var manifestStream = manifestEntry!.Open();
            using var reader = new StreamReader(manifestStream, Encoding.UTF8);
            var manifest = await reader.ReadToEndAsync();
            Assert.Contains("includedFiles", manifest, StringComparison.Ordinal);
            Assert.Contains("backend/Bosch.http/ComplianceEvidence.http", manifest, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
