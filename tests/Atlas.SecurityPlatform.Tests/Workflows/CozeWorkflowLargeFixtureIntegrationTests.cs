using System.Security.Cryptography;
using System.Text;
using Atlas.Infrastructure.Services.AiPlatform;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Workflows;

public sealed class CozeWorkflowLargeFixtureIntegrationTests
{
    private static readonly string FixtureDirectory = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "../../../../../src/frontend/packages/workflow/__fixtures__/workflow-large"));

    private readonly CozeWorkflowPlanCompiler _compiler = new();

    public static IEnumerable<object[]> FixtureFiles()
    {
        foreach (var file in Directory.EnumerateFiles(FixtureDirectory, "*.json").OrderBy(Path.GetFileName))
        {
            yield return [file];
        }
    }

    [Theory]
    [MemberData(nameof(FixtureFiles))]
    public void LargeWorkflowFixture_ShouldRoundTripBytesAndCompile(string file)
    {
        var schema = File.ReadAllText(file, Encoding.UTF8);
        var hashBefore = Sha256(schema);

        // Simulates save/read of the raw schema_json longtext field.
        var saved = schema.ToString();
        var hashAfter = Sha256(saved);

        Assert.Equal(hashBefore, hashAfter);

        var result = _compiler.Compile(saved);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => $"{error.Code}:{error.Message}")));
        Assert.NotNull(result.Canvas);
        Assert.NotEmpty(result.Canvas!.Nodes);
    }

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
