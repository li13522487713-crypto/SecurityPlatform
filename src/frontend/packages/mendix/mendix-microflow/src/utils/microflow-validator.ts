import type { MicroflowAction, MicroflowAuthoringSchema, MicroflowDesignSchema, MicroflowObject, MicroflowWorkflowNodeJSON } from "../schema/types";

export const MICROFLOW_LIMITS = {
  RECOMMENDED_MAX_NODES: 25,
  ANNOTATION_REQUIRED: 10,
  MAX_DECISIONS_NO_WARN: 2,
  WARN_LEVEL: 20,
  ERROR_LEVEL: 25,
} as const;

export type MicroflowNodeCountLevel = "ok" | "warning" | "error";

export interface MicroflowComplexitySummary {
  totalElements: number;
  activityCount: number;
  decisionCount: number;
  hasAnnotation: boolean;
  level: MicroflowNodeCountLevel;
  recommendedMaxNodes: number;
  annotationRecommended: boolean;
}

export interface MicroflowBestPracticeWarning {
  code: "LOOP_COMMIT" | "MISSING_ERROR_HANDLER" | "NESTED_IF_EXPRESSION";
  message: string;
  objectId?: string;
  fieldPath: string;
  severity: "warning" | "info";
}

export interface MicroflowVariableConflict {
  code: "MF_VARIABLE_DUPLICATED" | "DUPLICATE_VARIABLE";
  message: string;
  nodeIds: string[];
}

export interface ValidationResult {
  level: "warning" | "error";
  code: string;
  message: string;
  nodeIds: string[];
}

export type FlowNodeLike = { type: string };

const startEndKinds = new Set(["startEvent", "endEvent"]);
const nodeCountExcludedKinds = new Set(["startEvent", "endEvent", "annotation"]);
const nodeCountExcludedTypeNames = new Set(["start", "end", "startEvent", "endEvent", "annotation"]);
const decisionKinds = new Set(["exclusiveSplit", "inheritanceSplit", "parallelGateway", "inclusiveGateway"]);
const activityExcludedKinds = new Set([
  ...startEndKinds,
  "errorEvent",
  "breakEvent",
  "continueEvent",
  "exclusiveMerge",
  "loopedActivity",
  "annotation",
  "parameterObject",
  "tryCatch",
  "errorHandler",
]);

function flattenAuthoringObjects(collection: { objects: MicroflowObject[] }): MicroflowObject[] {
  return collection.objects.flatMap(object =>
    object.kind === "loopedActivity"
      ? [object, ...flattenAuthoringObjects(object.objectCollection)]
      : [object],
  );
}

function flattenAuthoringLocations(
  collection: { objects: MicroflowObject[] },
  parentLoopObjectId?: string,
): Array<{ object: MicroflowObject; parentLoopObjectId?: string }> {
  return collection.objects.flatMap(object =>
    object.kind === "loopedActivity"
      ? [
          { object, parentLoopObjectId },
          ...flattenAuthoringLocations(object.objectCollection, object.id),
        ]
      : [{ object, parentLoopObjectId }],
  );
}

function nodeKind(node: MicroflowWorkflowNodeJSON): string {
  return String((node.data as { objectKind?: string } | undefined)?.objectKind ?? node.type ?? "");
}

function isLoopCommitAction(action: MicroflowAction | undefined): boolean {
  return action?.kind === "commit";
}

function isIntegrationAction(action: MicroflowAction | undefined): boolean {
  return action?.kind === "restCall"
    || action?.kind === "webServiceCall"
    || action?.kind === "callJavaAction";
}

function lacksCustomErrorHandling(action: MicroflowAction | undefined): boolean {
  const handling = action?.errorHandlingType;
  return handling == null || handling === "rollback";
}

function hasNestedIfExpression(raw: string | undefined): boolean {
  const normalized = String(raw ?? "").toLowerCase();
  return (normalized.match(/\bif\b/g) ?? []).length > 1;
}

