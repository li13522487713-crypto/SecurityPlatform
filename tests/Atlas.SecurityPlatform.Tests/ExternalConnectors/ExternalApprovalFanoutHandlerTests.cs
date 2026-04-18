using Atlas.Application.Approval.Abstractions;
using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Models;
using Atlas.Connectors.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.ExternalConnectors.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.ExternalConnectors;

/// <summary>
/// 验证 ExternalApprovalFanoutHandler 把本地 IApprovalEventHandler 事件按预期路由到 dispatch service。
/// </summary>
public sealed class ExternalApprovalFanoutHandlerTests
{
    [Fact]
    public async Task OnInstanceStarted_DelegatesToDispatchService_WithBusinessKey()
    {
        var fake = new FakeDispatchService();
        var handler = new ExternalApprovalFanoutHandler(fake, NullLogger<ExternalApprovalFanoutHandler>.Instance);
        var evt = new ApprovalInstanceEvent(
            new TenantId(Guid.NewGuid()),
            InstanceId: 12345,
            DefinitionId: 678,
            BusinessKey: "leave-2026-04-18-001",
            DataJson: "{ \"days\": 3, \"reason\": \"family\" }",
            ActorUserId: 999);

        await handler.OnInstanceStartedAsync(evt, CancellationToken.None);

        Assert.Single(fake.StartedCalls);
        var (instanceId, defId, payload) = fake.StartedCalls[0];
        Assert.Equal(12345, instanceId);
        Assert.Equal(678, defId);
        Assert.Equal("leave-2026-04-18-001", payload.BusinessKey);
        Assert.True(payload.Fields.ContainsKey("days"));
        Assert.Equal("number", payload.Fields["days"].ValueType);
        Assert.Equal("string", payload.Fields["reason"].ValueType);
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    [InlineData("Canceled")]
    public async Task TerminalStatus_ForwardsToOnInstanceStatusChanged(string statusName)
    {
        var fake = new FakeDispatchService();
        var handler = new ExternalApprovalFanoutHandler(fake, NullLogger<ExternalApprovalFanoutHandler>.Instance);
        var evt = new ApprovalInstanceEvent(
            new TenantId(Guid.NewGuid()),
            InstanceId: 1,
            DefinitionId: 2,
            BusinessKey: "k",
            DataJson: null,
            ActorUserId: 3);

        switch (statusName)
        {
            case "Approved":
                await handler.OnInstanceCompletedAsync(evt, CancellationToken.None);
                Assert.Equal(ExternalApprovalStatus.Approved, fake.StatusCalls[0].Status);
                break;
            case "Rejected":
                await handler.OnInstanceRejectedAsync(evt, CancellationToken.None);
                Assert.Equal(ExternalApprovalStatus.Rejected, fake.StatusCalls[0].Status);
                break;
            case "Canceled":
                await handler.OnInstanceCanceledAsync(evt, CancellationToken.None);
                Assert.Equal(ExternalApprovalStatus.Canceled, fake.StatusCalls[0].Status);
                break;
        }
        Assert.Single(fake.StatusCalls);
        Assert.Equal(1, fake.StatusCalls[0].LocalInstanceId);
    }

    [Fact]
    public async Task OnInstanceStarted_SwallowsDispatchExceptions_DoesNotPropagate()
    {
        var fake = new FakeDispatchService { ThrowOnStarted = true };
        var handler = new ExternalApprovalFanoutHandler(fake, NullLogger<ExternalApprovalFanoutHandler>.Instance);
        var evt = new ApprovalInstanceEvent(new TenantId(Guid.NewGuid()), 1, 2, "k", null, 3);

        // Must not throw — fan-out failure should not break the local approval flow.
        await handler.OnInstanceStartedAsync(evt, CancellationToken.None);
    }

    private sealed class FakeDispatchService : IExternalApprovalDispatchService
    {
        public List<(long InstanceId, long DefinitionId, ExternalApprovalSubmission Payload)> StartedCalls { get; } = new();

        public List<(long LocalInstanceId, ExternalApprovalStatus Status, string? Comment)> StatusCalls { get; } = new();

        public bool ThrowOnStarted { get; init; }

        public Task<ExternalApprovalDispatchResult> OnInstanceStartedAsync(long localInstanceId, long flowDefinitionId, ExternalApprovalSubmission payload, CancellationToken cancellationToken)
        {
            if (ThrowOnStarted)
            {
                throw new InvalidOperationException("simulated failure");
            }
            StartedCalls.Add((localInstanceId, flowDefinitionId, payload));
            return Task.FromResult(new ExternalApprovalDispatchResult { Pushed = true, ExternalInstanceId = "ext-1", ProviderType = "wecom", ProviderId = 7 });
        }

        public Task OnInstanceStatusChangedAsync(long localInstanceId, ExternalApprovalStatus newStatus, string? commentText, CancellationToken cancellationToken)
        {
            StatusCalls.Add((localInstanceId, newStatus, commentText));
            return Task.CompletedTask;
        }
    }
}
