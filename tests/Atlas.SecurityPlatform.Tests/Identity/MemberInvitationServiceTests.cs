using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Options;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services;
using Microsoft.Extensions.Options;
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
            // 显式传 UserId 走 "已存在用户" 路径：先种 9001
            await SeedExistingUserAsync(db, 9001L, "alice@example.com");
            var resp = await svc.AcceptAsync(Tenant, new MemberInvitationAcceptRequest(token.Token, UserId: 9001), CancellationToken.None);
            Assert.Equal(9001L, resp.UserId);
            Assert.Null(resp.SetPasswordToken); // 已存在用户不发 set-password token

            var refreshed = await db.Queryable<MemberInvitation>().Where(x => x.Email == "alice@example.com").FirstAsync();
            Assert.Equal("accepted", refreshed.Status);
            Assert.Equal(9001L, refreshed.AcceptedByUserId);

            var memberCount = await db.Queryable<OrganizationMember>().CountAsync();
            Assert.Equal(1, memberCount);
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task AcceptAsync_ShouldAutoCreateUser_WhenUnknownEmail()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var orgId = await SeedOrganization(db);
            var (svc, _) = BuildService(db);
            var dto = await svc.CreateAsync(Tenant, 1,
                new MemberInvitationCreateRequest("carol@example.com", orgId.ToString(), "member", null), CancellationToken.None);

            var token = (await db.Queryable<MemberInvitation>().Where(x => x.Email == "carol@example.com").FirstAsync()).Token;

            // 不传 UserId 且邮箱无对应账号 → 自动创建 pending-activation
            var resp = await svc.AcceptAsync(Tenant, new MemberInvitationAcceptRequest(token, UserId: null), CancellationToken.None);
            Assert.True(resp.UserId > 0);
            Assert.False(string.IsNullOrEmpty(resp.SetPasswordToken));

            var user = await db.Queryable<UserAccount>().Where(x => x.Id == resp.UserId).FirstAsync();
            Assert.Equal(UserAccount.StatusPendingActivation, user.Status);
            Assert.False(user.IsActive);
            Assert.Equal("carol@example.com", user.Email);

            var inv = await db.Queryable<MemberInvitation>().Where(x => x.Email == "carol@example.com").FirstAsync();
            Assert.Equal(resp.SetPasswordToken, inv.SetPasswordToken);
            Assert.True(inv.IsSetPasswordTokenValid());
        }
        finally { DeleteDb(dbPath); }
    }

    [Fact]
    public async Task SetPasswordAsync_ShouldActivateUser_AndConsumeToken()
    {
        var dbPath = NewDb();
        using var db = CreateDb(dbPath);
        try
        {
            await CreateSchema(db);
            var orgId = await SeedOrganization(db);
            var (svc, _) = BuildService(db);
            var dto = await svc.CreateAsync(Tenant, 1,
                new MemberInvitationCreateRequest("dave@example.com", orgId.ToString(), "member", null), CancellationToken.None);
            var token = (await db.Queryable<MemberInvitation>().Where(x => x.Email == "dave@example.com").FirstAsync()).Token;
            var resp = await svc.AcceptAsync(Tenant, new MemberInvitationAcceptRequest(token, UserId: null), CancellationToken.None);
            Assert.NotNull(resp.SetPasswordToken);

            await svc.SetPasswordAsync(Tenant,
                new MemberInvitationSetPasswordRequest(resp.SetPasswordToken!, "Aa1!Strong#" + Guid.NewGuid().ToString("N")[..6]),
                CancellationToken.None);

            var user = await db.Queryable<UserAccount>().Where(x => x.Id == resp.UserId).FirstAsync();
            Assert.Equal(UserAccount.StatusActive, user.Status);
            Assert.True(user.IsActive);

            var inv = await db.Queryable<MemberInvitation>().Where(x => x.Email == "dave@example.com").FirstAsync();
            Assert.Null(inv.SetPasswordToken);
            Assert.NotNull(inv.PasswordSetAt);

            // 反例：token 已被消费 → 再次设置密码失败
            var ex = await Assert.ThrowsAsync<BusinessException>(() => svc.SetPasswordAsync(Tenant,
                new MemberInvitationSetPasswordRequest(resp.SetPasswordToken!, "Aa1!Different#abc"),
                CancellationToken.None));
            Assert.Equal("NOT_FOUND", ex.Code);
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
        var userRepo = new InMemoryUserAccountRepository(db);
        var hasher = new IdentityPasswordHasher();
        var policyMonitor = new StaticOptionsMonitor<PasswordPolicyOptions>(new PasswordPolicyOptions
        {
            MinLength = 8,
            RequireUppercase = true,
            RequireLowercase = true,
            RequireDigit = true,
            RequireNonAlphanumeric = true
        });
        return (new MemberInvitationService(repo, orgRepo, memberRepo, sender, idGen, userRepo, hasher, policyMonitor), sender);
    }

    private static async Task SeedExistingUserAsync(SqlSugarClient db, long userId, string email)
    {
        var user = new UserAccount(Tenant, email, email, "$2b$12$placeholderhashplaceholder.placeholderhashplaceholder.aaa", userId);
        user.UpdateProfile(email, email, null);
        await db.Insertable(user).ExecuteCommandAsync();
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
                    // SQLite 下 DateTimeOffset 派生属性会让 SqlSugar 反射时尝试列绑定 → 显式忽略
                    if (property.PropertyType == typeof(DateTimeOffset))
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
        db.CodeFirst.InitTables<UserAccount>();
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

    /// <summary>测试用：直接复用 SqlSugar 持久化的 UserAccountRepository（精简实现）。</summary>
    private sealed class InMemoryUserAccountRepository : IUserAccountRepository
    {
        private readonly ISqlSugarClient _db;
        public InMemoryUserAccountRepository(ISqlSugarClient db) => _db = db;

        public Task<UserAccount?> FindByUsernameAsync(TenantId tenantId, string username, CancellationToken cancellationToken)
            => _db.Queryable<UserAccount>().Where(x => x.Username == username).FirstAsync(cancellationToken)!;

        public Task<UserAccount?> FindByEmailAsync(TenantId tenantId, string email, CancellationToken cancellationToken)
            => _db.Queryable<UserAccount>().Where(x => x.Email == email).FirstAsync(cancellationToken)!;

        public Task<UserAccount?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
            => _db.Queryable<UserAccount>().Where(x => x.Id == id).FirstAsync(cancellationToken)!;

        public Task<(IReadOnlyList<UserAccount> Items, int TotalCount)> QueryPageAsync(TenantId tenantId, int pageIndex, int pageSize, string? keyword, CancellationToken cancellationToken)
            => Task.FromResult<(IReadOnlyList<UserAccount>, int)>((Array.Empty<UserAccount>(), 0));

        public Task<(IReadOnlyList<UserAccount> Items, int TotalCount)> QueryPageByIdsAsync(TenantId tenantId, IReadOnlyList<long> userIds, int pageIndex, int pageSize, string? keyword, CancellationToken cancellationToken)
            => Task.FromResult<(IReadOnlyList<UserAccount>, int)>((Array.Empty<UserAccount>(), 0));

        public Task<IReadOnlyList<UserAccount>> QueryByIdsAsync(TenantId tenantId, IReadOnlyList<long> userIds, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<UserAccount>>(Array.Empty<UserAccount>());

        public Task<IReadOnlyList<UserAccount>> QueryByUsernamesAsync(TenantId tenantId, IReadOnlyList<string> usernames, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<UserAccount>>(Array.Empty<UserAccount>());

        public async Task<bool> ExistsByUsernameAsync(TenantId tenantId, string username, CancellationToken cancellationToken)
        {
            var count = await _db.Queryable<UserAccount>().Where(x => x.Username == username).CountAsync(cancellationToken);
            return count > 0;
        }

        public async Task AddAsync(UserAccount account, CancellationToken cancellationToken)
        {
            await _db.Insertable(account).ExecuteCommandAsync(cancellationToken);
        }

        public Task AddRangeAsync(IReadOnlyList<UserAccount> accounts, CancellationToken cancellationToken)
            => _db.Insertable(accounts.ToList()).ExecuteCommandAsync(cancellationToken);

        public async Task UpdateAsync(UserAccount account, CancellationToken cancellationToken)
        {
            // SqlSugar 在 SQLite InitTables 时未识别 Id 主键 → 显式 Where 修复 "no primary key and no conditions"
            var id = account.Id;
            await _db.Updateable(account).Where(x => x.Id == id).ExecuteCommandAsync(cancellationToken);
        }

        public async Task UpdateRangeAsync(IReadOnlyList<UserAccount> accounts, CancellationToken cancellationToken)
        {
            foreach (var account in accounts)
            {
                await UpdateAsync(account, cancellationToken);
            }
        }

        public Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
            => _db.Deleteable<UserAccount>().Where(x => x.Id == id).ExecuteCommandAsync(cancellationToken);
    }

    /// <summary>测试用：BCrypt 风格的可逆"哈希"，仅用于断言密码已写入。</summary>
    private sealed class IdentityPasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => "hashed::" + password;
        public bool VerifyHashedPassword(string hashedPassword, string providedPassword) => hashedPassword == "hashed::" + providedPassword;
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
        where T : class
    {
        public StaticOptionsMonitor(T value) { CurrentValue = value; }
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<T, string?> listener) => new EmptyDisposable();
        private sealed class EmptyDisposable : IDisposable { public void Dispose() { } }
    }
}
