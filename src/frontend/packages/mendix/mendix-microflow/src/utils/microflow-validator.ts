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

const startEndKinds = new Set(["startEvent", "endEvent"]);
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

function countKinds(kinds: string[]): MicroflowComplexitySummary {
  const totalElements = kinds.filter(kind => !startEndKinds.has(kind)).length;
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
