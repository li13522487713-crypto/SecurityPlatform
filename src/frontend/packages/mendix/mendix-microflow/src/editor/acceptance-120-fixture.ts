import {
  createMicroflowWorkflowEdge,
  createMicroflowWorkflowNode,
} from "../flowgram/flowgram-native-schema";
import type { FlowGramMicroflowNodeData } from "../flowgram/FlowGramMicroflowTypes";
import type {
  MicroflowDesignSchema,
  MicroflowWorkflowEdgeJSON,
  MicroflowWorkflowNodeJSON,
} from "../schema/types";

export function buildAcceptance120Schema(schema: MicroflowDesignSchema): MicroflowDesignSchema {
  const intType = { kind: "integer" as const };
  const intListType = { kind: "list" as const, itemType: intType };
  const action = (id: string, kind: string, config: Record<string, unknown>) => ({
    id: `action-${id}`,
    kind,
    officialType: `Microflows$${kind}`,
    ...config,
  });
  const node = (
    id: string,
    objectKind: FlowGramMicroflowNodeData["objectKind"],
    title: string,
    x: number,
    y: number,
    data: Record<string, unknown> = {},
  ): MicroflowWorkflowNodeJSON => createMicroflowWorkflowNode({
    id,
    objectKind,
    position: { x, y },
    title,
    officialType: `Microflows$${objectKind}`,
    data: {
      title,
      ...data,
    },
  }) as MicroflowWorkflowNodeJSON;
  const actionNode = (id: string, title: string, x: number, y: number, actionKind: string, config: Record<string, unknown>) =>
    node(id, "actionActivity", title, x, y, {
      actionKind,
      action: action(id, actionKind, config),
    });
  const loopBody = "loop-numbers-body";
  const loopChild = (id: string, objectKind: FlowGramMicroflowNodeData["objectKind"], title: string, x: number, y: number, data: Record<string, unknown> = {}) =>
    node(id, objectKind, title, x, y, { collectionId: loopBody, parentObjectId: "loop-numbers", ...data });
  const loopAction = (id: string, title: string, x: number, y: number, actionKind: string, config: Record<string, unknown>) =>
    loopChild(id, "actionActivity", title, x, y, {
      actionKind,
      action: action(id, actionKind, config),
    });
  const edge = (id: string, source: string, target: string, data: Record<string, unknown> = {}) => {
    const created = createMicroflowWorkflowEdge({
      id,
      sourceNodeID: source,
      targetNodeID: target,
      data,
    }) as MicroflowWorkflowEdgeJSON & Record<string, unknown>;
    if (typeof data.edgeKind === "string") {
      created.edgeKind = data.edgeKind;
    }
    if (Array.isArray(data.caseValues)) {
      created.caseValues = data.caseValues;
    }
    if (typeof data.collectionId === "string") {
      created.collectionId = data.collectionId;
    }
    return created as MicroflowWorkflowEdgeJSON;
  };
  const loopEdge = (id: string, source: string, target: string, data: Record<string, unknown> = {}) =>
    edge(id, source, target, { collectionId: loopBody, ...data });
  const boolCase = (value: boolean) => [{ kind: "boolean", value, persistedValue: value ? "true" : "false" }];
  const exprCase = (expression: string) => [{ kind: "expression", condition: expression, expression }];
  const objectCase = (value: string) => [{ kind: "objectType", value, entityQualifiedName: value, persistedValue: value }];
  const nodes: MicroflowWorkflowNodeJSON[] = [
    node("start", "startEvent", "Start", 80, 240),
    actionNode("create-total", "创建变量", 280, 240, "createVariable", { variableName: "total", dataType: intType, initialValue: "0" }),
    actionNode("create-list-score", "创建变量", 480, 240, "createVariable", { variableName: "listScore", dataType: intType, initialValue: "0" }),
    actionNode("create-loop-score", "创建变量", 680, 240, "createVariable", { variableName: "loopScore", dataType: intType, initialValue: "0" }),
    actionNode("create-object-score", "创建变量", 880, 240, "createVariable", { variableName: "objectScore", dataType: intType, initialValue: "0" }),
    actionNode("create-gateway-score", "创建变量", 1080, 240, "createVariable", { variableName: "gatewayScore", dataType: intType, initialValue: "0" }),
    actionNode("create-list", "创建列表", 1280, 240, "createList", { outputListVariableName: "workList", elementType: intType, items: [] }),
    actionNode("change-list", "修改列表", 1480, 240, "changeList", { targetListVariableName: "workList", operation: "addRange", items: [6, 1, 3, 2, 5, 4] }),
    actionNode("sort-list", "排序列表", 1680, 240, "sortList", { sourceListVariableName: "workList", outputVariableName: "sortedNumbers", direction: "asc" }),
    actionNode("filter-list", "过滤列表", 1880, 240, "filterList", { sourceListVariableName: "sortedNumbers", outputVariableName: "positiveNumbers", itemVariableName: "item", conditionExpression: "$item > 2", itemType: intType }),
    actionNode("aggregate-list", "列表聚合", 2080, 240, "aggregateList", { sourceListVariableName: "positiveNumbers", aggregateFunction: "sum", outputVariableName: "filteredSum", resultType: intType }),
    actionNode("list-operation", "列表操作", 2280, 240, "listOperation", { leftListVariableName: "positiveNumbers", operation: "contains", itemExpression: "5", outputVariableName: "hasFive" }),
    node("decision", "exclusiveSplit", "决策", 2480, 240, { splitCondition: { expression: "$hasFive = true", resultType: "boolean" } }),
    actionNode("set-list-score", "决策 True", 2580, 160, "changeVariable", { targetVariableName: "listScore", newValueExpression: "$filteredSum" }),
    actionNode("fallback-list-score", "决策 False", 2580, 320, "changeVariable", { targetVariableName: "listScore", newValueExpression: "-99" }),
    node("merge", "exclusiveMerge", "合并", 2680, 240),
    node("loop-note", "annotation", "Loop: Process Batch", 2740, 92, {
      text: "迭代对象: $numbers\nContinue: $currentNumber = 2\nBreak: $currentNumber = 4\n默认路径: 累加 loopScore",
    }),
    node("loop-numbers", "loopedActivity", "循环", 2880, 240, { bodyCollectionId: loopBody, loopSource: { kind: "iterableList", listVariableName: "numbers", iteratorVariableName: "currentNumber", iteratorVariableDataType: intType } }),
    loopChild("continue-check", "exclusiveSplit", "currentNumber = 2", 2880, 380, { splitCondition: { expression: "$currentNumber = 2", resultType: "boolean" } }),
    loopChild("continue-event", "continueEvent", "继续事件", 3080, 380, { targetLoopObjectId: "loop-numbers" }),
    loopChild("break-check", "exclusiveSplit", "currentNumber = 4", 2880, 520, { splitCondition: { expression: "$currentNumber = 4", resultType: "boolean" } }),
    loopAction("loop-touch", "修改变量", 3080, 520, "changeVariable", { targetVariableName: "loopScore", newValueExpression: "$loopScore + $currentNumber" }),
    node("break-event", "breakEvent", "中断事件", 2880, 700, { targetLoopObjectId: "loop-numbers" }),
    actionNode("create-object", "创建对象", 3080, 240, "createObject", { entityQualifiedName: "Sales.Student", entityType: "Sales.Student", objectId: "student-1", outputVariableName: "student", memberChanges: [{ memberQualifiedName: "Sales.Student.Grade", valueExpression: "'A'" }], value: { id: "student-1", entityType: "Sales.Student", grade: "A" } }),
    actionNode("change-object", "修改对象", 3280, 240, "changeMembers", { changeVariableName: "student", entityQualifiedName: "Sales.Student", entityType: "Sales.Student", objectId: "student-1", memberChanges: [{ memberQualifiedName: "Sales.Student.Grade", valueExpression: "'B'" }], value: { id: "student-1", entityType: "Sales.Student", grade: "B" } }),
    actionNode("cast-object", "转换对象", 3480, 240, "cast", { sourceVariable: "student", sourceObjectVariableName: "student", targetVariable: "member", outputVariable: "member", outputVariableName: "member", targetEntity: "Sales.Member", targetEntityQualifiedName: "Sales.Member" }),
    node("object-type", "inheritanceSplit", "对象类型决策", 3680, 240, { inputObjectVariableName: "member", generalizedEntityQualifiedName: "Sales.Member" }),
    actionNode("object-type-student-touch", "对象类型 Student", 3780, 160, "changeVariable", { targetVariableName: "objectScore", newValueExpression: "$objectScore + 30" }),
    actionNode("object-type-fallback-touch", "对象类型 Fallback", 3780, 320, "changeVariable", { targetVariableName: "objectScore", newValueExpression: "$objectScore - 99" }),
    node("object-type-merge", "exclusiveMerge", "合并", 3880, 240),
    actionNode("commit-object", "提交对象", 4080, 240, "commit", { objectOrListVariableName: "student", entityQualifiedName: "Sales.Student", entityType: "Sales.Student", objectId: "student-1" }),
    actionNode("retrieve-object", "检索对象", 4280, 240, "retrieve", { outputVariableName: "students", retrieveSource: { kind: "database", entityQualifiedName: "Sales.Student", range: { kind: "list" } }, entityType: "Sales.Student", limit: 10 }),
    actionNode("retrieve-score", "修改变量", 4480, 240, "changeVariable", { targetVariableName: "objectScore", newValueExpression: "$objectScore + 20" }),
    actionNode("create-temp", "创建对象", 4680, 240, "createObject", { entityQualifiedName: "Sales.Student", entityType: "Sales.Student", objectId: "temp-rollback", outputVariableName: "tempStudent", value: { id: "temp-rollback", entityType: "Sales.Student" } }),
    actionNode("rollback-object", "回滚对象", 4880, 240, "rollback", { objectOrListVariableName: "tempStudent", entityQualifiedName: "Sales.Student", entityType: "Sales.Student", objectId: "temp-rollback" }),
    actionNode("rollback-score", "修改变量", 5080, 240, "changeVariable", { targetVariableName: "objectScore", newValueExpression: "$objectScore + 10" }),
    actionNode("delete-object", "删除对象", 5280, 240, "delete", { objectOrListVariableName: "student", entityQualifiedName: "Sales.Student", entityType: "Sales.Student", objectId: "student-1" }),
    actionNode("delete-score", "修改变量", 5480, 240, "changeVariable", { targetVariableName: "objectScore", newValueExpression: "$objectScore + 8" }),
    node("parallel-fork", "parallelGateway", "并行网关", 5680, 240),
    actionNode("parallel-a", "并行分支 A", 5880, 160, "createVariable", { variableName: "parallelA", dataType: intType, initialValue: "7" }),
    actionNode("parallel-b", "并行分支 B", 5880, 320, "createVariable", { variableName: "parallelB", dataType: intType, initialValue: "11" }),
    node("parallel-join", "parallelGateway", "并行合并", 6080, 240),
    actionNode("parallel-score", "修改变量", 6280, 240, "changeVariable", { targetVariableName: "gatewayScore", newValueExpression: "$gatewayScore + $parallelA + $parallelB" }),
    node("inclusive-fork", "inclusiveGateway", "包含网关", 6480, 240),
    actionNode("inclusive-a", "包含分支 A", 6680, 160, "createVariable", { variableName: "inclusiveA", dataType: intType, initialValue: "5" }),
    actionNode("inclusive-b", "包含分支 B", 6680, 320, "createVariable", { variableName: "inclusiveB", dataType: intType, initialValue: "7" }),
    node("inclusive-join", "inclusiveGateway", "包含合并", 6880, 240),
    actionNode("inclusive-score", "修改变量", 7080, 240, "changeVariable", { targetVariableName: "gatewayScore", newValueExpression: "$gatewayScore + $inclusiveA + $inclusiveB" }),
    actionNode("final-total", "修改变量", 7280, 240, "changeVariable", { targetVariableName: "total", newValueExpression: "$listScore + $loopScore + $objectScore + $gatewayScore" }),
    node("end", "endEvent", "End", 7480, 240, { returnValue: { raw: "$total" } }),
  ];
  const edges: MicroflowWorkflowEdgeJSON[] = [
    edge("f-start-total", "start", "create-total"),
    edge("f-total-list-score", "create-total", "create-list-score"),
    edge("f-list-loop-score", "create-list-score", "create-loop-score"),
    edge("f-loop-object-score", "create-loop-score", "create-object-score"),
    edge("f-object-gateway-score", "create-object-score", "create-gateway-score"),
    edge("f-gateway-create-list", "create-gateway-score", "create-list"),
    edge("f-create-change-list", "create-list", "change-list"),
    edge("f-change-sort", "change-list", "sort-list"),
    edge("f-sort-filter", "sort-list", "filter-list"),
    edge("f-filter-aggregate", "filter-list", "aggregate-list"),
    edge("f-aggregate-operation", "aggregate-list", "list-operation"),
    edge("f-operation-decision", "list-operation", "decision"),
    edge("f-decision-true", "decision", "set-list-score", { edgeKind: "decisionCondition", caseValues: boolCase(true) }),
    edge("f-decision-false", "decision", "fallback-list-score", { edgeKind: "decisionCondition", caseValues: boolCase(false) }),
    edge("f-decision-true-merge", "set-list-score", "merge"),
    edge("f-decision-false-merge", "fallback-list-score", "merge"),
    edge("f-merge-loop", "merge", "loop-numbers"),
    edge("f-loop-create-object", "loop-numbers", "create-object"),
    edge("f-loop-note", "loop-note", "loop-numbers", { edgeKind: "annotation", flowKind: "annotation" }),
    edge("f-loop-body-continue-decision", "loop-numbers", "continue-check", { edgeKind: "loopBody", collectionId: loopBody }),
    loopEdge("f-continue-true", "continue-check", "continue-event", { edgeKind: "decisionCondition", caseValues: boolCase(true) }),
    loopEdge("f-continue-false", "continue-check", "break-check", { edgeKind: "decisionCondition", caseValues: boolCase(false) }),
    loopEdge("f-break-false", "break-check", "loop-touch", { edgeKind: "decisionCondition", caseValues: boolCase(false) }),
    loopEdge("f-break-true", "break-check", "break-event", { edgeKind: "decisionCondition", caseValues: boolCase(true) }),
    edge("f-create-change-object", "create-object", "change-object"),
    edge("f-change-cast", "change-object", "cast-object"),
    edge("f-cast-object-type", "cast-object", "object-type"),
    edge("f-object-student", "object-type", "object-type-student-touch", { edgeKind: "objectTypeCondition", caseValues: objectCase("Sales.Student") }),
    edge("f-object-fallback", "object-type", "object-type-fallback-touch", { edgeKind: "objectTypeCondition", caseValues: objectCase("fallback") }),
    edge("f-object-student-merge", "object-type-student-touch", "object-type-merge"),
    edge("f-object-fallback-merge", "object-type-fallback-touch", "object-type-merge"),
    edge("f-object-merge-commit", "object-type-merge", "commit-object"),
    edge("f-commit-retrieve", "commit-object", "retrieve-object"),
    edge("f-retrieve-score", "retrieve-object", "retrieve-score"),
    edge("f-retrieve-create-temp", "retrieve-score", "create-temp"),
    edge("f-create-temp-rollback", "create-temp", "rollback-object"),
    edge("f-rollback-score", "rollback-object", "rollback-score"),
    edge("f-rollback-delete", "rollback-score", "delete-object"),
    edge("f-delete-score", "delete-object", "delete-score"),
    edge("f-delete-parallel", "delete-score", "parallel-fork"),
    edge("f-parallel-a", "parallel-fork", "parallel-a"),
    edge("f-parallel-b", "parallel-fork", "parallel-b"),
    edge("f-parallel-a-join", "parallel-a", "parallel-join"),
    edge("f-parallel-b-join", "parallel-b", "parallel-join"),
    edge("f-parallel-score", "parallel-join", "parallel-score"),
    edge("f-parallel-inclusive", "parallel-score", "inclusive-fork"),
    edge("f-inclusive-a", "inclusive-fork", "inclusive-a", { edgeKind: "sequence", caseValues: exprCase("$hasFive = true") }),
    edge("f-inclusive-b", "inclusive-fork", "inclusive-b", { edgeKind: "sequence", caseValues: exprCase("$listScore = 18") }),
    edge("f-inclusive-a-join", "inclusive-a", "inclusive-join"),
    edge("f-inclusive-b-join", "inclusive-b", "inclusive-join"),
    edge("f-inclusive-score", "inclusive-join", "inclusive-score"),
    edge("f-inclusive-final", "inclusive-score", "final-total"),
    edge("f-final-end", "final-total", "end"),
  ];
  return {
    ...schema,
    displayName: schema.displayName === "MF_AllNodeComplexComputation_Test" && schema.name !== "MF_AllNodeComplexComputation_Test"
      ? schema.name
      : schema.displayName,
    documentation: "All screenshot node families acceptance fixture. Expected output: 120.",
    workflow: { nodes, edges },
    parameters: [{ id: "numbers", stableId: "numbers", name: "numbers", dataType: intListType, type: { kind: "list", name: "List<Integer>", itemType: { kind: "primitive", name: "Integer" } }, required: true }],
    returnType: intType,
    returnVariableName: "total",
    editor: { ...schema.editor, viewport: { x: 0, y: 0, zoom: 0.35 }, selection: {} },
    audit: { ...schema.audit, updatedAt: new Date().toISOString() },
  };
}