function collectOutputVariableNames(action: MicroflowAction | Record<string, unknown> | undefined): Array<{ name: string; fieldPath: string }> {
  const record = (action as Record<string, unknown> | undefined) ?? {};
  const out: Array<{ name: string; fieldPath: string }> = [];
  const add = (name: unknown, fieldPath: string) => {
    if (typeof name === "string" && name.trim()) {
      out.push({ name, fieldPath });
    }
  };
  if (record.kind === "createVariable") {
    add(record.variableName, "action.variableName");
    return out;
  }
  if (record.kind === "createList") {
    add(record.outputListVariableName, "action.outputListVariableName");
    add(record.listVariableName, "action.listVariableName");
    return out;
  }
  if (record.kind === "aggregateList") {
    add(record.outputVariableName, "action.outputVariableName");
    add(record.resultVariableName, "action.resultVariableName");
    return out;
  }
  if (record.kind === "listOperation") {
    add(record.outputVariableName, "action.outputVariableName");
    add(record.outputListVariableName, "action.outputListVariableName");
    return out;
  }
  if (record.kind === "restCall") {
    add(record.outputVariableName, "action.outputVariableName");
    const response = record.response;
    if (response && typeof response === "object") {
      const handling = (response as Record<string, unknown>).handling;
      if (handling && typeof handling === "object") {
        const handlingRecord = handling as Record<string, unknown>;
        add(handlingRecord.outputVariableName, "action.response.handling.outputVariableName");
        add(handlingRecord.statusCodeVariableName, "action.response.statusCodeVariableName");
        add(handlingRecord.headersVariableName, "action.response.headersVariableName");
      }
    }
    return out;
  }
  if (record.kind === "createObject" || record.kind === "retrieve") {
    add(record.outputVariableName, "action.outputVariableName");
    return out;
  }
  if (record.kind === "cast" || record.kind === "exportXml") {
    add(record.outputVariableName, "action.outputVariableName");
    return out;
  }
  if (record.kind === "callMicroflow" || record.kind === "callJavaAction" || record.kind === "callJavaScriptAction" || record.kind === "callWorkflow" || record.kind === "callExternalAction") {
    const returnValue = record.returnValue;
    if (returnValue && typeof returnValue === "object") {
      const returnRecord = returnValue as Record<string, unknown>;
      add(returnRecord.outputVariableName, "action.returnValue.outputVariableName");
      add(returnRecord.resultVariableName, "action.returnValue.resultVariableName");
    }
    if (record.kind === "callExternalAction") {
      add(record.returnVariableName, "action.returnVariableName");
    }
    if (record.kind === "callWorkflow") {
      add(record.outputWorkflowVariableName, "action.outputWorkflowVariableName");
    }
    return out;
  }
  add(record.outputVariableName, "action.outputVariableName");
  add(record.outputListVariableName, "action.outputListVariableName");
  add(record.resultVariableName, "action.resultVariableName");
  if (record.kind === "generateJumpToOptions" || record.kind === "retrieveWorkflows") {
    add(record.outputListVariableName, "action.outputListVariableName");
  }
  return out;
}

function addAuthoringVariableConflicts(
  result: MicroflowVariableConflict[],
  objects: Array<{ id: string; action?: MicroflowAction | undefined }>,
): void {
  const outputVariableMap = new Map<string, string[]>();
  for (const item of objects) {
    const action = item.action;
    if (!action) {
      continue;
    }
    const outputs = collectOutputVariableNames(action).map(item => item.name);
    if (outputs.length === 0) {
      continue;
    }
    const seenInNode = new Set<string>();
    for (const outputName of outputs) {
      const key = outputName.toLocaleLowerCase();
      if (seenInNode.has(key)) {
        continue;
      }
      seenInNode.add(key);
      const nodeIds = outputVariableMap.get(key);
      if (!nodeIds) {
        outputVariableMap.set(key, [item.id]);
        continue;
      }
      if (!nodeIds.includes(item.id)) {
        nodeIds.push(item.id);
      }
      result.push({
        code: "DUPLICATE_VARIABLE",
        message: `变量名 "${outputName}" 重复定义，与节点 ${nodeIds[0]} 冲突。`,
        nodeIds: [...new Set(nodeIds)],
      });
    }
  }
}

function addDesignVariableConflicts(
  result: MicroflowVariableConflict[],
  nodes: Array<{ id: string; data?: Record<string, unknown> }>,
): void {
  const outputsByNode = nodes.flatMap(node => {
    const action = node.data?.action as MicroflowAction | undefined;
    return action ? [{ id: node.id, action }] : [];
  }).map(item => ({ id: item.id, action: item.action }));
  addAuthoringVariableConflicts(result, outputsByNode);
}

