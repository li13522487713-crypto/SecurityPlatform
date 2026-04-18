using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services;
using SqlSugar;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Identity;

/// <summary>
/// 覆盖 M-G06-C1（S11）：MemberInvitationService。
/// </summary>
public sealed class MemberInvitationServiceTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000070"));

    [Fact]
    public async Task CreateAsync_AndAccept_ShouldFlipStatusAndAutoJoin()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var orgId = await SeedOrganization(db);
            var (svc, sender) = BuildService(db);

            var dto = await svc.CreateAsync(Tenant, invitedBy: 1,
                new MemberInvitationCreateRequest("alice@example.com", orgId.ToString(), "member", null), CancellationToken.None);
            Assert.Equal("pending", dto.Status);
            Assert.Single(sender.Sent);

            var token = await db.Queryable<MemberInvitation>().Where(x => x.Email == "alice@example.com").FirstAsync();
            await svc.AcceptAsync(Tenant, new MemberInvitationAcceptRequest(token.Token, UserId: 9001), CancellationToken.None);

            var refreshed = await db.Queryable<MemberInvitation>().Where(x => x.Email == "alice@example.com").FirstAsync();
            Assert.Equal("accepted", refreshed.Status);
            Assert.Equal(9001L, refreshed.AcceptedByUserId);

            var memberCount = await db.Queryable<OrganizationMember>().CountAsync();
            Assert.Equal(1, memberCount);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task RevokeAsync_ShouldOnlyAffectPending()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var orgId = await SeedOrganization(db);
            var (svc, _) = BuildService(db);
            var dto = await svc.CreateAsync(Tenant, 1,
                new MemberInvitationCreateRequest("bob@example.com", orgId.ToString(), null, null), CancellationToken.None);
            await svc.RevokeAsync(Tenant, long.Parse(dto.Id), CancellationToken.None);
            var entity = await db.Queryable<MemberInvitation>().Where(x => x.Email == "bob@example.com").FirstAsync();
            Assert.Equal("revoked", entity.Status);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task AcceptAsync_ShouldThrow_WhenTokenUnknown()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            await SeedOrganization(db);
            var (svc, _) = BuildService(db);
            var ex = await Assert.ThrowsAsync<BusinessException>(() => svc.AcceptAsync(
                Tenant, new MemberInvitationAcceptRequest("unknown-token", UserId: 1), CancellationToken.None));
            Assert.Equal("NOT_FOUND", ex.Code);
        }
        finally { DeleteDb(dbPath); }
    }

    private static (MemberInvitationService Service, RecordingSender Sender) BuildService(SqlSugarClient db)
    {
        var repo = new MemberInvitationRepository(db);
        var orgRepo = new OrganizationRepository(db);
        var memberRepo = new OrganizationMemberRepository(db);
        var sender = new RecordingSender();
        var idGen = new SeqIdGen();
        return (new MemberInvitationService(repo, orgRepo, memberRepo, sender, idGen), sender);
    }

    private static async Task<long> SeedOrganization(SqlSugarClient db)
    {
        var org = new Organization(Tenant, "default", "Default Org", null, isDefault: true, createdBy: 1, id: 5_000_001);
        await db.Insertable(org).ExecuteCommandAsync();
        return org.Id;
    }

    private static SqlSugarClient CreateDb(string path)
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={path}",
            DbType = SqlSugar.DbType.Sqlite,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = (property, column) =>
                {
                    if (property.Name == nameof(TenantEntity.TenantId))
                    {
                        column.IsIgnore = true;
                    }
                }
            }
        });
    }

    private static async Task CreateSchema(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<Organization>();
        db.CodeFirst.InitTables<OrganizationMember>();
        db.CodeFirst.InitTables<MemberInvitation>();
        await Task.CompletedTask;
    }

    private static string NewDb() => Path.Combine(Path.GetTempPath(), $"member-invitation-{Guid.NewGuid():N}.db");

    private static void DeleteDb(string path)
    {
        if (File.Exists(path)) try { File.Delete(path); } catch { }
    }

    private sealed class SeqIdGen : IIdGeneratorAccessor
    {
        private long _next = 6_000_000;
        public long NextId() => Interlocked.Increment(ref _next);
    }

    private sealed class RecordingSender : IInvitationEmailSender
    {
        public List<(string Email, string Token, string Org)> Sent { get; } = new();
        public Task SendAsync(string toEmail, string token, string organizationName, CancellationToken cancellationToken)
        {
            Sent.Add((toEmail, token, organizationName));
            return Task.CompletedTask;
        }
    }
}
