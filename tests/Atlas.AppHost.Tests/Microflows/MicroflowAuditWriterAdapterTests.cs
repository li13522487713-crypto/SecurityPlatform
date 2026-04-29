using System.Net;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Microflows.Audit;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.AppHost.Microflows.Infrastructure;
using Atlas.Domain.Audit.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

/// <summary>
/// P0-9 验证：MicroflowAuditWriterAdapter 把 MicroflowAuditEvent 正确转成 AuditRecord
/// 并写入 IAuditWriter；写失败时只 swallow 不影响调用方。
/// </summary>
public sealed class MicroflowAuditWriterAdapterTests
{
    [Fact]
    public async Task WriteAsync_PersistsAuditRecordWithMicroflowResource()
    {
        var auditWriter = Substitute.For<IAuditWriter>();
        var accessor = new StubContextAccessor("user-1", "Alice", "ws-1", "00000000-0000-0000-0000-000000000001");
        var http = new HttpContextAccessor { HttpContext = BuildHttpContext("203.0.113.7", "atlas-test-agent") };

        var adapter = new MicroflowAuditWriterAdapter(auditWriter, accessor, http, NullLogger<MicroflowAuditWriterAdapter>.Instance);

        await adapter.WriteAsync(new MicroflowAuditEvent
        {
            Action = "microflow.create",
            Result = "success",
            ResourceId = "mf-1",
            ResourceName = "MF_Demo",
            WorkspaceId = "ws-1",
            Target = "module/MF_Demo",
            Details = new Dictionary<string, object?>
            {
                ["moduleId"] = "module",
                ["folderId"] = null
            }
        }, CancellationToken.None);

        await auditWriter.Received(1).WriteAsync(
            Arg.Is<AuditRecord>(record =>
                record.Action == "microflow.create"
                && record.Result == "success"
                && record.ResourceType == "microflow"
                && record.ResourceId == "mf-1"
                && record.IpAddress == "203.0.113.7"
                && record.UserAgent == "atlas-test-agent"
                && record.Actor == "Alice"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteAsync_SwallowsAuditWriterFailure()
    {
        var auditWriter = Substitute.For<IAuditWriter>();
        auditWriter
            .When(w => w.WriteAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>()))
            .Throw(new InvalidOperationException("audit infra is down"));

        var adapter = new MicroflowAuditWriterAdapter(
            auditWriter,
            new StubContextAccessor("user-1", "Alice", "ws", null),
            new HttpContextAccessor(),
            NullLogger<MicroflowAuditWriterAdapter>.Instance);

        // 异常吞掉，不会冒泡
        await adapter.WriteAsync(new MicroflowAuditEvent
        {
            Action = "microflow.delete",
            Result = "failure",
            ResourceId = "mf-1",
            ErrorCode = "MICROFLOW_REFERENCE_BLOCKED",
        }, CancellationToken.None);

        await auditWriter.Received(1).WriteAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    private static DefaultHttpContext BuildHttpContext(string ip, string userAgent)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        context.Request.Headers.UserAgent = userAgent;
        return context;
    }

    private sealed class StubContextAccessor : IMicroflowRequestContextAccessor
    {
        public StubContextAccessor(string userId, string userName, string workspace, string? tenant)
        {
            Current = new MicroflowRequestContext
            {
                UserId = userId,
                UserName = userName,
                WorkspaceId = workspace,
                TenantId = tenant,
                TraceId = "trace",
            };
        }

        public MicroflowRequestContext Current { get; }
    }
}
