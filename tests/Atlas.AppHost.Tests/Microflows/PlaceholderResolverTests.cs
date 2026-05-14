using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Actions.Database;
using Atlas.Application.Microflows.Runtime.Expressions;
using Atlas.Application.Microflows.Runtime.Security;

namespace Atlas.AppHost.Tests.Microflows;

/// <summary>
/// PlaceholderResolver 单元测试：验证 SQL 占位符的解析与参数化替换。
/// </summary>
public sealed class PlaceholderResolverTests
{
    private static MicroflowActionExecutionContext BuildContext(Dictionary<string, object?>? variables = null)
    {
        var plan = new MicroflowExecutionPlan { Id = "test-mf", SchemaId = "test-mf" };
        var security = new MicroflowRuntimeSecurityContext
        {
            UserId = "user-001",
            UserName = "TestUser",
            TenantId = Guid.Empty.ToString(),
            WorkspaceId = "ws-001",
            Roles = new List<string> { "admin" }
        };
        var runtime = RuntimeExecutionContext.Create(
            "run-placeholder-test",
            plan,
            MicroflowRuntimeExecutionMode.TestRun,
            input: null,
            securityContext: null,
            startedAt: DateTimeOffset.UtcNow);

        if (variables != null)
        {
            foreach (var (key, val) in variables)
            {
                runtime.VariableStore.Define(new MicroflowVariableDefinition
                {
                    Name = key,
                    RawValueJson = JsonSerializer.Serialize(val),
                    ScopeKind = MicroflowVariableScopeKind.Global
                });
            }
        }

        return new MicroflowActionExecutionContext
        {
            RuntimeExecutionContext = runtime,
            ExecutionPlan = plan,
            ExecutionNode = new MicroflowExecutionNode { ObjectId = "obj-1", ActionId = "act-1", ActionKind = "queryExternalDatabase" },
            ActionKind = "queryExternalDatabase",
            ObjectId = "obj-1",
            ActionId = "act-1",
            ActionConfig = JsonSerializer.SerializeToElement(new { }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            VariableStore = runtime.VariableStore,
            RuntimeSecurityContext = security,
            ExpressionEvaluator = new MicroflowExpressionEvaluator(),
            ConnectorRegistry = new MicroflowRuntimeConnectorRegistry()
        };
    }

    [Fact]
    public void Resolve_PlainSql_NoPlaceholders_ReturnsUnchanged()
    {
        var context = BuildContext();

        var (sql, parameters) = PlaceholderResolver.Resolve(
            "SELECT * FROM orders WHERE id = 1",
            "postgresql",
            context);

        Assert.Equal("SELECT * FROM orders WHERE id = 1", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Resolve_LocalVariable_ReplacesWithParameterizedPlaceholder()
    {
        var context = BuildContext(new Dictionary<string, object?> { ["customerId"] = 42 });

        var (sql, parameters) = PlaceholderResolver.Resolve(
            "SELECT * FROM orders WHERE customer_id = $.customerId",
            "postgresql",
            context);

        Assert.Equal("SELECT * FROM orders WHERE customer_id = @p0", sql);
        Assert.Single(parameters);
        Assert.Equal("@p0", parameters[0].Name);
    }

    [Fact]
    public void Resolve_GlobalVariable_ExtractsCorrectly()
    {
        var context = BuildContext(new Dictionary<string, object?> { ["filterStatus"] = "active" });

        var (sql, parameters) = PlaceholderResolver.Resolve(
            "SELECT * FROM users WHERE status = $global.filterStatus",
            "postgresql",
            context);

        Assert.Equal("SELECT * FROM users WHERE status = @p0", sql);
        Assert.Single(parameters);
    }

    [Fact]
    public void Resolve_CurrentUserField_InjectsUserId()
    {
        var context = BuildContext();

        var (sql, parameters) = PlaceholderResolver.Resolve(
            "SELECT * FROM audit WHERE created_by = $currentUser.id",
            "postgresql",
            context);

        Assert.Equal("SELECT * FROM audit WHERE created_by = @p0", sql);
        Assert.Single(parameters);
        Assert.Equal("user-001", (string?)parameters[0].Value);
    }

    [Fact]
    public void Resolve_MultiplePlaceholders_AssignsSequentialParameterNames()
    {
        var context = BuildContext(new Dictionary<string, object?>
        {
            ["from"] = "2024-01-01",
            ["to"] = "2024-12-31",
        });

        var (sql, parameters) = PlaceholderResolver.Resolve(
            "SELECT * FROM sales WHERE date >= $.from AND date <= $.to",
            "postgresql",
            context);

        Assert.Equal("SELECT * FROM sales WHERE date >= @p0 AND date <= @p1", sql);
        Assert.Equal(2, parameters.Count);
        Assert.Equal("@p0", parameters[0].Name);
        Assert.Equal("@p1", parameters[1].Name);
    }

    [Fact]
    public void Resolve_MySqlDriver_UsesMysqlStyleQuestionMark()
    {
        var context = BuildContext(new Dictionary<string, object?> { ["id"] = 1 });

        var (sql, parameters) = PlaceholderResolver.Resolve(
            "SELECT * FROM items WHERE id = $.id",
            "mysql",
            context);

        Assert.Equal("SELECT * FROM items WHERE id = ?", sql);
        Assert.Single(parameters);
    }

    [Fact]
    public void Resolve_EmptySql_ReturnsEmptyResult()
    {
        var context = BuildContext();

        var (sql, parameters) = PlaceholderResolver.Resolve("", "postgresql", context);

        Assert.Equal("", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Resolve_MissingVariable_InjectsNullParameter()
    {
        var context = BuildContext(); // no variables

        var (sql, parameters) = PlaceholderResolver.Resolve(
            "SELECT * FROM t WHERE x = $.nonexistent",
            "postgresql",
            context);

        Assert.Equal("SELECT * FROM t WHERE x = @p0", sql);
        Assert.Single(parameters);
        Assert.Null(parameters[0].Value);
    }
}
