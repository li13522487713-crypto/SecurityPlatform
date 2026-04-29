using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Actions.Http;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowRestSecurityPolicyTests
{
    private readonly MicroflowRestSecurityPolicy _policy = new();

    [Theory]
    [InlineData("http://localhost/internal")]
    [InlineData("http://127.0.0.1:5000/internal")]
    [InlineData("http://10.1.2.3/internal")]
    [InlineData("http://172.16.5.4/internal")]
    [InlineData("http://192.168.1.10/internal")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    public async Task EvaluateUrlAsync_blocks_private_network_and_ssrf_targets_by_default(string url)
    {
        var decision = await _policy.EvaluateUrlAsync(
            url,
            DefaultOptions(),
            resolveHostAddresses: false,
            CancellationToken.None);

        Assert.False(decision.Allowed);
        Assert.Equal(RuntimeErrorCode.RuntimeRestPrivateNetworkBlocked, decision.ReasonCode);
        Assert.Equal(url, decision.NormalizedUrl);
    }

    [Fact]
    public async Task EvaluateUrlAsync_blocks_denied_host_before_network_resolution()
    {
        var options = DefaultOptions() with
        {
            DeniedHosts = ["*.example.test"]
        };

        var decision = await _policy.EvaluateUrlAsync(
            "https://api.example.test/orders",
            options,
            resolveHostAddresses: false,
            CancellationToken.None);

        Assert.False(decision.Allowed);
        Assert.Equal(RuntimeErrorCode.RuntimeRestDeniedHost, decision.ReasonCode);
    }

    [Fact]
    public async Task EvaluateAsync_blocks_header_smuggling_fields()
    {
        var request = new MicroflowRuntimeHttpRequest
        {
            Url = "https://api.example.com/orders",
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Host"] = "metadata.google.internal"
            }
        };

        var decision = await _policy.EvaluateAsync(
            request,
            DefaultOptions(),
            resolveHostAddresses: false,
            CancellationToken.None);

        Assert.False(decision.Allowed);
        Assert.Equal(RuntimeErrorCode.RuntimeRestUrlBlocked, decision.ReasonCode);
    }

    [Fact]
    public async Task EvaluateUrlAsync_allows_public_https_when_policy_passes()
    {
        var decision = await _policy.EvaluateUrlAsync(
            "https://api.example.com/orders",
            DefaultOptions(),
            resolveHostAddresses: false,
            CancellationToken.None);

        Assert.True(decision.Allowed);
        Assert.Equal("allowed", decision.ReasonCode);
        Assert.Equal("https://api.example.com/orders", decision.NormalizedUrl);
    }

    private static MicroflowRuntimeHttpOptions DefaultOptions()
        => new()
        {
            AllowPrivateNetwork = false,
            MaxUrlLength = 2048,
            MaxHeaderValueLength = 4096
        };
}
