using System.Text.Json;
using System.Text.Json.Nodes;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Services;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowValidationCompatibilityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ValidateAsync_Allows_LoopBody_Entry_And_TopLevel_Boolean_CaseValues()
    {
        var schema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["schemaVersion"] = "flowgram.microflow.v1",
            ["id"] = "mf-loop-body-compat",
            ["name"] = "mf-loop-body-compat",
            ["displayName"] = "mf-loop-body-compat",
            ["moduleId"] = "Sales",
            ["parameters"] = new JsonArray(),
            ["returnType"] = new JsonObject { ["kind"] = "void" },
            ["workflow"] = new JsonObject
            {
                ["nodes"] = new JsonArray
                {
                    Node("start", "startEvent", "root-collection"),
                    Node("loop", "loopedActivity", "root-collection", new JsonObject
                    {
                        ["bodyCollectionId"] = "loop-body",
                        ["loopSource"] = new JsonObject { ["kind"] = "whileCondition", ["expression"] = "false" }
                    }),
                    Node("decision", "exclusiveSplit", "loop-body", new JsonObject
                    {
                        ["splitCondition"] = new JsonObject { ["expression"] = "true", ["resultType"] = "boolean" }
                    }),
                    Node("break", "breakEvent", "loop-body"),
                    Node("continue", "continueEvent", "loop-body"),
                    Node("end", "endEvent", "root-collection")
                },
                ["edges"] = new JsonArray
                {
                    Edge("f-start-loop", "start", "loop"),
                    Edge("f-loop-end", "loop", "end"),
                    Edge("f-loop-body", "loop", "decision", "loopBody", "loop-body"),
                    Edge("f-true", "decision", "break", "decisionCondition", "loop-body", topLevelCaseValues: true, value: true),
                    Edge("f-false", "decision", "continue", "decisionCondition", "loop-body", topLevelCaseValues: true, value: false)
                }
            },
            ["editor"] = new JsonObject(),
            ["audit"] = new JsonObject()
        }, JsonOptions);
        var service = CreateValidationService();

        var result = await service.ValidateAsync(
            "mf-loop-body-compat",
            new ValidateMicroflowRequestDto { Schema = schema, Mode = "testRun", IncludeWarnings = true },
            CancellationToken.None);

        Assert.DoesNotContain(result.Issues, issue => issue.Code == MicroflowValidationCodes.FlowInvalidTarget);
        Assert.DoesNotContain(result.Issues, issue => issue.Code == MicroflowValidationCodes.DecisionBooleanTrueMissing);
        Assert.DoesNotContain(result.Issues, issue => issue.Code == MicroflowValidationCodes.DecisionBooleanFalseMissing);
    }

    [Fact]
    public async Task ValidateAsync_BooleanDecisionMissingBranch_Returns_Node_FieldPath_And_RelatedFlows()
    {
        var schema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["schemaVersion"] = "flowgram.microflow.v1",
            ["id"] = "mf-decision-diagnostics",
            ["name"] = "mf-decision-diagnostics",
            ["displayName"] = "mf-decision-diagnostics",
            ["moduleId"] = "Sales",
            ["parameters"] = new JsonArray(),
            ["returnType"] = new JsonObject { ["kind"] = "void" },
            ["workflow"] = new JsonObject
            {
                ["nodes"] = new JsonArray
                {
                    Node("start", "startEvent", "root-collection"),
                    Node("decision", "exclusiveSplit", "root-collection", new JsonObject
                    {
                        ["splitCondition"] = new JsonObject { ["expression"] = "true", ["resultType"] = "boolean" }
                    }),
                    Node("end", "endEvent", "root-collection")
                },
                ["edges"] = new JsonArray
                {
                    Edge("f-start-decision", "start", "decision"),
                    Edge("f-true", "decision", "end", "decisionCondition", topLevelCaseValues: true, value: true)
                }
            },
            ["editor"] = new JsonObject(),
            ["audit"] = new JsonObject()
        }, JsonOptions);
        var service = CreateValidationService();

        var result = await service.ValidateAsync(
            "mf-decision-diagnostics",
            new ValidateMicroflowRequestDto { Schema = schema, Mode = "testRun", IncludeWarnings = true },
            CancellationToken.None);

        var issue = Assert.Single(result.Issues, issue => issue.Code == MicroflowValidationCodes.DecisionBooleanFalseMissing);
        Assert.Equal("decision", issue.ObjectId);
        Assert.Null(issue.FlowId);
        Assert.Contains("splitCondition", issue.FieldPath);
        Assert.Contains("f-true", issue.RelatedFlowIds);
        var quickFix = Assert.Single(issue.QuickFixes);
        Assert.Equal("Create false branch", quickFix.Title);
        Assert.Contains("\"createMissingFlow\"", quickFix.Patch);
        Assert.Contains("\"value\":false", quickFix.Patch);
    }

    [Fact]
    public async Task ValidateAsync_CrossCollectionFlow_Returns_Origin_FieldPath_And_RelatedFlow()
    {
        var schema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["schemaVersion"] = "flowgram.microflow.v1",
            ["id"] = "mf-cross-collection-diagnostics",
            ["name"] = "mf-cross-collection-diagnostics",
            ["displayName"] = "mf-cross-collection-diagnostics",
            ["moduleId"] = "Sales",
            ["parameters"] = new JsonArray(),
            ["returnType"] = new JsonObject { ["kind"] = "void" },
            ["workflow"] = new JsonObject
            {
                ["nodes"] = new JsonArray
                {
                    Node("start", "startEvent", "root-collection"),
                    Node("loop", "loopedActivity", "root-collection", new JsonObject
                    {
                        ["bodyCollectionId"] = "loop-body",
                        ["loopSource"] = new JsonObject { ["kind"] = "whileCondition", ["expression"] = "false" }
                    }),
                    Node("continue", "continueEvent", "loop-body")
                },
                ["edges"] = new JsonArray
                {
                    Edge("f-cross", "start", "continue", collectionId: "root-collection")
                }
            },
            ["editor"] = new JsonObject(),
            ["audit"] = new JsonObject()
        }, JsonOptions);
        var service = CreateValidationService();

        var result = await service.ValidateAsync(
            "mf-cross-collection-diagnostics",
            new ValidateMicroflowRequestDto { Schema = schema, Mode = "testRun", IncludeWarnings = true },
            CancellationToken.None);

        var issue = Assert.Single(result.Issues, issue => issue.Code == MicroflowValidationCodes.FlowInvalidTarget);
        Assert.Equal("start", issue.ObjectId);
        Assert.Equal("f-cross", issue.FlowId);
        Assert.Equal("root-collection", issue.CollectionId);
        Assert.Contains("f-cross", issue.RelatedFlowIds);
        Assert.Contains("start", issue.RelatedObjectIds);
        Assert.Contains("continue", issue.RelatedObjectIds);
        Assert.NotNull(issue.FieldPath);
    }

    [Fact]
    public async Task ValidateAsync_LoopBody_Return_Flow_Is_Rejected_As_CrossCollection()
    {
        var schema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["schemaVersion"] = "flowgram.microflow.v1",
            ["id"] = "mf-loop-return-diagnostics",
            ["name"] = "mf-loop-return-diagnostics",
            ["displayName"] = "mf-loop-return-diagnostics",
            ["moduleId"] = "Sales",
            ["parameters"] = new JsonArray(),
            ["returnType"] = new JsonObject { ["kind"] = "void" },
            ["workflow"] = new JsonObject
            {
                ["nodes"] = new JsonArray
                {
                    Node("loop", "loopedActivity", "root-collection", new JsonObject
                    {
                        ["bodyCollectionId"] = "loop-body",
                        ["loopSource"] = new JsonObject { ["kind"] = "whileCondition", ["expression"] = "false" }
                    }),
                    Node("continue", "continueEvent", "loop-body")
                },
                ["edges"] = new JsonArray
                {
                    Edge("f-return", "continue", "loop", "loopBody", "loop-body")
                }
            },
            ["editor"] = new JsonObject(),
            ["audit"] = new JsonObject()
        }, JsonOptions);
        var service = CreateValidationService();

        var result = await service.ValidateAsync(
            "mf-loop-return-diagnostics",
            new ValidateMicroflowRequestDto { Schema = schema, Mode = "testRun", IncludeWarnings = true },
            CancellationToken.None);

        var issue = Assert.Single(result.Issues, issue => issue.Code == MicroflowValidationCodes.FlowInvalidTarget);
        Assert.Equal("continue", issue.ObjectId);
        Assert.Equal("f-return", issue.FlowId);
        Assert.Contains("loop", issue.RelatedObjectIds);
    }

    [Fact]
    public async Task ValidateAsync_DuplicateObjectId_Returns_ObjectId_And_FieldPath()
    {
        var schema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["schemaVersion"] = "flowgram.microflow.v1",
            ["id"] = "mf-duplicate-object-diagnostics",
            ["name"] = "mf-duplicate-object-diagnostics",
            ["displayName"] = "mf-duplicate-object-diagnostics",
            ["moduleId"] = "Sales",
            ["parameters"] = new JsonArray(),
            ["returnType"] = new JsonObject { ["kind"] = "void" },
            ["workflow"] = new JsonObject
            {
                ["nodes"] = new JsonArray
                {
                    Node("duplicate-node", "startEvent", "root-collection"),
                    Node("duplicate-node", "endEvent", "root-collection")
                },
                ["edges"] = new JsonArray()
            },
            ["editor"] = new JsonObject(),
            ["audit"] = new JsonObject()
        }, JsonOptions);
        var service = CreateValidationService();

        var result = await service.ValidateAsync(
            "mf-duplicate-object-diagnostics",
            new ValidateMicroflowRequestDto { Schema = schema, Mode = "testRun", IncludeWarnings = true },
            CancellationToken.None);

        var issue = Assert.Single(result.Issues, issue => issue.Code == MicroflowValidationCodes.ObjectIdDuplicated);
        Assert.Equal("duplicate-node", issue.ObjectId);
        Assert.Null(issue.FlowId);
        Assert.Contains("workflow.nodes", issue.FieldPath);
    }

    [Fact]
    public async Task ValidateAsync_DuplicateFlowId_Returns_FlowId_And_FieldPath()
    {
        var schema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["schemaVersion"] = "flowgram.microflow.v1",
            ["id"] = "mf-duplicate-flow-diagnostics",
            ["name"] = "mf-duplicate-flow-diagnostics",
            ["displayName"] = "mf-duplicate-flow-diagnostics",
            ["moduleId"] = "Sales",
            ["parameters"] = new JsonArray(),
            ["returnType"] = new JsonObject { ["kind"] = "void" },
            ["workflow"] = new JsonObject
            {
                ["nodes"] = new JsonArray
                {
                    Node("start", "startEvent", "root-collection"),
                    Node("end-a", "endEvent", "root-collection"),
                    Node("end-b", "endEvent", "root-collection")
                },
                ["edges"] = new JsonArray
                {
                    Edge("duplicate-flow", "start", "end-a"),
                    Edge("duplicate-flow", "start", "end-b")
                }
            },
            ["editor"] = new JsonObject(),
            ["audit"] = new JsonObject()
        }, JsonOptions);
        var service = CreateValidationService();

        var result = await service.ValidateAsync(
            "mf-duplicate-flow-diagnostics",
            new ValidateMicroflowRequestDto { Schema = schema, Mode = "testRun", IncludeWarnings = true },
            CancellationToken.None);

        var issue = Assert.Single(result.Issues, issue => issue.Code == MicroflowValidationCodes.FlowDuplicated);
        Assert.Equal("duplicate-flow", issue.FlowId);
        Assert.Null(issue.ObjectId);
        Assert.Contains("workflow.edges", issue.FieldPath);
    }

    [Fact]
    public async Task ValidateAsync_Allows_List_ItemScope_Expressions_In_Backend_Validator()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new
                {
                    id = "seed",
                    kind = "actionActivity",
                    caption = "Seed",
                    action = new
                    {
                        id = "seed-action",
                        kind = "createList",
                        outputVariableName = "numbers",
                        dataType = new { kind = "list", itemType = new { kind = "integer" } },
                        initialItemsExpression = "[1, 2, 3]"
                    }
                },
                new
                {
                    id = "filter",
                    kind = "actionActivity",
                    caption = "Filter",
                    action = new
                    {
                        id = "filter-action",
                        kind = "filterList",
                        sourceListVariableName = "numbers",
                        outputVariableName = "filtered",
                        itemVariableName = "item",
                        dataType = new { kind = "list", itemType = new { kind = "integer" } },
                        conditionExpression = "$item > 1"
                    }
                },
                new
                {
                    id = "sort",
                    kind = "actionActivity",
                    caption = "Sort",
                    action = new
                    {
                        id = "sort-action",
                        kind = "sortList",
                        sourceListVariableName = "filtered",
                        outputVariableName = "sorted",
                        itemVariableName = "item",
                        dataType = new { kind = "list", itemType = new { kind = "integer" } },
                        sortExpression = "$item"
                    }
                },
                new
                {
                    id = "map",
                    kind = "actionActivity",
                    caption = "Map",
                    action = new
                    {
                        id = "map-action",
                        kind = "listOperation",
                        operation = "map",
                        leftListVariableName = "sorted",
                        outputVariableName = "mapped",
                        itemVariableName = "item",
                        dataType = new { kind = "list", itemType = new { kind = "integer" } },
                        expression = "$item"
                    }
                },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Edge("f1", "start", "seed"),
                Edge("f2", "seed", "filter"),
                Edge("f3", "filter", "sort"),
                Edge("f4", "sort", "map"),
                Edge("f5", "map", "end")
            ],
            parameters: Array.Empty<object>(),
            id: "mf-backend-item-scope-validation",
            options: JsonOptions);
        var service = CreateValidationService(MicroflowSeedMetadataCatalog.Create());

        var result = await service.ValidateAsync(
            "mf-backend-item-scope-validation",
            new ValidateMicroflowRequestDto { Schema = schema, Mode = "testRun", IncludeWarnings = true },
            CancellationToken.None);

        Assert.DoesNotContain(result.Issues, issue =>
            issue.Code == MicroflowValidationCodes.ExprUnknownVariable
            && issue.ObjectId is "filter" or "sort" or "map");
    }

    [Fact]
    public async Task ValidateAsync_LatestHttpResponse_And_SoapFault_Use_Metadata_For_Member_Checks()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new
                {
                    id = "seed-message",
                    kind = "actionActivity",
                    caption = "Seed Message",
                    action = new
                    {
                        id = "seed-message-action",
                        kind = "createVariable",
                        variableName = "message",
                        dataType = new { kind = "string" },
                        initialValue = "\"seed\""
                    }
                },
                new
                {
                    id = "change-message",
                    kind = "actionActivity",
                    caption = "Change Message",
                    action = new
                    {
                        id = "change-message-action",
                        kind = "changeVariable",
                        targetVariableName = "message",
                        newValueExpression = "$latestHttpResponse/missingStatus + $latestSoapFault/missingFault"
                    }
                },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Edge("f1", "start", "seed-message"),
                Edge("f2", "seed-message", "change-message"),
                Edge("f3", "change-message", "end")
            ],
            parameters: Array.Empty<object>(),
            id: "mf-latest-error-context-metadata-validation",
            options: JsonOptions);
        var service = CreateValidationService(MicroflowSeedMetadataCatalog.Create());

        var result = await service.ValidateAsync(
            "mf-latest-error-context-metadata-validation",
            new ValidateMicroflowRequestDto { Schema = schema, Mode = "testRun", IncludeWarnings = true },
            CancellationToken.None);

        var memberIssues = result.Issues.Where(issue => issue.Code == MicroflowValidationCodes.ExprMemberNotFound).ToArray();
        Assert.Equal(2, memberIssues.Length);
        Assert.All(memberIssues, issue =>
            Assert.Equal("workflow.nodes.2.action.newValueExpression", issue.FieldPath));
    }

    [Fact]
    public async Task ValidateAsync_Allows_SortKey_Expression_ItemScope_In_Backend_Validator()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new
                {
                    id = "seed",
                    kind = "actionActivity",
                    caption = "Seed",
                    action = new
                    {
                        id = "seed-action",
                        kind = "createList",
                        outputVariableName = "numbers",
                        dataType = new { kind = "list", itemType = new { kind = "integer" } },
                        initialItemsExpression = "[3, 1, 2]"
                    }
                },
                new
                {
                    id = "sort-op",
                    kind = "actionActivity",
                    caption = "Sort Operation",
                    action = new
                    {
                        id = "sort-op-action",
                        kind = "listOperation",
                        operation = "sort",
                        leftListVariableName = "numbers",
                        outputVariableName = "sorted",
                        itemVariableName = "item",
                        dataType = new { kind = "list", itemType = new { kind = "integer" } },
                        sortKeys = new object[]
                        {
                            new { expression = "$item", direction = "asc" }
                        }
                    }
                },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Edge("f1", "start", "seed"),
                Edge("f2", "seed", "sort-op"),
                Edge("f3", "sort-op", "end")
            ],
            parameters: Array.Empty<object>(),
            id: "mf-backend-sort-key-validation",
            options: JsonOptions);
        var service = CreateValidationService();

        var result = await service.ValidateAsync(
            "mf-backend-sort-key-validation",
            new ValidateMicroflowRequestDto { Schema = schema, Mode = "testRun", IncludeWarnings = true },
            CancellationToken.None);

        Assert.DoesNotContain(result.Issues, issue =>
            issue.Code == MicroflowValidationCodes.ExprUnknownVariable
            && issue.ObjectId == "sort-op");
    }

    [Fact]
    public async Task ValidateAsync_Allows_Generic_Output_Alias_Downstream_References()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new
                {
                    id = "call-workflow",
                    kind = "actionActivity",
                    caption = "Call Workflow",
                    action = new
                    {
                        id = "call-workflow-action",
                        kind = "callWorkflow",
                        targetWorkflowId = "wf-1",
                        outputWorkflowVariableName = "workflowInstance"
                    }
                },
                new
                {
                    id = "generate-document",
                    kind = "actionActivity",
                    caption = "Generate Document",
                    action = new
                    {
                        id = "generate-document-action",
                        kind = "generateDocument",
                        templateId = "tpl-1",
                        outputFileDocumentVariableName = "invoiceDoc"
                    }
                },
                new
                {
                    id = "external-call",
                    kind = "actionActivity",
                    caption = "External Call",
                    action = new
                    {
                        id = "external-call-action",
                        kind = "callExternalAction",
                        serviceId = "svc-1",
                        externalActionId = "op-1",
                        returnVariableName = "externalResult"
                    }
                },
                new
                {
                    id = "change-doc",
                    kind = "actionActivity",
                    caption = "Use Document",
                    action = new
                    {
                        id = "change-doc-action",
                        kind = "changeVariable",
                        targetVariableName = "invoiceDoc",
                        newValueExpression = "$invoiceDoc"
                    }
                },
                new
                {
                    id = "change-workflow",
                    kind = "actionActivity",
                    caption = "Use Workflow",
                    action = new
                    {
                        id = "change-workflow-action",
                        kind = "changeVariable",
                        targetVariableName = "workflowInstance",
                        newValueExpression = "$workflowInstance"
                    }
                },
                new
                {
                    id = "change-external",
                    kind = "actionActivity",
                    caption = "Use External Result",
                    action = new
                    {
                        id = "change-external-action",
                        kind = "changeVariable",
                        targetVariableName = "externalResult",
                        newValueExpression = "$externalResult"
                    }
                },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Edge("f1", "start", "call-workflow"),
                Edge("f2", "call-workflow", "generate-document"),
                Edge("f3", "generate-document", "external-call"),
                Edge("f4", "external-call", "change-doc"),
                Edge("f5", "change-doc", "change-workflow"),
                Edge("f6", "change-workflow", "change-external"),
                Edge("f7", "change-external", "end")
            ],
            parameters: Array.Empty<object>(),
            id: "mf-generic-output-alias-validation",
            options: JsonOptions);
        var service = CreateValidationService();

        var result = await service.ValidateAsync(
            "mf-generic-output-alias-validation",
            new ValidateMicroflowRequestDto { Schema = schema, Mode = "testRun", IncludeWarnings = true },
            CancellationToken.None);

        Assert.DoesNotContain(result.Issues, issue =>
            issue.Code == MicroflowValidationCodes.VariableNotFound
            && issue.ObjectId is "change-doc" or "change-workflow" or "change-external");
    }

    [Fact]
    public async Task ValidateAsync_ListOperationContains_Result_Is_Not_Treated_As_List()
    {
        var schema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["schemaVersion"] = "flowgram.microflow.v1",
            ["id"] = "mf-contains-not-list",
            ["name"] = "mf-contains-not-list",
            ["displayName"] = "mf-contains-not-list",
            ["moduleId"] = "Sales",
            ["parameters"] = new JsonArray(),
            ["returnType"] = new JsonObject { ["kind"] = "void" },
            ["workflow"] = new JsonObject
            {
                ["nodes"] = new JsonArray
                {
                    Node("start", "startEvent", "root-collection"),
                    Node("seed", "actionActivity", "root-collection", new JsonObject
                    {
                        ["action"] = new JsonObject
                        {
                            ["id"] = "seed-action",
                            ["kind"] = "createList",
                            ["outputVariableName"] = "numbers",
                            ["dataType"] = new JsonObject
                            {
                                ["kind"] = "list",
                                ["itemType"] = new JsonObject { ["kind"] = "integer" }
                            }
                        }
                    }),
                    Node("contains", "actionActivity", "root-collection", new JsonObject
                    {
                        ["action"] = new JsonObject
                        {
                            ["id"] = "contains-action",
                            ["kind"] = "listOperation",
                            ["operation"] = "contains",
                            ["leftListVariableName"] = "numbers",
                            ["outputVariableName"] = "hasFive",
                            ["itemExpression"] = "5"
                        }
                    }),
                    Node("loop", "loopedActivity", "root-collection", new JsonObject
                    {
                        ["bodyCollectionId"] = "loop-body",
                        ["loopSource"] = new JsonObject
                        {
                            ["kind"] = "iterableList",
                            ["listVariableName"] = "hasFive",
                            ["iteratorVariableName"] = "item"
                        }
                    })
                },
                ["edges"] = new JsonArray
                {
                    Edge("f1", "start", "seed"),
                    Edge("f2", "seed", "contains"),
                    Edge("f3", "contains", "loop")
                }
            },
            ["editor"] = new JsonObject(),
            ["audit"] = new JsonObject()
        }, JsonOptions);
        var service = CreateValidationService();

        var result = await service.ValidateAsync(
            "mf-contains-not-list",
            new ValidateMicroflowRequestDto { Schema = schema, Mode = "testRun", IncludeWarnings = true },
            CancellationToken.None);

        Assert.Contains(result.Issues, issue =>
            issue.Code == MicroflowValidationCodes.VariableTypeMismatch
            && issue.ObjectId == "loop"
            && issue.FieldPath?.Contains("loopSource.listVariableName", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task ValidateAsync_RetrieveWorkflows_Output_Can_Be_Used_As_Loop_List()
    {
        var schema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["schemaVersion"] = "flowgram.microflow.v1",
            ["id"] = "mf-retrieve-workflows-list",
            ["name"] = "mf-retrieve-workflows-list",
            ["displayName"] = "mf-retrieve-workflows-list",
            ["moduleId"] = "Sales",
            ["parameters"] = new JsonArray(),
            ["returnType"] = new JsonObject { ["kind"] = "void" },
            ["workflow"] = new JsonObject
            {
                ["nodes"] = new JsonArray
                {
                    Node("start", "startEvent", "root-collection"),
                    Node("retrieve", "actionActivity", "root-collection", new JsonObject
                    {
                        ["action"] = new JsonObject
                        {
                            ["id"] = "retrieve-action",
                            ["kind"] = "retrieveWorkflows",
                            ["outputListVariableName"] = "workflowList"
                        }
                    }),
                    Node("loop", "loopedActivity", "root-collection", new JsonObject
                    {
                        ["bodyCollectionId"] = "loop-body",
                        ["loopSource"] = new JsonObject
                        {
                            ["kind"] = "iterableList",
                            ["listVariableName"] = "workflowList",
                            ["iteratorVariableName"] = "workflowItem"
                        }
                    })
                },
                ["edges"] = new JsonArray
                {
                    Edge("f1", "start", "retrieve"),
                    Edge("f2", "retrieve", "loop")
                }
            },
            ["editor"] = new JsonObject(),
            ["audit"] = new JsonObject()
        }, JsonOptions);
        var service = CreateValidationService();

        var result = await service.ValidateAsync(
            "mf-retrieve-workflows-list",
            new ValidateMicroflowRequestDto { Schema = schema, Mode = "testRun", IncludeWarnings = true },
            CancellationToken.None);

        Assert.DoesNotContain(result.Issues, issue =>
            issue.Code == MicroflowValidationCodes.VariableTypeMismatch
            && issue.ObjectId == "loop"
            && issue.FieldPath?.Contains("loopSource.listVariableName", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task ValidateAsync_Cast_OutputVariable_Alias_Is_Available_Downstream()
    {
        var schema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["schemaVersion"] = "flowgram.microflow.v1",
            ["id"] = "mf-cast-output-alias",
            ["name"] = "mf-cast-output-alias",
            ["displayName"] = "mf-cast-output-alias",
            ["moduleId"] = "Sales",
            ["parameters"] = new JsonArray(),
            ["returnType"] = new JsonObject { ["kind"] = "void" },
            ["workflow"] = new JsonObject
            {
                ["nodes"] = new JsonArray
                {
                    Node("start", "startEvent", "root-collection"),
                    Node("source", "actionActivity", "root-collection", new JsonObject
                    {
                        ["action"] = new JsonObject
                        {
                            ["id"] = "source-action",
                            ["kind"] = "createObject",
                            ["entityQualifiedName"] = "Sales.Member",
                            ["outputVariableName"] = "memberSource"
                        }
                    }),
                    Node("cast", "actionActivity", "root-collection", new JsonObject
                    {
                        ["action"] = new JsonObject
                        {
                            ["id"] = "cast-action",
                            ["kind"] = "cast",
                            ["sourceVariable"] = "memberSource",
                            ["targetEntity"] = "Sales.Member",
                            ["outputVariable"] = "memberResult"
                        }
                    }),
                    Node("change", "actionActivity", "root-collection", new JsonObject
                    {
                        ["action"] = new JsonObject
                        {
                            ["id"] = "change-action",
                            ["kind"] = "changeVariable",
                            ["targetVariableName"] = "memberResult",
                            ["newValueExpression"] = "$memberResult"
                        }
                    })
                },
                ["edges"] = new JsonArray
                {
                    Edge("f1", "start", "source"),
                    Edge("f2", "source", "cast"),
                    Edge("f3", "cast", "change")
                }
            },
            ["editor"] = new JsonObject(),
            ["audit"] = new JsonObject()
        }, JsonOptions);
        var service = CreateValidationService();

        var result = await service.ValidateAsync(
            "mf-cast-output-alias",
            new ValidateMicroflowRequestDto { Schema = schema, Mode = "testRun", IncludeWarnings = true },
            CancellationToken.None);

        Assert.DoesNotContain(result.Issues, issue =>
            issue.Code == MicroflowValidationCodes.VariableNotFound
            && issue.ObjectId == "change"
            && issue.FieldPath?.Contains("targetVariableName", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task ValidateAsync_CreateList_ListVariableName_Alias_Is_Available_Downstream()
    {
        var schema = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["schemaVersion"] = "flowgram.microflow.v1",
            ["id"] = "mf-create-list-alias",
            ["name"] = "mf-create-list-alias",
            ["displayName"] = "mf-create-list-alias",
            ["moduleId"] = "Sales",
            ["parameters"] = new JsonArray(),
            ["returnType"] = new JsonObject { ["kind"] = "void" },
            ["workflow"] = new JsonObject
            {
                ["nodes"] = new JsonArray
                {
                    Node("start", "startEvent", "root-collection"),
                    Node("create-list", "actionActivity", "root-collection", new JsonObject
                    {
                        ["action"] = new JsonObject
                        {
                            ["id"] = "create-list-action",
                            ["kind"] = "createList",
                            ["listVariableName"] = "items",
                            ["dataType"] = new JsonObject
                            {
                                ["kind"] = "list",
                                ["itemType"] = new JsonObject { ["kind"] = "integer" }
                            }
                        }
                    }),
                    Node("change", "actionActivity", "root-collection", new JsonObject
                    {
                        ["action"] = new JsonObject
                        {
                            ["id"] = "change-action",
                            ["kind"] = "changeVariable",
                            ["targetVariableName"] = "items",
                            ["newValueExpression"] = "$items"
                        }
                    })
                },
                ["edges"] = new JsonArray
                {
                    Edge("f1", "start", "create-list"),
                    Edge("f2", "create-list", "change")
                }
            },
            ["editor"] = new JsonObject(),
            ["audit"] = new JsonObject()
        }, JsonOptions);
        var service = CreateValidationService();

        var result = await service.ValidateAsync(
            "mf-create-list-alias",
            new ValidateMicroflowRequestDto { Schema = schema, Mode = "testRun", IncludeWarnings = true },
            CancellationToken.None);

        Assert.DoesNotContain(result.Issues, issue =>
            issue.Code == MicroflowValidationCodes.VariableNotFound
            && issue.ObjectId == "change"
            && issue.FieldPath?.Contains("targetVariableName", StringComparison.Ordinal) == true);
    }

    private static MicroflowValidationService CreateValidationService(MicroflowMetadataCatalogDto? catalog = null)
    {
        var metadata = Substitute.For<IMicroflowMetadataService>();
        metadata.GetCatalogAsync(Arg.Any<GetMicroflowMetadataRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(catalog ?? new MicroflowMetadataCatalogDto { UpdatedAt = DateTimeOffset.UtcNow });
        var context = Substitute.For<IMicroflowRequestContextAccessor>();
        context.Current.Returns(new MicroflowRequestContext { WorkspaceId = "workspace-test" });
        return new MicroflowValidationService(
            Substitute.For<IMicroflowResourceRepository>(),
            Substitute.For<IMicroflowSchemaSnapshotRepository>(),
            metadata,
            new MicroflowSchemaReader(),
            new MicroflowActionSupportMatrix(),
            context,
            new TestClock());
    }

    private static JsonObject Node(string id, string kind, string collectionId, JsonObject? extraData = null)
    {
        var data = new JsonObject
        {
            ["objectId"] = id,
            ["objectKind"] = kind,
            ["collectionId"] = collectionId,
            ["title"] = id,
            ["officialType"] = $"Microflows${kind}",
        };
        if (extraData is not null)
        {
            foreach (var property in extraData)
            {
                data[property.Key] = property.Value?.DeepClone();
            }
        }

        return new JsonObject
        {
            ["id"] = id,
            ["type"] = kind,
            ["data"] = data,
            ["meta"] = new JsonObject
            {
                ["collectionId"] = collectionId,
                ["position"] = new JsonObject { ["x"] = 0, ["y"] = 0 }
            }
        };
    }

    private static JsonObject Edge(
        string id,
        string source,
        string target,
        string edgeKind = "sequence",
        string collectionId = "root-collection",
        bool topLevelCaseValues = false,
        bool? value = null)
    {
        var edge = new JsonObject
        {
            ["id"] = id,
            ["sourceNodeID"] = source,
            ["targetNodeID"] = target,
            ["edgeKind"] = edgeKind,
            ["data"] = new JsonObject
            {
                ["flowId"] = id,
                ["flowKind"] = "sequence",
                ["edgeKind"] = edgeKind,
                ["collectionId"] = collectionId
            }
        };
        if (value.HasValue)
        {
            var cases = new JsonArray
            {
                new JsonObject { ["kind"] = "boolean", ["value"] = value.Value }
            };
            if (topLevelCaseValues)
            {
                edge["caseValues"] = cases;
            }
            else
            {
                ((JsonObject)edge["data"]!)["caseValues"] = cases;
            }
        }

        return edge;
    }

    private sealed class TestClock : IMicroflowClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
