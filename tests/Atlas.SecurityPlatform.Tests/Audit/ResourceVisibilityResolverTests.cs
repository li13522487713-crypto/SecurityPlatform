using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.Authorization;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Services.Audit;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Audit;

/// <summary>
/// 覆盖 M-G04-C2（S8）：ResourceVisibilityResolver。
/// </summary>
public sealed class ResourceVisibilityResolverTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000040"));

    [Fact]
    public async Task FilterVisibleAsync_PlatformAdmin_ShouldReturnAll()
    {
        var resolver = new ResourceVisibilityResolver(new RecordingGuard());
        var candidates = new[]
        {
            ("agent", "1"),
            ("workflow", "2")
        };
        var result = await resolver.FilterVisibleAsync(Tenant, userId: 1, isPlatformAdmin: true, candidates, CancellationToken.None);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task FilterVisibleAsync_NormalUser_ShouldOnlyReturnAllowed()
    {
        var guard = new RecordingGuard
        {
            AllowResourceIds = new HashSet<long> { 1L }
        };
        var resolver = new ResourceVisibilityResolver(guard);
        var candidates = new[]
        {
            ("agent", "1"),
            ("agent", "2"),
            ("workflow", "3")
        };
        var result = await resolver.FilterVisibleAsync(Tenant, userId: 9527, isPlatformAdmin: false, candidates, CancellationToken.None);
        Assert.Single(result);
        Assert.Equal(("agent", "1"), result.First());
    }

    [Fact]
    public async Task FilterVisibleAsync_EmptyOrInvalidIds_ShouldBeSkipped()
    {
        var resolver = new ResourceVisibilityResolver(new RecordingGuard { AllowResourceIds = new HashSet<long> { 1, 2 } });
        var result = await resolver.FilterVisibleAsync(
            Tenant, 1, isPlatformAdmin: false,
            new (string, string)[]
            {
                ("", "1"),
                ("agent", ""),
                ("agent", "abc-not-long"),
                ("agent", "1")
            }, CancellationToken.None);
        Assert.Single(result);
    }

    private sealed class RecordingGuard : IResourceAccessGuard
    {
        public HashSet<long> AllowResourceIds { get; set; } = new();

        public Task<ResourceAccessDecision> CheckAsync(ResourceAccessQuery query, CancellationToken cancellationToken)
        {
            var allowed = query.ResourceId.HasValue && AllowResourceIds.Contains(query.ResourceId.Value);
            return Task.FromResult(new ResourceAccessDecision(allowed, allowed ? null : "denied", allowed ? "resource" : null));
        }

        public Task RequireAsync(ResourceAccessQuery query, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