export function validateVariableNames(schema: MicroflowAuthoringSchema | MicroflowDesignSchema): MicroflowVariableConflict[] {
  if ("workflow" in schema) {
    const conflicts: MicroflowVariableConflict[] = [];
    const nodes = schema.workflow.nodes.map(node => ({ id: node.id, data: node.data as Record<string, unknown> | undefined }));
    addDesignVariableConflicts(conflicts, nodes);
    return conflicts;
  }

  const conflicts: MicroflowVariableConflict[] = [];
  const objects = flattenAuthoringObjects(schema.objectCollection).flatMap(item => item.kind === "actionActivity"
    ? [{ id: item.id, action: item.action }]
    : []);
  addAuthoringVariableConflicts(conflicts, objects);
  return conflicts;
}

function countKinds(kinds: string[]): MicroflowComplexitySummary {
  const totalElements = kinds.filter(kind => !nodeCountExcludedKinds.has(kind)).length;
  const activityCount = kinds.filter(kind => !activityExcludedKinds.has(kind) && !decisionKinds.has(kind)).length;
  const decisionCount = kinds.filter(kind => decisionKinds.has(kind)).length;
  const hasAnnotation = kinds.includes("annotation");
  const annotationRecommended = activityCount > MICROFLOW_LIMITS.ANNOTATION_REQUIRED || decisionCount > MICROFLOW_LIMITS.MAX_DECISIONS_NO_WARN;
  const level: MicroflowNodeCountLevel = totalElements >= MICROFLOW_LIMITS.ERROR_LEVEL
    ? "error"
    : totalElements >= MICROFLOW_LIMITS.WARN_LEVEL
      ? "warning"
      : "ok";
  return {
    totalElements,
    activityCount,
    decisionCount,
    hasAnnotation,
    level,
    recommendedMaxNodes: MICROFLOW_LIMITS.RECOMMENDED_MAX_NODES,
    annotationRecommended,
  };
}

export function summarizeMicroflowComplexity(schema: MicroflowAuthoringSchema | MicroflowDesignSchema): MicroflowComplexitySummary {
  if ("workflow" in schema) {
    return countKinds((schema.workflow.nodes ?? []).map(nodeKind));
  }
  return countKinds(flattenAuthoringObjects(schema.objectCollection).map(object => object.kind));
}

