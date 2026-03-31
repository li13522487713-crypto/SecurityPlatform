using System.Text;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class TeamAgentSchemaDraftComposer : ITeamAgentSchemaDraftComposer
{
    private static readonly IReadOnlyList<EntityDefinition> KnownEntities =
    [
        new("customer", "客户", "客户主数据"),
        new("contract", "合同", "合同主表"),
        new("payment", "回款", "合同回款记录"),
        new("invoice", "发票", "合同发票记录"),
        new("order", "订单", "订单主表"),
        new("product", "商品", "商品主数据"),
        new("ticket", "工单", "工单记录"),
        new("employee", "员工", "员工档案"),
        new("department", "部门", "组织部门"),
        new("project", "项目", "项目主表")
    ];

    private static readonly IReadOnlyDictionary<string, string[]> EntityKeywords = new Dictionary<string, string[]>
    {
        ["customer"] = ["客户", "客户档案", "客户信息"],
        ["contract"] = ["合同", "合约"],
        ["payment"] = ["回款", "收款", "付款记录"],
        ["invoice"] = ["发票", "开票"],
        ["order"] = ["订单", "销售单"],
        ["product"] = ["商品", "产品"],
        ["ticket"] = ["工单", "服务单"],
        ["employee"] = ["员工", "人员"],
        ["department"] = ["部门", "组织"],
        ["project"] = ["项目"]
    };

    public SchemaDraftDto Compose(
        TeamAgent teamAgent,
        string requirement,
        IReadOnlyList<TeamAgentMemberContribution> contributions,
        string? appId)
    {
        _ = appId;
        var entities = ResolveEntities(teamAgent, requirement, contributions);
        var fields = new List<SchemaDraftFieldDto>();
        var indexes = new List<SchemaDraftIndexDto>();
        var relations = new List<SchemaDraftRelationDto>();
        var securityPolicies = new List<SchemaDraftSecurityPolicyDto>();

        foreach (var entity in entities)
        {
            fields.AddRange(BuildEntityFields(entity));
            indexes.AddRange(BuildIndexes(entity));
            securityPolicies.AddRange(BuildSecurityPolicies(entity));
        }

        relations.AddRange(BuildRelations(entities));

        var openQuestions = BuildOpenQuestions(requirement, entities, contributions);
        return new SchemaDraftDto(
            BuildDraftSummary(requirement, entities),
            entities.Select(entity => new SchemaDraftEntityDto(entity.TableKey, entity.DisplayName, entity.Description)).ToList(),
            fields,
            relations,
            indexes,
            securityPolicies,
            openQuestions,
            TeamAgentSchemaDraftConfirmationState.Pending.ToString().ToLowerInvariant());
    }

    private static List<EntityBlueprint> ResolveEntities(
        TeamAgent teamAgent,
        string requirement,
        IReadOnlyList<TeamAgentMemberContribution> contributions)
    {
        var sourceText = new StringBuilder(requirement.Trim());
        foreach (var contribution in contributions)
        {
            if (!string.IsNullOrWhiteSpace(contribution.OutputMessage))
            {
                sourceText.Append('\n').Append(contribution.OutputMessage);
            }
        }

        var content = sourceText.ToString();
        var resolved = new List<EntityBlueprint>();
        foreach (var definition in KnownEntities)
        {
            if (!EntityKeywords.TryGetValue(definition.Key, out var keywords))
            {
                continue;
            }

            if (keywords.Any(keyword => content.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                resolved.Add(new EntityBlueprint(
                    $"{NormalizeKey(definition.Key)}_{teamAgent.Id % 1000}",
                    $"{definition.DisplayName}表",
                    definition.Description,
                    definition.Key));
            }
        }

        if (resolved.Count > 0)
        {
            return resolved;
        }

        var normalizedKey = NormalizeKey(requirement);
        var fallbackKey = string.IsNullOrWhiteSpace(normalizedKey) ? $"team_{teamAgent.Id}_entity" : normalizedKey;
        return
        [
            new EntityBlueprint(fallbackKey, $"{ExtractDisplayName(requirement)}表", requirement.Trim(), "generic")
        ];
    }

    private static IReadOnlyList<SchemaDraftFieldDto> BuildEntityFields(EntityBlueprint entity)
    {
        var fields = new List<SchemaDraftFieldDto>
        {
            new(entity.TableKey, "Id", "主键", "Long", false, true, true, true, 1),
            new(entity.TableKey, "Status", "状态", "String", false, false, false, false, 90, Length: 50),
            new(entity.TableKey, "TenantId", "租户", "Long", false, false, false, false, 91),
            new(entity.TableKey, "CreatedAt", "创建时间", "DateTime", false, false, false, false, 92),
            new(entity.TableKey, "UpdatedAt", "更新时间", "DateTime", true, false, false, false, 93),
            new(entity.TableKey, "OwnerUserId", "负责人", "Long", true, false, false, false, 94)
        };

        foreach (var field in BuildDomainFields(entity))
        {
            fields.Add(field);
        }

        return fields.OrderBy(field => field.SortOrder).ToList();
    }

    private static IEnumerable<SchemaDraftFieldDto> BuildDomainFields(EntityBlueprint entity)
    {
        return entity.DomainKey switch
        {
            "customer" =>
            [
                new(entity.TableKey, "CustomerCode", "客户编码", "String", false, false, false, true, 10, Length: 64),
                new(entity.TableKey, "CustomerName", "客户名称", "String", false, false, false, false, 11, Length: 200),
                new(entity.TableKey, "ContactName", "联系人", "String", true, false, false, false, 12, Length: 100),
                new(entity.TableKey, "Phone", "联系电话", "String", true, false, false, false, 13, Length: 32)
            ],
            "contract" =>
            [
                new(entity.TableKey, "ContractNo", "合同编号", "String", false, false, false, true, 10, Length: 64),
                new(entity.TableKey, "CustomerId", "客户", "Long", false, false, false, false, 11),
                new(entity.TableKey, "Amount", "合同金额", "Decimal", false, false, false, false, 12, Precision: 18, Scale: 2),
                new(entity.TableKey, "SignedAt", "签署时间", "DateTime", true, false, false, false, 13)
            ],
            "payment" =>
            [
                new(entity.TableKey, "ContractId", "合同", "Long", false, false, false, false, 10),
                new(entity.TableKey, "ReceivedAmount", "回款金额", "Decimal", false, false, false, false, 11, Precision: 18, Scale: 2),
                new(entity.TableKey, "ReceivedAt", "回款时间", "DateTime", true, false, false, false, 12),
                new(entity.TableKey, "PaymentMethod", "回款方式", "String", true, false, false, false, 13, Length: 50)
            ],
            "invoice" =>
            [
                new(entity.TableKey, "ContractId", "合同", "Long", false, false, false, false, 10),
                new(entity.TableKey, "InvoiceNo", "发票编号", "String", false, false, false, true, 11, Length: 64),
                new(entity.TableKey, "InvoiceAmount", "发票金额", "Decimal", false, false, false, false, 12, Precision: 18, Scale: 2),
                new(entity.TableKey, "IssuedAt", "开票时间", "DateTime", true, false, false, false, 13)
            ],
            "order" =>
            [
                new(entity.TableKey, "OrderNo", "订单编号", "String", false, false, false, true, 10, Length: 64),
                new(entity.TableKey, "CustomerId", "客户", "Long", false, false, false, false, 11),
                new(entity.TableKey, "Amount", "订单金额", "Decimal", false, false, false, false, 12, Precision: 18, Scale: 2),
                new(entity.TableKey, "OrderedAt", "下单时间", "DateTime", true, false, false, false, 13)
            ],
            "product" =>
            [
                new(entity.TableKey, "ProductCode", "商品编码", "String", false, false, false, true, 10, Length: 64),
                new(entity.TableKey, "ProductName", "商品名称", "String", false, false, false, false, 11, Length: 200),
                new(entity.TableKey, "Category", "分类", "String", true, false, false, false, 12, Length: 100),
                new(entity.TableKey, "UnitPrice", "单价", "Decimal", false, false, false, false, 13, Precision: 18, Scale: 2)
            ],
            "ticket" =>
            [
                new(entity.TableKey, "TicketNo", "工单编号", "String", false, false, false, true, 10, Length: 64),
                new(entity.TableKey, "Title", "标题", "String", false, false, false, false, 11, Length: 200),
                new(entity.TableKey, "Priority", "优先级", "String", false, false, false, false, 12, Length: 32),
                new(entity.TableKey, "OpenedAt", "创建时间", "DateTime", false, false, false, false, 13)
            ],
            "employee" =>
            [
                new(entity.TableKey, "EmployeeNo", "员工编号", "String", false, false, false, true, 10, Length: 64),
                new(entity.TableKey, "EmployeeName", "员工姓名", "String", false, false, false, false, 11, Length: 100),
                new(entity.TableKey, "DepartmentId", "所属部门", "Long", true, false, false, false, 12),
                new(entity.TableKey, "Phone", "手机号", "String", true, false, false, false, 13, Length: 32)
            ],
            "department" =>
            [
                new(entity.TableKey, "DepartmentCode", "部门编码", "String", false, false, false, true, 10, Length: 64),
                new(entity.TableKey, "DepartmentName", "部门名称", "String", false, false, false, false, 11, Length: 100),
                new(entity.TableKey, "LeaderId", "负责人", "Long", true, false, false, false, 12)
            ],
            "project" =>
            [
                new(entity.TableKey, "ProjectCode", "项目编码", "String", false, false, false, true, 10, Length: 64),
                new(entity.TableKey, "ProjectName", "项目名称", "String", false, false, false, false, 11, Length: 200),
                new(entity.TableKey, "OwnerId", "项目负责人", "Long", true, false, false, false, 12),
                new(entity.TableKey, "StartDate", "开始日期", "DateTime", true, false, false, false, 13)
            ],
            _ =>
            [
                new(entity.TableKey, "Name", "名称", "String", false, false, false, false, 10, Length: 200),
                new(entity.TableKey, "Code", "编码", "String", true, false, false, true, 11, Length: 64),
                new(entity.TableKey, "Remark", "备注", "String", true, false, false, false, 12, Length: 500)
            ]
        };
    }

    private static IReadOnlyList<SchemaDraftIndexDto> BuildIndexes(EntityBlueprint entity)
    {
        var indexes = new List<SchemaDraftIndexDto>
        {
            new(entity.TableKey, $"IX_{entity.TableKey}_Status", false, ["Status"]),
            new(entity.TableKey, $"IX_{entity.TableKey}_TenantId", false, ["TenantId"])
        };

        var codeField = entity.DomainKey switch
        {
            "customer" => "CustomerCode",
            "contract" => "ContractNo",
            "invoice" => "InvoiceNo",
            "order" => "OrderNo",
            "product" => "ProductCode",
            "ticket" => "TicketNo",
            "employee" => "EmployeeNo",
            "department" => "DepartmentCode",
            "project" => "ProjectCode",
            _ => "Code"
        };
        indexes.Add(new SchemaDraftIndexDto(entity.TableKey, $"UX_{entity.TableKey}_{codeField}", true, [codeField]));
        return indexes;
    }

    private static IReadOnlyList<SchemaDraftSecurityPolicyDto> BuildSecurityPolicies(EntityBlueprint entity)
    {
        return
        [
            new(entity.TableKey, "TenantId", "tenant_admin", true, false),
            new(entity.TableKey, "OwnerUserId", "app_user", true, false),
            new(entity.TableKey, "Status", "app_user", true, true)
        ];
    }

    private static IReadOnlyList<SchemaDraftRelationDto> BuildRelations(IReadOnlyList<EntityBlueprint> entities)
    {
        var relationList = new List<SchemaDraftRelationDto>();
        var customer = entities.FirstOrDefault(entity => entity.DomainKey == "customer");
        var contract = entities.FirstOrDefault(entity => entity.DomainKey == "contract");
        var payment = entities.FirstOrDefault(entity => entity.DomainKey == "payment");
        var invoice = entities.FirstOrDefault(entity => entity.DomainKey == "invoice");
        var department = entities.FirstOrDefault(entity => entity.DomainKey == "department");
        var employee = entities.FirstOrDefault(entity => entity.DomainKey == "employee");

        if (customer is not null && contract is not null)
        {
            relationList.Add(new SchemaDraftRelationDto(contract.TableKey, "CustomerId", customer.TableKey, "Id", "ManyToOne", "Restrict"));
        }

        if (contract is not null && payment is not null)
        {
            relationList.Add(new SchemaDraftRelationDto(payment.TableKey, "ContractId", contract.TableKey, "Id", "ManyToOne", "Cascade"));
        }

        if (contract is not null && invoice is not null)
        {
            relationList.Add(new SchemaDraftRelationDto(invoice.TableKey, "ContractId", contract.TableKey, "Id", "ManyToOne", "Cascade"));
        }

        if (department is not null && employee is not null)
        {
            relationList.Add(new SchemaDraftRelationDto(employee.TableKey, "DepartmentId", department.TableKey, "Id", "ManyToOne", "Restrict"));
        }

        return relationList;
    }

    private static IReadOnlyList<SchemaDraftOpenQuestionDto> BuildOpenQuestions(
        string requirement,
        IReadOnlyList<EntityBlueprint> entities,
        IReadOnlyList<TeamAgentMemberContribution> contributions)
    {
        var questions = new List<SchemaDraftOpenQuestionDto>();
        if (!requirement.Contains("权限", StringComparison.OrdinalIgnoreCase) &&
            contributions.All(item => !item.OutputMessage.Contains("权限", StringComparison.OrdinalIgnoreCase)))
        {
            questions.Add(new SchemaDraftOpenQuestionDto("permission_scope", "是否需要按租户、部门、负责人三层做数据权限隔离？"));
        }

        if (entities.Any(entity => entity.DomainKey is "contract" or "order") &&
            !requirement.Contains("审批", StringComparison.OrdinalIgnoreCase))
        {
            questions.Add(new SchemaDraftOpenQuestionDto("approval_flow", "合同或订单是否需要审批流状态字段与审批历史？"));
        }

        if (entities.Count == 1 && entities[0].DomainKey == "generic")
        {
            questions.Add(new SchemaDraftOpenQuestionDto("entity_split", "当前需求未明确核心实体，是否需要拆分为主表与明细表？"));
        }

        return questions;
    }

    private static string BuildDraftSummary(string requirement, IReadOnlyList<EntityBlueprint> entities)
    {
        var displayNames = string.Join("、", entities.Select(entity => entity.DisplayName));
        return $"根据需求“{requirement.Trim()}”生成的草案，建议创建 {displayNames}。";
    }

    private static string NormalizeKey(string text)
    {
        var letters = new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(letters))
        {
            return string.Empty;
        }

        if (!char.IsLetter(letters[0]))
        {
            letters = $"t{letters}";
        }

        return letters.Length > 32 ? letters[..32] : letters;
    }

    private static string ExtractDisplayName(string requirement)
    {
        var trimmed = requirement.Trim();
        return trimmed.Length <= 16 ? trimmed : trimmed[..16];
    }

    private sealed record EntityDefinition(
        string Key,
        string DisplayName,
        string Description);

    private sealed record EntityBlueprint(
        string TableKey,
        string DisplayName,
        string? Description,
        string DomainKey);
}
