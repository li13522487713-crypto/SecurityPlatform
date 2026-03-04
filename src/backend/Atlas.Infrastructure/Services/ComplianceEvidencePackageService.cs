using Atlas.Application.Monitor.Abstractions;
using Atlas.Application.Monitor.Models;
using Microsoft.Extensions.Hosting;
using System.IO.Compression;
using System.Text.Json;

namespace Atlas.Infrastructure.Services;

public sealed class ComplianceEvidencePackageService : IComplianceEvidencePackageService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IHostEnvironment _hostEnvironment;

    public ComplianceEvidencePackageService(IHostEnvironment hostEnvironment)
    {
        _hostEnvironment = hostEnvironment;
    }

    public async Task<ComplianceEvidencePackageResult> BuildPackageAsync(CancellationToken cancellationToken = default)
    {
        var webApiRoot = _hostEnvironment.ContentRootPath;
        var workspaceRoot = ResolveWorkspaceRoot(webApiRoot);
        var generatedAt = DateTimeOffset.UtcNow;
        var fileName = $"compliance-evidence-{generatedAt:yyyyMMddHHmmss}.zip";

        var candidateFiles = new (string EntryName, string AbsolutePath)[]
        {
            ("docs/compliance-evidence-map.md", Path.Combine(workspaceRoot, "docs", "compliance-evidence-map.md")),
            ("backend/appsettings.json", Path.Combine(webApiRoot, "appsettings.json")),
            ("backend/nlog.config", Path.Combine(webApiRoot, "nlog.config")),
            ("backend/Bosch.http/ComplianceEvidence.http", Path.Combine(webApiRoot, "Bosch.http", "ComplianceEvidence.http")),
            ("backend/Bosch.http/Health.http", Path.Combine(webApiRoot, "Bosch.http", "Health.http")),
            ("backend/Bosch.http/ScheduledJobs.http", Path.Combine(webApiRoot, "Bosch.http", "ScheduledJobs.http"))
        };

        var includedFiles = new List<string>();
        var missingFiles = new List<string>();
        await using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in candidateFiles)
            {
                if (!File.Exists(file.AbsolutePath))
                {
                    missingFiles.Add(file.EntryName);
                    continue;
                }

                var entry = archive.CreateEntry(file.EntryName, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await using var fileStream = File.OpenRead(file.AbsolutePath);
                await fileStream.CopyToAsync(entryStream, cancellationToken);
                includedFiles.Add(file.EntryName);
            }

            var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.NoCompression);
            await using var manifestStream = manifestEntry.Open();
            var manifest = new
            {
                generatedAt,
                webApiRoot,
                workspaceRoot,
                includedFiles,
                missingFiles
            };
            await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken);
        }

        return new ComplianceEvidencePackageResult(
            fileName,
            "application/zip",
            output.ToArray(),
            includedFiles,
            missingFiles);
    }

    private static string ResolveWorkspaceRoot(string contentRootPath)
    {
        var current = new DirectoryInfo(contentRootPath);
        while (current is not null)
        {
            var docsPath = Path.Combine(current.FullName, "docs");
            var srcPath = Path.Combine(current.FullName, "src");
            if (Directory.Exists(docsPath) && Directory.Exists(srcPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return contentRootPath;
    }
}
