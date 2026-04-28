using Atlas.Application.Audit.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using Atlas.Infrastructure.Services.LowCode;
using NSubstitute;

namespace Atlas.SecurityPlatform.Tests.LowCode;

/// <summary>
/// P0-1 守门测试：<see cref="RuntimeSessionService.SwitchAsync"/> 修复前后端契约断裂。
/// 此前 HttpSessionAdapter 调用 POST /api/runtime/sessions/{id}:switch 但后端无路由 → 404。
/// 现要求：
///   - 不存在 sessionId → BusinessException(NOT_FOUND)；
///   - 跨用户访问 → BusinessException(FORBIDDEN)；
///   - 同用户成功 → Touch + 审计 + 返回 RuntimeSessionInfo。
/// </summary>
public sealed class RuntimeSessionSwitchTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private const long OwnerUser = 100L;
    private const long OtherUser = 200L;

    private static (RuntimeSessionService Service, ILowCodeSessionRepository Repo, IAuditWriter Audit)
        BuildService(LowCodeSession? seedSession)
    {
        var repo = Substitute.For<ILowCodeSessionRepository>();
        if (seedSession is not null)
        {
            repo.FindBySessionIdAsync(Arg.Any<TenantId>(), seedSession.SessionId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<LowCodeSession?>(seedSession));
        }
        var idGen = Substitute.For<IIdGeneratorAccessor>();
        idGen.NextId().Returns(42L);
        var audit = Substitute.For<IAuditWriter>();
        return (new RuntimeSessionService(repo, idGen, audit), repo, audit);
    }

    [Fact]
    public async Task SwitchAsync_NotFound_Throws()
    {
        var (svc, _, _) = BuildService(seedSession: null);
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            svc.SwitchAsync(Tenant, OwnerUser, "no-such-sess", default));
        Assert.Equal(ErrorCodes.NotFound, ex.Code);
    }

    [Fact]
    public async Task SwitchAsync_CrossUser_ReturnsForbidden_AndAudits()
    {
        var sess = new LowCodeSession(Tenant, 1L, "sess_owner", OwnerUser, "原所有者会话");
        var (svc, _, audit) = BuildService(sess);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            svc.SwitchAsync(Tenant, OtherUser, sess.SessionId, default));
        Assert.Equal(ErrorCodes.Forbidden, ex.Code);

        // 审计：跨用户拒绝必须留痕（reason:forbidden）
        await audit.Received().WriteAsync(
            Arg.Is<Atlas.Domain.Audit.Entities.AuditRecord>(r =>
                r.Action == "lowcode.runtime.session.switch" &&
                r.Result == "failed"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchAsync_OwnerSucceeds_TouchUpdated_AuditWritten_ReturnsInfo()
    {
        var sess = new LowCodeSession(Tenant, 1L, "sess_ok", OwnerUser, "我的会话");
        var beforeUpdated = sess.UpdatedAt;
        await Task.Delay(10); // 让 Touch 时间发生肉眼可见的变化

        var (svc, repo, audit) = BuildService(sess);
        var info = await svc.SwitchAsync(Tenant, OwnerUser, sess.SessionId, default);

        Assert.Equal(sess.SessionId, info.Id);
        Assert.Equal("我的会话", info.Title);
        Assert.False(info.Archived);
        Assert.True(sess.UpdatedAt > beforeUpdated, "SwitchAsync 应触发 Touch 更新 UpdatedAt");

        await repo.Received().UpdateAsync(sess, Arg.Any<CancellationToken>());
        await audit.Received().WriteAsync(
            Arg.Is<Atlas.Domain.Audit.Entities.AuditRecord>(r =>
                r.Action == "lowcode.runtime.session.switch" &&
                r.Result == "success"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchAsync_ArchivedSession_StillReturnsInfo_WithArchivedFlag()
    {
        var sess = new LowCodeSession(Tenant, 1L, "sess_arch", OwnerUser, "归档会话");
        sess.Archive(true); // 归档允许切入但不自动恢复 active；info.Archived 应为 true
        var (svc, _, _) = BuildService(sess);
        var info = await svc.SwitchAsync(Tenant, OwnerUser, sess.SessionId, default);
        Assert.True(info.Archived);
    }
}
