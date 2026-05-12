using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Services;

namespace Atlas.AppHost.Tests.Microflows;

public sealed class MicroflowExecutionPlanGatewayTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Build_Compiles_ParallelGateway_Split_And_Merge_Descriptors()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new { id = "fork", kind = "parallelGateway", caption = "Fork" },
                new { id = "left", kind = "actionActivity", caption = "Left", action = new { id = "left-action", kind = "createVariable", variableName = "leftValue", initialValue = "1" } },
                new { id = "right", kind = "actionActivity", caption = "Right", action = new { id = "right-action", kind = "createVariable", variableName = "rightValue", initialValue = "2" } },
                new { id = "join", kind = "parallelGateway", caption = "Join" },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Flow("f1", "start", "fork"),
                Flow("f2", "fork", "left"),
                Flow("f3", "fork", "right"),
                Flow("f4", "left", "join"),
                Flow("f5", "right", "join"),
                Flow("f6", "join", "end")
            ],
            parameters: null,
            id: "mf-gateway-plan",
            options: JsonOptions);
        var options = new MicroflowExecutionPlanLoadOptions { Mode = MicroflowExecutionPlanMode.TestRun };
        var runtimeDto = new MicroflowRuntimeDtoBuilder(new MicroflowSchemaReader(), new MicroflowActionSupportMatrix(), new TestClock())
            .Build(schema, options);
        var plan = new MicroflowExecutionPlanBuilder(new MicroflowExecutionPlanValidator(), new TestClock())
            .Build(runtimeDto, options);

        var fork = Assert.Single(plan.Gateways, gateway => gateway.ObjectId == "fork");
        Assert.Equal("parallelGateway", fork.Kind);
        Assert.Equal("split", fork.Role);
        Assert.Equal(["f1"], fork.IncomingFlowIds);
        Assert.Equal(["f2", "f3"], fork.OutgoingFlowIds);
        Assert.Equal(["f2", "f3"], fork.BranchFlowIds);

        var join = Assert.Single(plan.Gateways, gateway => gateway.ObjectId == "join");
        Assert.Equal("merge", join.Role);
        Assert.Equal(["f4", "f5"], join.IncomingFlowIds);
        Assert.Equal(["f6"], join.OutgoingFlowIds);
    }

    [Fact]
    public void Build_Allows_InclusiveGateway_Conditional_Branch_Flows()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new { id = "fork", kind = "inclusiveGateway", caption = "Fork" },
                new { id = "left", kind = "actionActivity", caption = "Left", action = new { id = "left-action", kind = "createVariable", variableName = "leftValue", dataType = new { kind = "integer" }, initialValue = "1" } },
                new { id = "right", kind = "actionActivity", caption = "Right", action = new { id = "right-action", kind = "createVariable", variableName = "rightValue", dataType = new { kind = "integer" }, initialValue = "2" } },
                new { id = "join", kind = "inclusiveGateway", caption = "Join" },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Flow("f1", "start", "fork"),
                ConditionFlow("f2", "fork", "left", "1 = 1"),
                ConditionFlow("f3", "fork", "right", "2 = 2"),
                Flow("f4", "left", "join"),
                Flow("f5", "right", "join"),
                Flow("f6", "join", "end")
            ],
            parameters: null,
            id: "mf-inclusive-gateway-plan",
            options: JsonOptions);
        var options = new MicroflowExecutionPlanLoadOptions { Mode = MicroflowExecutionPlanMode.TestRun };
        var runtimeDto = new MicroflowRuntimeDtoBuilder(new MicroflowSchemaReader(), new MicroflowActionSupportMatrix(), new TestClock())
            .Build(schema, options);
        var plan = new MicroflowExecutionPlanBuilder(new MicroflowExecutionPlanValidator(), new TestClock())
            .Build(runtimeDto, options);

        Assert.Equal(0, plan.Validation.ErrorCount);
        Assert.DoesNotContain(plan.Validation.Diagnostics, issue => issue.Code == "RUNTIME_DECISION_FLOW_SOURCE_INVALID");
        Assert.DoesNotContain(plan.Validation.Diagnostics, issue => issue.Code == "RUNTIME_NODE_DEAD_END" && issue.ObjectId == "fork");
        var fork = Assert.Single(plan.Gateways, gateway => gateway.ObjectId == "fork");
        Assert.Equal("split", fork.Role);
        Assert.Equal(["f2", "f3"], fork.BranchFlowIds);
    }

    [Fact]
    public void Validate_Rejects_Invalid_Gateway_Descriptors()
    {
        var plan = new MicroflowExecutionPlan
        {
            Id = "plan-invalid-gateway",
            SchemaId = "schema-invalid-gateway",
            StartNodeId = "start",
            EndNodeIds = ["end"],
            Nodes =
            [
                new MicroflowExecutionNode { ObjectId = "start", Kind = "startEvent", RuntimeBehavior = "executable" },
                new MicroflowExecutionNode { ObjectId = "fork", Kind = "parallelGateway", RuntimeBehavior = "executable" },
                new MicroflowExecutionNode { ObjectId = "end", Kind = "endEvent", RuntimeBehavior = "executable" }
            ],
            Flows =
            [
                new MicroflowExecutionFlow { FlowId = "f1", ControlFlow = "normal", OriginObjectId = "start", DestinationObjectId = "fork" },
                new MicroflowExecutionFlow { FlowId = "f2", ControlFlow = "normal", OriginObjectId = "fork", DestinationObjectId = "end" }
            ],
            NormalFlows =
            [
                new MicroflowExecutionFlow { FlowId = "f1", ControlFlow = "normal", OriginObjectId = "start", DestinationObjectId = "fork" },
                new MicroflowExecutionFlow { FlowId = "f2", ControlFlow = "normal", OriginObjectId = "fork", DestinationObjectId = "end" }
            ],
            Gateways =
            [
                new MicroflowExecutionGateway
                {
                    ObjectId = "fork",
                    Kind = "parallelGateway",
                    Role = "split",
                    IncomingFlowIds = ["f1"],
                    OutgoingFlowIds = ["f2", "missing-flow"],
                    BranchFlowIds = ["f2"]
                },
                new MicroflowExecutionGateway
                {
                    ObjectId = "missing-gateway",
                    Kind = "parallelGateway",
                    Role = "merge",
                    IncomingFlowIds = [],
                    OutgoingFlowIds = []
                }
            ]
        };

        var result = new MicroflowExecutionPlanValidator()
            .Validate(plan, new MicroflowExecutionPlanLoadOptions { Mode = MicroflowExecutionPlanMode.TestRun });

        Assert.Contains(result.Diagnostics, issue => issue.Code == "RUNTIME_GATEWAY_FLOW_NOT_FOUND" && issue.FlowId == "missing-flow");
        Assert.Contains(result.Diagnostics, issue => issue.Code == "RUNTIME_GATEWAY_SPLIT_BRANCH_MISSING" && issue.ObjectId == "fork");
        Assert.Contains(result.Diagnostics, issue => issue.Code == "RUNTIME_GATEWAY_NODE_NOT_FOUND" && issue.ObjectId == "missing-gateway");
    }

    [Fact]
    public void Build_RuntimeDto_Tracks_Generic_And_Nested_Output_Variables()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new
                {
                    id = "call-mf",
                    kind = "actionActivity",
                    caption = "Call MF",
                    action = new
                    {
                        id = "call-mf-action",
                        kind = "callMicroflow",
                        targetMicroflowId = "child-mf",
                        returnValue = new
                        {
                            storeResult = true,
                            outputVariableName = "callResult"
                        }
                    }
                },
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
                    id = "call-external",
                    kind = "actionActivity",
                    caption = "Call External",
                    action = new
                    {
                        id = "call-external-action",
                        kind = "callExternalAction",
                        serviceId = "svc-1",
                        externalActionId = "op-1",
                        returnVariableName = "externalResult"
                    }
                },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Flow("f1", "start", "call-mf"),
                Flow("f2", "call-mf", "call-workflow"),
                Flow("f3", "call-workflow", "generate-document"),
                Flow("f4", "generate-document", "call-external"),
                Flow("f5", "call-external", "end")
            ],
            parameters: null,
            id: "mf-runtime-output-aliases",
            options: JsonOptions);
        var options = new MicroflowExecutionPlanLoadOptions { Mode = MicroflowExecutionPlanMode.TestRun };
        var runtimeDto = new MicroflowRuntimeDtoBuilder(new MicroflowSchemaReader(), new MicroflowActionSupportMatrix(), new TestClock())
            .Build(schema, options);

        Assert.Contains(runtimeDto.Variables, variable => variable.Name == "callResult");
        Assert.Contains(runtimeDto.Variables, variable => variable.Name == "workflowInstance" && ReadKind(variable.DataTypeJson) == "object");
        Assert.Contains(runtimeDto.Variables, variable => variable.Name == "invoiceDoc" && ReadEntity(variable.DataTypeJson) == "System.FileDocument");
        Assert.Contains(runtimeDto.Variables, variable => variable.Name == "externalResult");

        Assert.Contains(runtimeDto.Nodes, node => node.ObjectId == "call-mf" && node.OutputVariableNames.Contains("callResult"));
        Assert.Contains(runtimeDto.Nodes, node => node.ObjectId == "call-workflow" && node.OutputVariableNames.Contains("workflowInstance"));
        Assert.Contains(runtimeDto.Nodes, node => node.ObjectId == "generate-document" && node.OutputVariableNames.Contains("invoiceDoc"));
        Assert.Contains(runtimeDto.Nodes, node => node.ObjectId == "call-external" && node.OutputVariableNames.Contains("externalResult"));
    }

    [Fact]
    public void Build_RuntimeDto_Tracks_RestCall_Status_And_Header_Output_Variables()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new
                {
                    id = "rest",
                    kind = "actionActivity",
                    caption = "Rest",
                    action = new
                    {
                        id = "rest-action",
                        kind = "restCall",
                        request = new
                        {
                            method = "GET",
                            urlExpression = "\"https://example.test\""
                        },
                        response = new
                        {
                            statusCodeVariableName = "statusCode",
                            headersVariableName = "responseHeaders",
                            handling = new
                            {
                                kind = "string",
                                outputVariableName = "responseBody"
                            }
                        }
                    }
                },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Flow("f1", "start", "rest"),
                Flow("f2", "rest", "end")
            ],
            parameters: null,
            id: "mf-runtime-restcall-output-aliases",
            options: JsonOptions);
        var options = new MicroflowExecutionPlanLoadOptions { Mode = MicroflowExecutionPlanMode.TestRun };
        var runtimeDto = new MicroflowRuntimeDtoBuilder(new MicroflowSchemaReader(), new MicroflowActionSupportMatrix(), new TestClock())
            .Build(schema, options);

        Assert.Contains(runtimeDto.Variables, variable => variable.Name == "responseBody");
        Assert.Contains(runtimeDto.Variables, variable => variable.Name == "statusCode" && ReadKind(variable.DataTypeJson) == "integer");
        Assert.Contains(runtimeDto.Variables, variable => variable.Name == "responseHeaders" && ReadKind(variable.DataTypeJson) == "json");
        Assert.Contains(runtimeDto.Variables, variable => variable.Name == "$latestHttpResponse" && ReadEntity(variable.DataTypeJson) == "System.HttpResponse");

        var restNode = Assert.Single(runtimeDto.Nodes, node => node.ObjectId == "rest");
        Assert.Contains("responseBody", restNode.OutputVariableNames);
        Assert.Contains("statusCode", restNode.OutputVariableNames);
        Assert.Contains("responseHeaders", restNode.OutputVariableNames);
    }

    [Fact]
    public void Build_RuntimeDto_Declares_LatestSoapFault_For_WebServiceCall()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new
                {
                    id = "soap",
                    kind = "actionActivity",
                    caption = "Soap",
                    action = new
                    {
                        id = "soap-action",
                        kind = "webServiceCall",
                        endpoint = "https://soap.test/service",
                        operation = "SubmitOrder",
                        outputVariableName = "soapResult"
                    }
                },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Flow("f1", "start", "soap"),
                Flow("f2", "soap", "end")
            ],
            parameters: null,
            id: "mf-runtime-webservice-latest-soapfault",
            options: JsonOptions);
        var options = new MicroflowExecutionPlanLoadOptions { Mode = MicroflowExecutionPlanMode.TestRun };
        var runtimeDto = new MicroflowRuntimeDtoBuilder(new MicroflowSchemaReader(), new MicroflowActionSupportMatrix(), new TestClock())
            .Build(schema, options);

        Assert.Contains(runtimeDto.Variables, variable => variable.Name == "soapResult");
        Assert.Contains(runtimeDto.Variables, variable => variable.Name == "$latestSoapFault" && ReadEntity(variable.DataTypeJson) == "System.SoapFault");
    }

    [Fact]
    public void Build_RuntimeDto_Infers_ListOperationContains_And_AggregateList_Output_Types()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new
                {
                    id = "contains",
                    kind = "actionActivity",
                    caption = "Contains",
                    action = new
                    {
                        id = "contains-action",
                        kind = "listOperation",
                        operation = "contains",
                        leftListVariableName = "numbers",
                        outputVariableName = "hasFive"
                    }
                },
                new
                {
                    id = "aggregate",
                    kind = "actionActivity",
                    caption = "Aggregate",
                    action = new
                    {
                        id = "aggregate-action",
                        kind = "aggregateList",
                        sourceListVariableName = "numbers",
                        aggregateFunction = "sum",
                        outputVariableName = "total",
                        resultType = new { kind = "integer" }
                    }
                },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Flow("f1", "start", "contains"),
                Flow("f2", "contains", "aggregate"),
                Flow("f3", "aggregate", "end")
            ],
            parameters:
            [
                new
                {
                    id = "numbers-param",
                    name = "numbers",
                    type = new
                    {
                        kind = "list",
                        itemType = new { kind = "integer" }
                    }
                }
            ],
            id: "mf-runtime-list-scalar-types",
            options: JsonOptions);
        var options = new MicroflowExecutionPlanLoadOptions { Mode = MicroflowExecutionPlanMode.TestRun };
        var runtimeDto = new MicroflowRuntimeDtoBuilder(new MicroflowSchemaReader(), new MicroflowActionSupportMatrix(), new TestClock())
            .Build(schema, options);

        Assert.Contains(runtimeDto.Variables, variable => variable.Name == "hasFive" && ReadKind(variable.DataTypeJson) == "boolean");
        Assert.Contains(runtimeDto.Variables, variable => variable.Name == "total" && ReadKind(variable.DataTypeJson) == "integer");
    }

    [Fact]
    public void Build_RuntimeDto_Infers_Workflow_List_And_ExportXml_Output_Types()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new
                {
                    id = "retrieve-workflows",
                    kind = "actionActivity",
                    caption = "Retrieve Workflows",
                    action = new
                    {
                        id = "retrieve-workflows-action",
                        kind = "retrieveWorkflows",
                        outputListVariableName = "workflowList"
                    }
                },
                new
                {
                    id = "export-xml",
                    kind = "actionActivity",
                    caption = "Export XML",
                    action = new
                    {
                        id = "export-xml-action",
                        kind = "exportXml",
                        outputVariableName = "documentRef",
                        outputType = "fileDocument"
                    }
                },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Flow("f1", "start", "retrieve-workflows"),
                Flow("f2", "retrieve-workflows", "export-xml"),
                Flow("f3", "export-xml", "end")
            ],
            parameters: null,
            id: "mf-runtime-workflow-export-types",
            options: JsonOptions);
        var options = new MicroflowExecutionPlanLoadOptions { Mode = MicroflowExecutionPlanMode.TestRun };
        var runtimeDto = new MicroflowRuntimeDtoBuilder(new MicroflowSchemaReader(), new MicroflowActionSupportMatrix(), new TestClock())
            .Build(schema, options);

        Assert.Contains(runtimeDto.Variables, variable => variable.Name == "workflowList" && ReadKind(variable.DataTypeJson) == "list");
        Assert.Contains(runtimeDto.Variables, variable => variable.Name == "documentRef" && ReadEntity(variable.DataTypeJson) == "System.FileDocument");
    }

    [Fact]
    public void Build_RuntimeDto_Infers_Cast_OutputVariable_Alias_As_Object_Type()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new
                {
                    id = "cast",
                    kind = "actionActivity",
                    caption = "Cast",
                    action = new
                    {
                        id = "cast-action",
                        kind = "cast",
                        sourceVariable = "source",
                        targetEntity = "Sales.Member",
                        outputVariable = "memberResult"
                    }
                },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Flow("f1", "start", "cast"),
                Flow("f2", "cast", "end")
            ],
            parameters:
            [
                new
                {
                    id = "source-param",
                    name = "source",
                    type = new
                    {
                        kind = "object",
                        entityQualifiedName = "Sales.Member"
                    }
                }
            ],
            id: "mf-runtime-cast-output-alias",
            options: JsonOptions);
        var options = new MicroflowExecutionPlanLoadOptions { Mode = MicroflowExecutionPlanMode.TestRun };
        var runtimeDto = new MicroflowRuntimeDtoBuilder(new MicroflowSchemaReader(), new MicroflowActionSupportMatrix(), new TestClock())
            .Build(schema, options);

        Assert.Contains(runtimeDto.Variables, variable => variable.Name == "memberResult" && ReadEntity(variable.DataTypeJson) == "Sales.Member");
        Assert.Contains(runtimeDto.Nodes, node => node.ObjectId == "cast" && node.OutputVariableNames.Contains("memberResult"));
    }

    [Fact]
    public void Build_RuntimeDto_Tracks_CreateList_ListVariableName_Alias_As_Output()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new
                {
                    id = "create-list",
                    kind = "actionActivity",
                    caption = "Create List",
                    action = new
                    {
                        id = "create-list-action",
                        kind = "createList",
                        listVariableName = "items",
                        dataType = new
                        {
                            kind = "list",
                            itemType = new { kind = "integer" }
                        }
                    }
                },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Flow("f1", "start", "create-list"),
                Flow("f2", "create-list", "end")
            ],
            parameters: null,
            id: "mf-runtime-create-list-alias",
            options: JsonOptions);
        var options = new MicroflowExecutionPlanLoadOptions { Mode = MicroflowExecutionPlanMode.TestRun };
        var runtimeDto = new MicroflowRuntimeDtoBuilder(new MicroflowSchemaReader(), new MicroflowActionSupportMatrix(), new TestClock())
            .Build(schema, options);

        Assert.Contains(runtimeDto.Variables, variable => variable.Name == "items" && ReadKind(variable.DataTypeJson) == "list");
        Assert.Contains(runtimeDto.Nodes, node => node.ObjectId == "create-list" && node.OutputVariableNames.Contains("items"));
    }

    [Fact]
    public void Build_RuntimeDto_Tracks_Action_Input_Variable_Aliases()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new
                {
                    id = "cast",
                    kind = "actionActivity",
                    caption = "Cast",
                    action = new
                    {
                        id = "cast-action",
                        kind = "cast",
                        sourceVariable = "memberSource",
                        targetEntity = "Sales.Member",
                        outputVariable = "memberResult"
                    }
                },
                new
                {
                    id = "change-workflow",
                    kind = "actionActivity",
                    caption = "Workflow State",
                    action = new
                    {
                        id = "workflow-action",
                        kind = "changeWorkflowState",
                        workflowInstanceVariableName = "workflowInstance"
                    }
                },
                new
                {
                    id = "download",
                    kind = "actionActivity",
                    caption = "Download",
                    action = new
                    {
                        id = "download-action",
                        kind = "downloadFile",
                        fileDocumentVariableName = "invoiceDoc"
                    }
                },
                new
                {
                    id = "external",
                    kind = "actionActivity",
                    caption = "Delete External",
                    action = new
                    {
                        id = "external-action",
                        kind = "deleteExternalObject",
                        externalObjectVariableName = "externalObject"
                    }
                },
                new
                {
                    id = "user-task",
                    kind = "actionActivity",
                    caption = "Complete Task",
                    action = new
                    {
                        id = "user-task-action",
                        kind = "completeUserTask",
                        userTaskVariableName = "userTask"
                    }
                },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Flow("f1", "start", "cast"),
                Flow("f2", "cast", "change-workflow"),
                Flow("f3", "change-workflow", "download"),
                Flow("f4", "download", "external"),
                Flow("f5", "external", "user-task"),
                Flow("f6", "user-task", "end")
            ],
            parameters:
            [
                new { id = "member-source-param", name = "memberSource", type = new { kind = "object", entityQualifiedName = "Sales.Member" } },
                new { id = "workflow-instance-param", name = "workflowInstance", type = new { kind = "object", entityQualifiedName = "Workflow.Workflow" } },
                new { id = "invoice-doc-param", name = "invoiceDoc", type = new { kind = "object", entityQualifiedName = "System.FileDocument" } },
                new { id = "external-object-param", name = "externalObject", type = new { kind = "object", entityQualifiedName = "External.Object" } },
                new { id = "user-task-param", name = "userTask", type = new { kind = "object", entityQualifiedName = "Workflow.UserTask" } }
            ],
            id: "mf-runtime-input-variable-aliases",
            options: JsonOptions);
        var options = new MicroflowExecutionPlanLoadOptions { Mode = MicroflowExecutionPlanMode.TestRun };
        var runtimeDto = new MicroflowRuntimeDtoBuilder(new MicroflowSchemaReader(), new MicroflowActionSupportMatrix(), new TestClock())
            .Build(schema, options);

        Assert.Contains(runtimeDto.Nodes, node => node.ObjectId == "cast" && node.InputVariableNames.Contains("memberSource"));
        Assert.Contains(runtimeDto.Nodes, node => node.ObjectId == "change-workflow" && node.InputVariableNames.Contains("workflowInstance"));
        Assert.Contains(runtimeDto.Nodes, node => node.ObjectId == "download" && node.InputVariableNames.Contains("invoiceDoc"));
        Assert.Contains(runtimeDto.Nodes, node => node.ObjectId == "external" && node.InputVariableNames.Contains("externalObject"));
        Assert.Contains(runtimeDto.Nodes, node => node.ObjectId == "user-task" && node.InputVariableNames.Contains("userTask"));
    }

    [Fact]
    public void Build_RuntimeDto_Tracks_Nested_And_ActionSpecific_Input_Variable_References()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new
                {
                    id = "retrieve",
                    kind = "actionActivity",
                    caption = "Retrieve Association",
                    action = new
                    {
                        id = "retrieve-action",
                        kind = "retrieve",
                        retrieveSource = new
                        {
                            kind = "association",
                            associationQualifiedName = "Sales.Member_Order",
                            startVariableName = "memberSource"
                        },
                        outputVariableName = "orders"
                    }
                },
                new
                {
                    id = "change-members",
                    kind = "actionActivity",
                    caption = "Change Members",
                    action = new
                    {
                        id = "change-members-action",
                        kind = "changeMembers",
                        changeVariableName = "memberSource"
                    }
                },
                new
                {
                    id = "call-mf",
                    kind = "actionActivity",
                    caption = "Call MF",
                    action = new
                    {
                        id = "call-mf-action",
                        kind = "callMicroflow",
                        targetMicroflowId = "child-mf",
                        parameterMappings = new object[]
                        {
                            new { parameterName = "inputOrder", sourceVariableName = "orders" }
                        }
                    }
                },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Flow("f1", "start", "retrieve"),
                Flow("f2", "retrieve", "change-members"),
                Flow("f3", "change-members", "call-mf"),
                Flow("f4", "call-mf", "end")
            ],
            parameters:
            [
                new { id = "member-source-param", name = "memberSource", type = new { kind = "object", entityQualifiedName = "Sales.Member" } }
            ],
            id: "mf-runtime-input-variable-nested",
            options: JsonOptions);
        var options = new MicroflowExecutionPlanLoadOptions { Mode = MicroflowExecutionPlanMode.TestRun };
        var runtimeDto = new MicroflowRuntimeDtoBuilder(new MicroflowSchemaReader(), new MicroflowActionSupportMatrix(), new TestClock())
            .Build(schema, options);

        Assert.Contains(runtimeDto.Nodes, node => node.ObjectId == "retrieve" && node.InputVariableNames.Contains("memberSource"));
        Assert.Contains(runtimeDto.Nodes, node => node.ObjectId == "change-members" && node.InputVariableNames.Contains("memberSource"));
        Assert.Contains(runtimeDto.Nodes, node => node.ObjectId == "call-mf" && node.InputVariableNames.Contains("orders"));
    }

    [Fact]
    public void Build_RuntimeDto_Tracks_Loop_List_Source_As_Node_Input()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new
                {
                    id = "loop",
                    kind = "loopedActivity",
                    caption = "Loop Orders",
                    loopSource = new
                    {
                        kind = "iterableList",
                        listVariableName = "orders",
                        iteratorVariableName = "currentOrder"
                    }
                },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Flow("f1", "start", "loop"),
                Flow("f2", "loop", "end")
            ],
            parameters:
            [
                new
                {
                    id = "orders-param",
                    name = "orders",
                    type = new
                    {
                        kind = "list",
                        itemType = new { kind = "object", entityQualifiedName = "Sales.Order" }
                    }
                }
            ],
            id: "mf-runtime-loop-node-inputs",
            options: JsonOptions);
        var options = new MicroflowExecutionPlanLoadOptions { Mode = MicroflowExecutionPlanMode.TestRun };
        var runtimeDto = new MicroflowRuntimeDtoBuilder(new MicroflowSchemaReader(), new MicroflowActionSupportMatrix(), new TestClock())
            .Build(schema, options);

        Assert.Contains(runtimeDto.Nodes, node => node.ObjectId == "loop" && node.InputVariableNames.Contains("orders"));
    }

    [Fact]
    public void Build_RuntimeDto_Tracks_InheritanceSplit_Input_Object_As_Node_Input()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new
                {
                    id = "type-check",
                    kind = "inheritanceSplit",
                    caption = "Object Type",
                    inputObjectVariableName = "member",
                    generalizedEntityQualifiedName = "Sales.Member"
                },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Flow("f1", "start", "type-check"),
                Flow("f2", "type-check", "end")
            ],
            parameters:
            [
                new
                {
                    id = "member-param",
                    name = "member",
                    type = new
                    {
                        kind = "object",
                        entityQualifiedName = "Sales.Member"
                    }
                }
            ],
            id: "mf-runtime-inheritance-node-inputs",
            options: JsonOptions);
        var options = new MicroflowExecutionPlanLoadOptions { Mode = MicroflowExecutionPlanMode.TestRun };
        var runtimeDto = new MicroflowRuntimeDtoBuilder(new MicroflowSchemaReader(), new MicroflowActionSupportMatrix(), new TestClock())
            .Build(schema, options);

        Assert.Contains(runtimeDto.Nodes, node => node.ObjectId == "type-check" && node.InputVariableNames.Contains("member"));
    }

    [Fact]
    public void Build_RuntimeDto_Tracks_Object_Level_Metadata_Refs()
    {
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            objects:
            [
                new { id = "start", kind = "startEvent", caption = "Start" },
                new
                {
                    id = "enum-decision",
                    kind = "exclusiveSplit",
                    caption = "Tier?",
                    splitCondition = new
                    {
                        kind = "expression",
                        expression = "\"Gold\"",
                        resultType = "enumeration",
                        enumerationQualifiedName = "Sales.CustomerTier"
                    }
                },
                new
                {
                    id = "type-check",
                    kind = "inheritanceSplit",
                    caption = "Object Type",
                    inputObjectVariableName = "member",
                    generalizedEntityQualifiedName = "Sales.Member"
                },
                new { id = "end", kind = "endEvent", caption = "End" }
            ],
            flows:
            [
                Flow("f1", "start", "enum-decision"),
                Flow("f2", "enum-decision", "type-check"),
                Flow("f3", "type-check", "end")
            ],
            parameters:
            [
                new
                {
                    id = "member-param",
                    name = "member",
                    type = new
                    {
                        kind = "object",
                        entityQualifiedName = "Sales.Member"
                    }
                }
            ],
            id: "mf-runtime-object-metadata-refs",
            options: JsonOptions);
        var options = new MicroflowExecutionPlanLoadOptions { Mode = MicroflowExecutionPlanMode.TestRun };
        var runtimeDto = new MicroflowRuntimeDtoBuilder(new MicroflowSchemaReader(), new MicroflowActionSupportMatrix(), new TestClock())
            .Build(schema, options);

        Assert.Contains(runtimeDto.Nodes, node => node.ObjectId == "enum-decision"
            && node.MetadataRefs.Any(reference => reference.Kind == "enumeration"
                && reference.QualifiedName == "Sales.CustomerTier"
                && reference.FieldPath == "workflow.nodes.1.splitCondition.enumerationQualifiedName"));

        Assert.Contains(runtimeDto.Nodes, node => node.ObjectId == "type-check"
            && node.MetadataRefs.Any(reference => reference.Kind == "entity"
                && reference.QualifiedName == "Sales.Member"
                && reference.FieldPath == "workflow.nodes.2.generalizedEntityQualifiedName"));

        Assert.Contains(runtimeDto.MetadataRefs, reference => reference.Kind == "enumeration"
            && reference.QualifiedName == "Sales.CustomerTier"
            && reference.SourceObjectId == "enum-decision");
        Assert.Contains(runtimeDto.MetadataRefs, reference => reference.Kind == "entity"
            && reference.QualifiedName == "Sales.Member"
            && reference.SourceObjectId == "type-check"
            && reference.FieldPath == "workflow.nodes.2.generalizedEntityQualifiedName");
    }

    private static object Flow(string id, string source, string target)
        => new
        {
            id,
            kind = "sequence",
            originObjectId = source,
            destinationObjectId = target,
            caseValues = Array.Empty<object>(),
            isErrorHandler = false,
            editor = new { edgeKind = "sequence" }
        };

    private static object ConditionFlow(string id, string source, string target, string expression)
        => new
        {
            id,
            kind = "sequence",
            originObjectId = source,
            destinationObjectId = target,
            caseValues = new[] { new { kind = "expression", condition = expression, expression } },
            isErrorHandler = false,
            editor = new { edgeKind = "decisionCondition" }
        };

    private static string? ReadKind(JsonElement value)
        => value.ValueKind == JsonValueKind.Object && value.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String
            ? kind.GetString()
            : null;

    private static string? ReadEntity(JsonElement value)
        => value.ValueKind == JsonValueKind.Object && value.TryGetProperty("entityQualifiedName", out var entity) && entity.ValueKind == JsonValueKind.String
            ? entity.GetString()
            : null;

    private sealed class TestClock : IMicroflowClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
