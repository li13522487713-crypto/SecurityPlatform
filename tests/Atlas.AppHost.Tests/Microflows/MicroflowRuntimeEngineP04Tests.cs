using System.Text.Json;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.DependencyInjection;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Metadata;
using Atlas.Application.Microflows.Runtime.Security;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Atlas.AppHost.Tests.Microflows;

/// <summary>
/// P0-4 覆盖：
///   1. 之前 _ => Unsupported 兜底导致 errorEvent / loopedActivity / parallelGateway / inclusiveGateway / annotation 静默失败。
///   2. 之前 PendingClientCommand 被当作 success 继续推进流。
///   3. 之前 ShouldEnterErrorHandler 没有跳错误分支。
/// </summary>
public sealed class MicroflowRuntimeEngineP04Tests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Run_ErrorEvent_ProducesRuntimeErrorEventReached()
    {
        var schema = Schema(
            Objects(
                Start(),
                new { id = "thrower", kind = "errorEvent", caption = "Throw", errorCode = "DOMAIN_FAILED", message = "boom" }),
            Flows(Flow("f1", "start", "thrower")));

        var session = await RunAsync(schema);

        Assert.Equal("failed", session.Status);
        Assert.Equal("DOMAIN_FAILED", session.Error?.Code);
        Assert.Contains(session.Trace, frame => frame.ObjectId == "thrower" && frame.Status == "failed");
    }

    [Fact]
    public async Task Run_LoopedActivityWithoutBody_FailsWithLoopBodyNotFound()
    {
        var schema = Schema(
            Objects(
                Start(),
                new { id = "loop", kind = "loopedActivity", caption = "Loop" }),
            Flows(Flow("f1", "start", "loop")));

        var session = await RunAsync(schema);

        Assert.Equal("failed", session.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeLoopBodyNotFound, session.Error?.Code);
    }

    [Fact]
    public async Task Run_LoopedActivity_With_CustomErrorHandler_Routes_To_Handler()
    {
        var schema = Schema(
            Objects(
                Start(),
                new { id = "loop", kind = "loopedActivity", caption = "Loop", errorHandlingType = "customWithoutRollback" },
                new { id = "handler", kind = "exclusiveMerge", caption = "Handler" },
                End(returnValue: "$latestError")),
            Flows(
                Flow("f1", "start", "loop"),
                ErrorFlow("f2-err", "loop", "handler"),
                Flow("f3", "handler", "end")));

        var session = await RunAsync(schema);

        Assert.True(
            string.Equals("success", session.Status, StringComparison.OrdinalIgnoreCase),
            JsonSerializer.Serialize(session.Error, JsonOptions));
        Assert.Contains(session.Trace, frame => frame.ObjectId == "handler");
    }

    [Fact]
    public async Task Run_LoopedActivity_With_Continue_ErrorHandling_Continues_Normal_Flow()
    {
        var schema = Schema(
            Objects(
                Start(),
                new { id = "loop", kind = "loopedActivity", caption = "Loop", errorHandlingType = "continue" },
                End()),
            Flows(
                Flow("f1", "start", "loop"),
                Flow("f2", "loop", "end")));

        var session = await RunAsync(schema);

        Assert.True(
            string.Equals("success", session.Status, StringComparison.OrdinalIgnoreCase),
            JsonSerializer.Serialize(new { session.Error, Trace = session.Trace.Select(frame => new { frame.ObjectId, frame.Status, frame.Output, frame.VariablesSnapshot }) }, JsonOptions));
        Assert.Contains(session.Trace, frame => frame.ObjectId == "end" && frame.Status == "success");
    }

    [Fact]
    public async Task Run_LoopedActivity_With_Filter_Decision_And_ChangeVariable_Returns_Sum()
    {
        const string loopCollectionId = "loop-sum-body";
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            Objects(
                Start(),
                Action("create-total", "createVariable", new { variableName = "total", dataType = new { kind = "integer" }, initialValue = "0" }),
                Action("filter-positive", "filterList", new
                {
                    sourceListVariableName = "numbers",
                    outputVariableName = "positiveNumbers",
                    itemVariableName = "$item",
                    conditionExpression = "$item > 0",
                    itemType = new { kind = "integer" }
                }),
                new
                {
                    id = "loop-sum",
                    kind = "loopedActivity",
                    caption = "Loop positiveNumbers",
                    loopSource = new
                    {
                        kind = "iterableList",
                        listVariableName = "positiveNumbers",
                        iteratorVariableName = "currentNumber"
                    }
                },
                new
                {
                    id = "decision-positive",
                    kind = "exclusiveSplit",
                    caption = "currentNumber > 0",
                    collectionId = loopCollectionId,
                    parentObjectId = "loop-sum",
                    splitCondition = new { expression = "$currentNumber > 0", resultType = "boolean" }
                },
                new
                {
                    id = "change-total",
                    kind = "actionActivity",
                    caption = "change-total",
                    collectionId = loopCollectionId,
                    parentObjectId = "loop-sum",
                    action = MergeAction("change-total", "changeVariable", new
                    {
                        targetVariableName = "total",
                        newValueExpression = "$total + $currentNumber"
                    })
                },
                new { id = "loop-body-end", kind = "endEvent", caption = "Body return", collectionId = loopCollectionId, parentObjectId = "loop-sum" },
                new { id = "continue-loop", kind = "continueEvent", caption = "Continue", collectionId = loopCollectionId, parentObjectId = "loop-sum" },
                End(returnValue: "$total")),
            Flows(
                Flow("f-start-create", "start", "create-total"),
                Flow("f-create-filter", "create-total", "filter-positive"),
                Flow("f-filter-loop", "filter-positive", "loop-sum"),
                Flow("f-loop-end", "loop-sum", "end"),
                Flow("f-body-decision", "loop-sum", "decision-positive", loopCollectionId),
                BooleanFlow("f-decision-change", "decision-positive", "change-total", true, loopCollectionId),
                BooleanFlow("f-decision-continue", "decision-positive", "continue-loop", false, loopCollectionId),
                Flow("f-change-body-end", "change-total", "loop-body-end", loopCollectionId)),
            Parameters(new { id = "numbers", name = "numbers", dataType = "Integer", type = new { kind = "list", itemType = new { kind = "integer" } }, required = true }),
            "mf-list-loop-sum-test",
            JsonOptions);

        var session = await RunAsync(schema, new Dictionary<string, object?> { ["numbers"] = new[] { 1, 2, 3, 4, 5, 6 } });

        Assert.True(
            string.Equals("success", session.Status, StringComparison.OrdinalIgnoreCase),
            JsonSerializer.Serialize(new { session.Error, Trace = session.Trace.Select(frame => new { frame.ObjectId, frame.Status, frame.Output, frame.VariablesSnapshot }) }, JsonOptions));
        Assert.Equal("21", session.Output?.GetRawText());
        Assert.Contains(session.Trace, frame => frame.ObjectId == "filter-positive" && frame.Status == "success");
        Assert.Contains(session.Trace, frame => frame.ObjectId == "loop-sum" && frame.Status == "success");
        Assert.Equal(6, session.Trace.Count(frame => frame.ObjectId == "change-total" && frame.Status == "success"));
        Assert.Contains(session.Trace, frame => frame.ObjectId == "end" && frame.Status == "success");
    }

    [Fact]
    public async Task Run_ParallelGateway_FailsWhenOutgoingFlowMissing()
    {
        var schema = Schema(
            Objects(
                Start(),
                new { id = "fork", kind = "parallelGateway", caption = "Fork" }),
            Flows(Flow("f1", "start", "fork")));

        var session = await RunAsync(schema);

        Assert.Equal("failed", session.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeFlowNotFound, session.Error?.Code);
    }

    [Fact]
    public async Task Run_ParallelGateway_UsesRuntimeMainPath()
    {
        var schema = Schema(
            Objects(
                Start(),
                new { id = "fork", kind = "parallelGateway", caption = "Fork" },
                End()),
            Flows(
                Flow("f1", "start", "fork"),
                Flow("f2", "fork", "end")));

        var session = await RunAsync(schema);

        Assert.True(
            string.Equals("success", session.Status, StringComparison.OrdinalIgnoreCase),
            JsonSerializer.Serialize(new { session.Error, Trace = session.Trace.Select(frame => new { frame.ObjectId, frame.Status, frame.Output, frame.VariablesSnapshot }) }, JsonOptions));
        Assert.Contains(session.Trace, frame => frame.ObjectId == "fork" && frame.Status == "success");
    }

    [Fact]
    public async Task Run_ParallelGateway_Executes_All_Branches_Before_Join()
    {
        var schema = Schema(
            Objects(
                Start(),
                new { id = "fork", kind = "parallelGateway", caption = "Fork" },
                Action("left", "createVariable", new { variableName = "leftValue", dataType = new { kind = "string" }, initialValue = "\"L\"" }),
                Action("right", "createVariable", new { variableName = "rightValue", dataType = new { kind = "string" }, initialValue = "\"R\"" }),
                new { id = "join", kind = "parallelMerge", caption = "Join" },
                End(returnValue: "leftValue")),
            Flows(
                Flow("f1", "start", "fork"),
                Flow("f2", "fork", "left"),
                Flow("f3", "fork", "right"),
                Flow("f4", "left", "join"),
                Flow("f5", "right", "join"),
                Flow("f6", "join", "end")));

        var session = await RunAsync(schema);

        Assert.True(
            string.Equals("success", session.Status, StringComparison.OrdinalIgnoreCase),
            JsonSerializer.Serialize(new { session.Error, Trace = session.Trace.Select(frame => new { frame.ObjectId, frame.Status, frame.Output, frame.VariablesSnapshot }) }, JsonOptions));
        Assert.Contains(session.Trace, frame => frame.ObjectId == "left" && frame.Status == "success");
        Assert.Contains(session.Trace, frame => frame.ObjectId == "right" && frame.Status == "success");
        Assert.Contains(session.Trace, frame => frame.ObjectId == "join" && frame.Status == "success");
    }

    [Fact]
    public async Task Run_ParallelGateway_Fails_On_Variable_Write_Conflict()
    {
        var schema = Schema(
            Objects(
                Start(),
                new { id = "fork", kind = "parallelGateway", caption = "Fork" },
                Action("left", "createVariable", new { variableName = "sharedValue", dataType = new { kind = "string" }, initialValue = "\"L\"" }),
                Action("right", "createVariable", new { variableName = "sharedValue", dataType = new { kind = "string" }, initialValue = "\"R\"" }),
                new { id = "join", kind = "parallelMerge", caption = "Join" },
                End(returnValue: "sharedValue")),
            Flows(
                Flow("f1", "start", "fork"),
                Flow("f2", "fork", "left"),
                Flow("f3", "fork", "right"),
                Flow("f4", "left", "join"),
                Flow("f5", "right", "join"),
                Flow("f6", "join", "end")));

        var session = await RunAsync(schema);

        Assert.Equal("failed", session.Status);
        Assert.Equal("PARALLEL_VARIABLE_WRITE_CONFLICT", session.Error?.Code);
    }

    [Fact]
    public async Task Run_InclusiveGateway_Executes_All_Selected_Branches_Before_Join()
    {
        var schema = Schema(
            Objects(
                Start(),
                Action("seed", "createVariable", new { variableName = "gate", dataType = new { kind = "integer" }, initialValue = "0" }),
                new { id = "fork", kind = "inclusiveGateway", caption = "Inclusive Fork" },
                Action("left", "createVariable", new { variableName = "inclusiveA", dataType = new { kind = "integer" }, initialValue = "5" }),
                Action("right", "createVariable", new { variableName = "inclusiveB", dataType = new { kind = "integer" }, initialValue = "7" }),
                new { id = "join", kind = "inclusiveGateway", caption = "Inclusive Join" },
                Action("sum", "changeVariable", new { targetVariableName = "gate", newValueExpression = "$gate + $inclusiveA + $inclusiveB" }),
                End(returnValue: "$gate")),
            Flows(
                Flow("f1", "start", "seed"),
                Flow("f2", "seed", "fork"),
                ConditionFlow("f3", "fork", "left", "1 = 1"),
                ConditionFlow("f4", "fork", "right", "2 = 2"),
                Flow("f5", "left", "join"),
                Flow("f6", "right", "join"),
                Flow("f7", "join", "sum"),
                Flow("f8", "sum", "end")));

        var session = await RunAsync(schema);

        Assert.True(
            string.Equals("success", session.Status, StringComparison.OrdinalIgnoreCase),
            JsonSerializer.Serialize(new { session.Error, Trace = session.Trace.Select(frame => new { frame.ObjectId, frame.Status, frame.Output, frame.VariablesSnapshot }) }, JsonOptions));
        Assert.Equal("12", session.Output?.GetRawText());
        Assert.Contains(session.Trace, frame => frame.ObjectId == "left" && frame.Status == "success");
        Assert.Contains(session.Trace, frame => frame.ObjectId == "right" && frame.Status == "success");
        Assert.Contains(session.Trace, frame => frame.ObjectId == "join" && frame.Status == "success");
    }

    [Fact]
    public async Task Run_ObjectTypeDecision_Selects_Runtime_Entity_Branch()
    {
        var schema = Schema(
            Objects(
                Start(),
                Action("create-student", "createObject", new
                {
                    entityType = "Sales.Student",
                    objectId = "student-1",
                    outputVariableName = "student",
                    value = new { id = "student-1", entityType = "Sales.Student" }
                }),
                Action("cast-member", "cast", new { sourceVariable = "student", targetEntity = "Sales.Member", outputVariable = "member" }),
                new { id = "type", kind = "inheritanceSplit", caption = "Type?", inputObjectVariableName = "member", generalizedEntityQualifiedName = "Sales.Member" },
                Action("student-score", "createVariable", new { variableName = "score", dataType = new { kind = "integer" }, initialValue = "30" }),
                Action("fallback-score", "createVariable", new { variableName = "score", dataType = new { kind = "integer" }, initialValue = "-99" }),
                End(returnValue: "$score")),
            Flows(
                Flow("f1", "start", "create-student"),
                Flow("f2", "create-student", "cast-member"),
                Flow("f3", "cast-member", "type"),
                ObjectTypeFlow("f4", "type", "student-score", "Sales.Student"),
                ObjectTypeFlow("f5", "type", "fallback-score", "fallback"),
                Flow("f6", "student-score", "end"),
                Flow("f7", "fallback-score", "end")));

        var session = await RunAsync(schema, metadata: Catalog());

        Assert.Equal("success", session.Status);
        Assert.Equal("30", session.Output?.GetRawText());
        Assert.Contains(session.Trace, frame => frame.ObjectId == "type" && frame.Status == "success");
        Assert.DoesNotContain(session.Trace, frame => frame.ObjectId == "fallback-score" && frame.Status == "success");
    }

    [Fact]
    public async Task Run_ListOperation_Unknown_Operation_Fails_Instead_Of_Returning_Count()
    {
        var schema = Schema(
            Objects(
                Start(),
                Action("list", "createList", new { outputListVariableName = "items", items = new[] { 1, 2, 3 } }),
                Action("bad-op", "listOperation", new { leftListVariableName = "items", operation = "notAnOperation", outputVariableName = "result" }),
                End(returnValue: "$result")),
            Flows(
                Flow("f1", "start", "list"),
                Flow("f2", "list", "bad-op"),
                Flow("f3", "bad-op", "end")));

        var session = await RunAsync(schema);

        Assert.Equal("failed", session.Status);
        Assert.Equal(RuntimeErrorCode.RuntimeUnsupportedAction, session.Error?.Code);
    }

    [Fact]
    public async Task Run_All_Screenshot_Node_Families_Returns_120()
    {
        const string loopCollectionId = "all-node-loop-body";
        var schema = MicroflowDesignSchemaTestFactory.Schema(
            Objects(
                Start(),
                Action("create-total", "createVariable", new { variableName = "total", dataType = new { kind = "integer" }, initialValue = "0" }),
                Action("create-list-score", "createVariable", new { variableName = "listScore", dataType = new { kind = "integer" }, initialValue = "0" }),
                Action("create-loop-score", "createVariable", new { variableName = "loopScore", dataType = new { kind = "integer" }, initialValue = "0" }),
                Action("create-object-score", "createVariable", new { variableName = "objectScore", dataType = new { kind = "integer" }, initialValue = "0" }),
                Action("create-gateway-score", "createVariable", new { variableName = "gatewayScore", dataType = new { kind = "integer" }, initialValue = "0" }),
                Action("create-work-list", "createList", new { outputListVariableName = "workNumbers", items = Array.Empty<int>() }),
                Action("change-work-list", "changeList", new { targetListVariableName = "workNumbers", operation = "addRange", items = new[] { 6, 1, 3, 2, 5, 4 } }),
                Action("sort-work-list", "sortList", new { sourceListVariableName = "workNumbers", outputVariableName = "sortedNumbers", direction = "asc" }),
                Action("filter-work-list", "filterList", new { sourceListVariableName = "sortedNumbers", outputVariableName = "filteredNumbers", itemVariableName = "item", conditionExpression = "$item > 2", itemType = new { kind = "integer" } }),
                Action("contains-five", "listOperation", new { leftListVariableName = "filteredNumbers", operation = "contains", itemExpression = "5", outputVariableName = "hasFive" }),
                Action("aggregate-filtered", "aggregateList", new { sourceListVariableName = "filteredNumbers", aggregateFunction = "sum", outputVariableName = "filteredSum", resultType = new { kind = "integer" } }),
                new { id = "has-five-decision", kind = "exclusiveSplit", caption = "Has five?", splitCondition = new { expression = "$hasFive = true", resultType = "boolean" } },
                Action("set-list-score", "changeVariable", new { targetVariableName = "listScore", newValueExpression = "$filteredSum" }),
                Action("fallback-list-score", "changeVariable", new { targetVariableName = "listScore", newValueExpression = "-99" }),
                new { id = "list-merge", kind = "exclusiveMerge", caption = "List Merge" },
                new
                {
                    id = "loop-numbers",
                    kind = "loopedActivity",
                    caption = "Loop numbers",
                    loopSource = new
                    {
                        kind = "iterableList",
                        listVariableName = "numbers",
                        iteratorVariableName = "currentNumber"
                    }
                },
                new
                {
                    id = "break-at-four",
                    kind = "exclusiveSplit",
                    caption = "currentNumber = 4",
                    collectionId = loopCollectionId,
                    parentObjectId = "loop-numbers",
                    splitCondition = new { expression = "$currentNumber = 4", resultType = "boolean" }
                },
                new { id = "break-loop", kind = "breakEvent", caption = "Break", collectionId = loopCollectionId, parentObjectId = "loop-numbers" },
                new
                {
                    id = "continue-at-two",
                    kind = "exclusiveSplit",
                    caption = "currentNumber = 2",
                    collectionId = loopCollectionId,
                    parentObjectId = "loop-numbers",
                    splitCondition = new { expression = "$currentNumber = 2", resultType = "boolean" }
                },
                new { id = "continue-loop", kind = "continueEvent", caption = "Continue", collectionId = loopCollectionId, parentObjectId = "loop-numbers" },
                ActionIn("add-loop-score", "changeVariable", loopCollectionId, "loop-numbers", new { targetVariableName = "loopScore", newValueExpression = "$loopScore + $currentNumber" }),
                new { id = "loop-body-end", kind = "endEvent", caption = "Body return", collectionId = loopCollectionId, parentObjectId = "loop-numbers" },
                Action("create-student", "createObject", new
                {
                    entityType = "Sales.Student",
                    objectId = "student-1",
                    outputVariableName = "student",
                    value = new { id = "student-1", entityType = "Sales.Student", points = 30 }
                }),
                Action("change-student", "changeMembers", new
                {
                    entityType = "Sales.Student",
                    objectId = "student-1",
                    value = new { id = "student-1", entityType = "Sales.Student", points = 31 }
                }),
                Action("cast-member", "cast", new { sourceVariable = "student", targetEntity = "Sales.Member", outputVariable = "member" }),
                new { id = "student-type", kind = "inheritanceSplit", caption = "Student type?", inputObjectVariableName = "member", generalizedEntityQualifiedName = "Sales.Member" },
                Action("object-type-score", "changeVariable", new { targetVariableName = "objectScore", newValueExpression = "$objectScore + 30" }),
                Action("object-type-fallback", "changeVariable", new { targetVariableName = "objectScore", newValueExpression = "$objectScore - 99" }),
                new { id = "object-type-merge", kind = "exclusiveMerge", caption = "Object Type Merge" },
                Action("commit-student", "commit", new { entityType = "Sales.Student", objectId = "student-1" }),
                Action("retrieve-student", "retrieve", new { entityType = "Sales.Student", limit = 10 }),
                Action("retrieve-score", "changeVariable", new { targetVariableName = "objectScore", newValueExpression = "$objectScore + 20" }),
                Action("create-temp", "createObject", new
                {
                    entityType = "Sales.Student",
                    objectId = "temp-rollback",
                    value = new { id = "temp-rollback", entityType = "Sales.Student" }
                }),
                Action("rollback-temp", "rollback", new { entityType = "Sales.Student", objectId = "temp-rollback" }),
                Action("rollback-score", "changeVariable", new { targetVariableName = "objectScore", newValueExpression = "$objectScore + 10" }),
                Action("delete-student", "delete", new { entityType = "Sales.Student", objectId = "student-1" }),
                Action("delete-score", "changeVariable", new { targetVariableName = "objectScore", newValueExpression = "$objectScore + 8" }),
                new { id = "parallel-fork", kind = "parallelGateway", caption = "Parallel Fork" },
                Action("parallel-a", "createVariable", new { variableName = "parallelA", dataType = new { kind = "integer" }, initialValue = "7" }),
                Action("parallel-b", "createVariable", new { variableName = "parallelB", dataType = new { kind = "integer" }, initialValue = "11" }),
                new { id = "parallel-join", kind = "parallelGateway", caption = "Parallel Join" },
                Action("parallel-score", "changeVariable", new { targetVariableName = "gatewayScore", newValueExpression = "$gatewayScore + $parallelA + $parallelB" }),
                new { id = "inclusive-fork", kind = "inclusiveGateway", caption = "Inclusive Fork" },
                Action("inclusive-a", "createVariable", new { variableName = "inclusiveA", dataType = new { kind = "integer" }, initialValue = "5" }),
                Action("inclusive-b", "createVariable", new { variableName = "inclusiveB", dataType = new { kind = "integer" }, initialValue = "7" }),
                new { id = "inclusive-join", kind = "inclusiveGateway", caption = "Inclusive Join" },
                Action("inclusive-score", "changeVariable", new { targetVariableName = "gatewayScore", newValueExpression = "$gatewayScore + $inclusiveA + $inclusiveB" }),
                Action("final-total", "changeVariable", new { targetVariableName = "total", newValueExpression = "$listScore + $loopScore + $objectScore + $gatewayScore" }),
                End(returnValue: "$total")),
            Flows(
                Flow("f-start-total", "start", "create-total"),
                Flow("f-total-list-score", "create-total", "create-list-score"),
                Flow("f-list-loop-score", "create-list-score", "create-loop-score"),
                Flow("f-loop-object-score", "create-loop-score", "create-object-score"),
                Flow("f-object-gateway-score", "create-object-score", "create-gateway-score"),
                Flow("f-gateway-create-list", "create-gateway-score", "create-work-list"),
                Flow("f-create-change-list", "create-work-list", "change-work-list"),
                Flow("f-change-sort-list", "change-work-list", "sort-work-list"),
                Flow("f-sort-filter-list", "sort-work-list", "filter-work-list"),
                Flow("f-filter-contains", "filter-work-list", "contains-five"),
                Flow("f-contains-aggregate", "contains-five", "aggregate-filtered"),
                Flow("f-aggregate-decision", "aggregate-filtered", "has-five-decision"),
                BooleanFlow("f-has-five-true", "has-five-decision", "set-list-score", true),
                BooleanFlow("f-has-five-false", "has-five-decision", "fallback-list-score", false),
                Flow("f-set-list-merge", "set-list-score", "list-merge"),
                Flow("f-fallback-list-merge", "fallback-list-score", "list-merge"),
                Flow("f-list-merge-loop", "list-merge", "loop-numbers"),
                Flow("f-loop-create-student", "loop-numbers", "create-student"),
                Flow("f-loop-body-break-decision", "loop-numbers", "break-at-four", loopCollectionId),
                BooleanFlow("f-break-true", "break-at-four", "break-loop", true, loopCollectionId),
                BooleanFlow("f-break-false", "break-at-four", "continue-at-two", false, loopCollectionId),
                BooleanFlow("f-continue-true", "continue-at-two", "continue-loop", true, loopCollectionId),
                BooleanFlow("f-continue-false", "continue-at-two", "add-loop-score", false, loopCollectionId),
                Flow("f-add-loop-body-end", "add-loop-score", "loop-body-end", loopCollectionId),
                Flow("f-create-change-student", "create-student", "change-student"),
                Flow("f-change-cast-member", "change-student", "cast-member"),
                Flow("f-cast-type", "cast-member", "student-type"),
                ObjectTypeFlow("f-student-type", "student-type", "object-type-score", "Sales.Student"),
                ObjectTypeFlow("f-student-fallback", "student-type", "object-type-fallback", "fallback"),
                Flow("f-type-score-merge", "object-type-score", "object-type-merge"),
                Flow("f-type-fallback-merge", "object-type-fallback", "object-type-merge"),
                Flow("f-type-merge-commit", "object-type-merge", "commit-student"),
                Flow("f-commit-retrieve", "commit-student", "retrieve-student"),
                Flow("f-retrieve-score", "retrieve-student", "retrieve-score"),
                Flow("f-retrieve-score-create-temp", "retrieve-score", "create-temp"),
                Flow("f-create-temp-rollback", "create-temp", "rollback-temp"),
                Flow("f-rollback-score", "rollback-temp", "rollback-score"),
                Flow("f-rollback-score-delete", "rollback-score", "delete-student"),
                Flow("f-delete-score", "delete-student", "delete-score"),
                Flow("f-delete-score-parallel", "delete-score", "parallel-fork"),
                Flow("f-parallel-a", "parallel-fork", "parallel-a"),
                Flow("f-parallel-b", "parallel-fork", "parallel-b"),
                Flow("f-parallel-a-join", "parallel-a", "parallel-join"),
                Flow("f-parallel-b-join", "parallel-b", "parallel-join"),
                Flow("f-parallel-score", "parallel-join", "parallel-score"),
                Flow("f-parallel-inclusive", "parallel-score", "inclusive-fork"),
                ConditionFlow("f-inclusive-a", "inclusive-fork", "inclusive-a", "$hasFive = true"),
                ConditionFlow("f-inclusive-b", "inclusive-fork", "inclusive-b", "$listScore = 18"),
                ObjectTypeFlow("f-inclusive-fallback", "inclusive-fork", "final-total", "fallback"),
                Flow("f-inclusive-a-join", "inclusive-a", "inclusive-join"),
                Flow("f-inclusive-b-join", "inclusive-b", "inclusive-join"),
                Flow("f-inclusive-score", "inclusive-join", "inclusive-score"),
                Flow("f-inclusive-score-final", "inclusive-score", "final-total"),
                Flow("f-final-end", "final-total", "end")),
            Parameters(new { id = "numbers", name = "numbers", dataType = "Integer", type = new { kind = "list", itemType = new { kind = "integer" } }, required = true }),
            "mf-all-screenshot-node-families",
            JsonOptions);

        var session = await RunAsync(schema, new Dictionary<string, object?> { ["numbers"] = new[] { 1, 2, 3, 4, 5, 6 } }, Catalog());

        Assert.True(
            string.Equals("success", session.Status, StringComparison.OrdinalIgnoreCase),
            JsonSerializer.Serialize(new { session.Error, Trace = session.Trace.Select(frame => new { frame.ObjectId, frame.Status, frame.Output, frame.VariablesSnapshot }) }, JsonOptions));
        Assert.Equal("120", session.Output?.GetRawText());
        foreach (var objectId in new[]
        {
            "has-five-decision", "list-merge", "loop-numbers", "continue-loop", "break-loop",
            "create-student", "change-student", "cast-member", "student-type", "commit-student",
            "retrieve-student", "rollback-temp", "delete-student", "parallel-fork", "parallel-join",
            "inclusive-fork", "inclusive-join", "aggregate-filtered", "contains-five", "sort-work-list", "filter-work-list"
        })
        {
            Assert.Contains(session.Trace, frame => frame.ObjectId == objectId && frame.Status == "success");
        }
    }

    [Fact]
    public async Task Run_AnnotationNode_DoesNotBlockSuccessfulFlow()
    {
        var schema = Schema(
            Objects(
                Start(),
                new { id = "note", kind = "annotation", caption = "Comment", text = "design note" },
                End()),
            Flows(
                Flow("f1", "start", "note"),
                Flow("f2", "note", "end")));

        var session = await RunAsync(schema);

        Assert.Equal("success", session.Status);
        Assert.Contains(session.Trace, frame => frame.ObjectId == "note" && frame.Status == "success");
    }

    [Fact]
    public async Task Run_PendingClientCommand_Continues_And_Emits_RuntimeCommand()
    {
        // showPage 注册为 RuntimeCommand，executor 返回 PendingClientCommand。
        // 当前 runtime 会把命令写入 trace/output，然后继续 normal flow，
        // 由前端在同一 run session 中消费这些命令。
        var schema = Schema(
            Objects(
                Start(),
                Action("show", "showPage", new { pageId = "p1" }),
                End()),
            Flows(
                Flow("f1", "start", "show"),
                Flow("f2", "show", "end")));

        var session = await RunAsync(schema);

        Assert.Equal("success", session.Status);
        var showFrame = session.Trace.Single(frame => frame.ObjectId == "show");
        Assert.Equal("success", showFrame.Status);
        Assert.Contains("runtimeCommands", showFrame.Output?.GetRawText() ?? string.Empty);
    }

    [Theory]
    [InlineData("counter", "{ \"metricName\": \"orders.total\", \"valueExpression\": { \"raw\": \"1\" } }")]
    [InlineData("incrementCounter", "{ \"metricName\": \"orders.increment\" }")]
    [InlineData("gauge", "{ \"metricName\": \"orders.gauge\", \"valueExpression\": { \"raw\": \"2\" } }")]
    public async Task Run_MetricsActions_Succeed(string actionKind, string rawAction)
    {
        var actionJson = JsonDocument.Parse(rawAction).RootElement.Clone();
        var schema = Schema(
            Objects(
                Start(),
                Action("metric", actionKind, actionJson),
                End()),
            Flows(
                Flow("f1", "start", "metric"),
                Flow("f2", "metric", "end")));

        var session = await RunAsync(schema);

        Assert.Equal("success", session.Status);
        Assert.Contains(session.Trace, frame => frame.ObjectId == "metric" && frame.Status == "success");
    }

    [Fact]
    public async Task Run_ThrowExceptionWithErrorHandlerEdge_RoutesToErrorHandlerBranch()
    {
        // throwException action 设置 ShouldEnterErrorHandler=true。
        // 现在引擎在该标志 + isErrorHandler 边存在时跳 error handler 路径而不是 fail run。
        var schema = Schema(
            Objects(
                Start(),
                Action("thrower", "throwException", new
                {
                    code = "BIZ_FAIL",
                    message = "primary path failed"
                }),
                new { id = "handler", kind = "exclusiveMerge", caption = "Handler" },
                End(returnValue: "$latestError")),
            Flows(
                Flow("f1", "start", "thrower"),
                ErrorFlow("f2-err", "thrower", "handler"),
                Flow("f3", "handler", "end")));

        var session = await RunAsync(schema);

        // 错误分支兜住后流程仍然成功结束（但首节点 trace 标 failed）。
        Assert.Equal("success", session.Status);
        Assert.Contains(session.Trace, frame => frame.ObjectId == "thrower" && frame.Status == "failed");
        Assert.Contains(session.Trace, frame => frame.ObjectId == "handler");
    }

    private static async Task<MicroflowRunSessionDto> RunAsync(
        JsonElement schema,
        IReadOnlyDictionary<string, object?>? input = null,
        MicroflowMetadataCatalogDto? metadata = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAtlasApplicationMicroflows();
        services.AddSingleton(AllowingEntityAccess());
        services.AddSingleton<IMicroflowClock>(new FixedClock());
        services.AddSingleton<IMicroflowRequestContextAccessor>(new InMemoryRequestContextAccessor());
        services.AddSingleton(Substitute.For<IMicroflowResourceRepository>());
        services.AddSingleton(Substitute.For<IMicroflowSchemaSnapshotRepository>());
        services.AddSingleton(Substitute.For<IMicroflowVersionRepository>());
        services.AddSingleton(Substitute.For<IMicroflowPublishSnapshotRepository>());
        services.AddSingleton(Substitute.For<IMicroflowFolderRepository>());
        services.AddSingleton(Substitute.For<IMicroflowReferenceRepository>());
        services.AddSingleton(Substitute.For<IMicroflowRunRepository>());
        services.AddSingleton(Substitute.For<IMicroflowMetadataCacheRepository>());
        services.AddSingleton(Substitute.For<IMicroflowStorageTransaction>());
        services.AddSingleton(Substitute.For<IMicroflowStorageDiagnosticsService>());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IMicroflowRuntimeEngine>();
        var jsonInput = (input ?? new Dictionary<string, object?>())
            .ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value, JsonOptions), StringComparer.Ordinal);

        return await engine.RunAsync(
            new MicroflowExecutionRequest
            {
                ResourceId = "mf-p04",
                SchemaId = "schema-p04",
                Version = "1",
                Schema = schema,
                Input = jsonInput,
                Options = new MicroflowTestRunOptionsDto(),
                Metadata = metadata,
                RequestContext = new MicroflowRequestContext { TraceId = Guid.NewGuid().ToString("N") },
                MaxCallDepth = 10
            },
            CancellationToken.None);
    }

    private static JsonElement Schema(IReadOnlyList<object> objects, IReadOnlyList<object> flows)
        => MicroflowDesignSchemaTestFactory.Schema(objects, flows, Array.Empty<object>(), "mf-p04", JsonOptions);

    private static IReadOnlyList<object> Objects(params object[] objects) => objects;

    private static IReadOnlyList<object> Flows(params object[] flows) => flows;

    private static IReadOnlyList<object> Parameters(params object[] parameters) => parameters;

    private static object Start(string id = "start") => new { id, kind = "startEvent", caption = "Start" };

    private static object End(string id = "end", string? returnValue = null)
        => returnValue is null
            ? new { id, kind = "endEvent", caption = "End" }
            : new { id, kind = "endEvent", caption = "End", returnValue };

    private static object Action(string id, string actionKind, object action)
        => new { id, kind = "actionActivity", caption = id, action = MergeAction(id, actionKind, action) };

    private static object ActionIn(string id, string actionKind, string collectionId, string parentObjectId, object action)
        => new { id, kind = "actionActivity", caption = id, collectionId, parentObjectId, action = MergeAction(id, actionKind, action) };

    private static object MergeAction(string id, string actionKind, object action)
    {
        var json = JsonSerializer.SerializeToElement(action, JsonOptions);
        var values = new Dictionary<string, object?>
        {
            ["id"] = $"{id}-action",
            ["kind"] = actionKind,
            ["officialType"] = $"Microflows${actionKind}"
        };
        foreach (var property in json.EnumerateObject())
        {
            values[property.Name] = property.Value.Clone();
        }

        return values;
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

    private static object Flow(string id, string source, string target, string collectionId)
        => new
        {
            id,
            kind = "sequence",
            originObjectId = source,
            destinationObjectId = target,
            collectionId,
            caseValues = Array.Empty<object>(),
            isErrorHandler = false,
            editor = new { edgeKind = "sequence" }
        };

    private static object BooleanFlow(string id, string source, string target, bool value, string collectionId)
        => new
        {
            id,
            kind = "sequence",
            originObjectId = source,
            destinationObjectId = target,
            collectionId,
            caseValues = new[] { new { kind = "boolean", value, persistedValue = value ? "true" : "false" } },
            isErrorHandler = false,
            editor = new { edgeKind = "decisionCondition" }
        };

    private static object BooleanFlow(string id, string source, string target, bool value)
        => new
        {
            id,
            kind = "sequence",
            originObjectId = source,
            destinationObjectId = target,
            caseValues = new[] { new { kind = "boolean", value, persistedValue = value ? "true" : "false" } },
            isErrorHandler = false,
            editor = new { edgeKind = "decisionCondition" }
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

    private static object ObjectTypeFlow(string id, string source, string target, string entityOrCase)
        => new
        {
            id,
            kind = "sequence",
            originObjectId = source,
            destinationObjectId = target,
            caseValues = new[] { new { kind = "objectType", value = entityOrCase, entityQualifiedName = entityOrCase, persistedValue = entityOrCase } },
            isErrorHandler = false,
            editor = new { edgeKind = "objectTypeCondition" }
        };

    private static object ErrorFlow(string id, string source, string target)
        => new
        {
            id,
            kind = "sequence",
            originObjectId = source,
            destinationObjectId = target,
            caseValues = Array.Empty<object>(),
            isErrorHandler = true,
            editor = new { edgeKind = "errorHandler" }
        };

    private sealed class FixedClock : IMicroflowClock
    {
        private DateTimeOffset _now = new(2026, 4, 28, 0, 0, 0, TimeSpan.Zero);

        public DateTimeOffset UtcNow
        {
            get
            {
                _now = _now.AddMilliseconds(1);
                return _now;
            }
        }
    }

    private sealed class InMemoryRequestContextAccessor : IMicroflowRequestContextAccessor
    {
        public MicroflowRequestContext Current { get; set; } = new()
        {
            TraceId = Guid.NewGuid().ToString("N"),
            UserId = "system",
            UserName = "system",
            WorkspaceId = "ws-p04",
            TenantId = "tenant-p04"
        };
    }

    private static MicroflowMetadataCatalogDto Catalog()
        => new()
        {
            Entities =
            [
                new MetadataEntityDto
                {
                    QualifiedName = "Sales.Member",
                    Generalization = null,
                    Specializations = ["Sales.Student"]
                },
                new MetadataEntityDto
                {
                    QualifiedName = "Sales.Student",
                    Generalization = "Sales.Member",
                    Specializations = []
                }
            ]
        };

    private static IMicroflowEntityAccessService AllowingEntityAccess()
    {
        var access = Substitute.For<IMicroflowEntityAccessService>();
        MicroflowEntityAccessDecision Allow(MicroflowResolvedEntity entity, string operation)
            => new()
            {
                Allowed = true,
                Operation = operation,
                EntityQualifiedName = entity.QualifiedName
            };

        access.CanReadAsync(Arg.Any<MicroflowRuntimeSecurityContext>(), Arg.Any<MicroflowResolvedEntity>(), Arg.Any<CancellationToken>())
            .Returns(call => Allow(call.ArgAt<MicroflowResolvedEntity>(1), "read"));
        access.CanCreateAsync(Arg.Any<MicroflowRuntimeSecurityContext>(), Arg.Any<MicroflowResolvedEntity>(), Arg.Any<CancellationToken>())
            .Returns(call => Allow(call.ArgAt<MicroflowResolvedEntity>(1), "create"));
        access.CanUpdateAsync(Arg.Any<MicroflowRuntimeSecurityContext>(), Arg.Any<MicroflowResolvedEntity>(), Arg.Any<CancellationToken>())
            .Returns(call => Allow(call.ArgAt<MicroflowResolvedEntity>(1), "update"));
        access.CanDeleteAsync(Arg.Any<MicroflowRuntimeSecurityContext>(), Arg.Any<MicroflowResolvedEntity>(), Arg.Any<CancellationToken>())
            .Returns(call => Allow(call.ArgAt<MicroflowResolvedEntity>(1), "delete"));
        access.CanExecuteMicroflowAsync(Arg.Any<MicroflowRuntimeSecurityContext>(), Arg.Any<MicroflowResolvedMicroflowRef>(), Arg.Any<CancellationToken>())
            .Returns(call => new MicroflowEntityAccessDecision
            {
                Allowed = true,
                Operation = "execute",
                EntityQualifiedName = call.ArgAt<MicroflowResolvedMicroflowRef>(1).QualifiedName
            });
        return access;
    }
}