export function collectMicroflowBestPracticeWarnings(schema: MicroflowAuthoringSchema | MicroflowDesignSchema): MicroflowBestPracticeWarning[] {
  if ("workflow" in schema) {
    const nodes = schema.workflow.nodes ?? [];
    const objectKindById = new Map(nodes.map(node => [node.id, nodeKind(node)]));
    return nodes.flatMap((node, index) => {
      const data = (node.data ?? {}) as {
        parentObjectId?: string;
        action?: MicroflowAction;
        actionKind?: string;
      };
      const action = data.action;
      const warnings: MicroflowBestPracticeWarning[] = [];
      if (isLoopCommitAction(action) && data.parentObjectId && objectKindById.get(String(data.parentObjectId)) === "loopedActivity") {
        warnings.push({
          code: "LOOP_COMMIT",
          message: "在 Loop 内直接 Commit 对象会导致性能问题，建议在 Loop 外批量 Commit。",
          objectId: node.id,
          fieldPath: `workflow.nodes.${index}.data.actionKind`,
          severity: "warning",
        });
      }
      if (isIntegrationAction(action) && lacksCustomErrorHandling(action)) {
        warnings.push({
          code: "MISSING_ERROR_HANDLER",
          message: "集成调用（REST/Web Service/Java）建议配置错误处理路径。",
          objectId: node.id,
          fieldPath: `workflow.nodes.${index}.data.action.errorHandlingType`,
          severity: "warning",
        });
      }
      const splitCondition = (node.data as { splitCondition?: { expression?: { raw?: string } } } | undefined)?.splitCondition;
      if (nodeKind(node) === "exclusiveSplit" && hasNestedIfExpression(splitCondition?.expression?.raw)) {
        warnings.push({
          code: "NESTED_IF_EXPRESSION",
          message: "不建议在单个 Decision 表达式中嵌套 if 语句，应改用多个 Decision 节点。",
          objectId: node.id,
          fieldPath: `workflow.nodes.${index}.data.splitCondition.expression`,
          severity: "info",
        });
      }
      return warnings;
    });
  }

  return flattenAuthoringLocations(schema.objectCollection).flatMap(({ object, parentLoopObjectId }) => {
    const warnings: MicroflowBestPracticeWarning[] = [];
    if (object.kind === "actionActivity") {
      if (isLoopCommitAction(object.action) && parentLoopObjectId) {
        warnings.push({
          code: "LOOP_COMMIT",
          message: "在 Loop 内直接 Commit 对象会导致性能问题，建议在 Loop 外批量 Commit。",
          objectId: object.id,
          fieldPath: "action.kind",
          severity: "warning",
        });
      }
      if (isIntegrationAction(object.action) && lacksCustomErrorHandling(object.action)) {
        warnings.push({
          code: "MISSING_ERROR_HANDLER",
          message: "集成调用（REST/Web Service/Java）建议配置错误处理路径。",
          objectId: object.id,
          fieldPath: "action.errorHandlingType",
          severity: "warning",
        });
      }
    }
    if (object.kind === "exclusiveSplit" && object.splitCondition.kind === "expression" && hasNestedIfExpression(object.splitCondition.expression.raw)) {
      warnings.push({
        code: "NESTED_IF_EXPRESSION",
        message: "不建议在单个 Decision 表达式中嵌套 if 语句，应改用多个 Decision 节点。",
        objectId: object.id,
        fieldPath: "splitCondition.expression",
        severity: "info",
      });
    }
    return warnings;
  });
}

export function validateMicroflowSize(nodes: FlowNodeLike[]): ValidationResult[] {
  const types = nodes.map(item => item.type);
  const totalElements = types.filter(type => !nodeCountExcludedTypeNames.has(type)).length;
  const activityCount = types.filter(type => ![
    "startEvent",
    "endEvent",
    "decision",
    "exclusiveSplit",
    "inheritanceSplit",
    "parallelGateway",
    "inclusiveGateway",
    "merge",
    "mergeNode",
    "loopedActivity",
    "loop",
    "annotation",
    "parameter",
    "parameterObject",
    "errorHandler",
  ].includes(type)).length;
  const decisionCount = types.filter(type => ["decision", "exclusiveSplit", "inheritanceSplit"].includes(type)).length;
  const hasAnnotation = types.includes("annotation");
  const results: ValidationResult[] = [];
  if (totalElements >= MICROFLOW_LIMITS.ERROR_LEVEL) {
    results.push({
      level: "error",
      code: "MF_TOO_LARGE",
      message: `微流包含 ${totalElements} 个元素，超过推荐上限 25 个。建议将部分逻辑提取为子微流。`,
      nodeIds: [],
    });
  } else if (totalElements >= MICROFLOW_LIMITS.WARN_LEVEL) {
    results.push({
      level: "warning",
      code: "MF_APPROACHING_LIMIT",
      message: `微流包含 ${totalElements} 个元素，接近推荐上限 25 个。`,
      nodeIds: [],
    });
  }
  if (!hasAnnotation && (activityCount > MICROFLOW_LIMITS.ANNOTATION_REQUIRED || decisionCount > MICROFLOW_LIMITS.MAX_DECISIONS_NO_WARN)) {
    results.push({
      level: "warning",
      code: "MF_MISSING_ANNOTATION",
      message: "复杂微流（超过 10 个活动或 2 个 Decision）建议在起始处添加注释说明目的和参数。",
      nodeIds: [],
    });
  }
  return results;
}

export function validateMicroflowSizeFromSchema(schema: MicroflowAuthoringSchema | MicroflowDesignSchema): ValidationResult[] {
  if ("workflow" in schema) {
    return validateMicroflowSize(schema.workflow.nodes.map(node => ({ type: node.type })));
  }
  const flattenKinds = flattenAuthoringObjects(schema.objectCollection).map(item => item.kind);
  return validateMicroflowSize(flattenKinds.map(kind => ({ type: kind })));
}
