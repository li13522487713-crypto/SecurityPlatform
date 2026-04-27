import type { MicroflowAuthoringSchema, MicroflowObject, MicroflowObjectCollection } from "@atlas/microflow";

import type { StudioMicroflowDefinitionView } from "../studio/studio-microflow-types";
import type {
  MicroflowImpactLevel,
  MicroflowReference,
  MicroflowReferenceImpactSummary,
  MicroflowReferenceKind,
  MicroflowReferenceSourceType,
  StudioMicroflowCalleeView
} from "./microflow-reference-types";

export function groupMicroflowReferencesByType(references: MicroflowReference[]) {
  return references.reduce<Record<MicroflowReference["sourceType"], MicroflowReference[]>>((groups, reference) => {
    groups[reference.sourceType] = [...(groups[reference.sourceType] ?? []), reference];
    return groups;
  }, {} as Record<MicroflowReference["sourceType"], MicroflowReference[]>);
}

export function groupReferencesBySourceType(references: MicroflowReference[]): Record<MicroflowReferenceSourceType, MicroflowReference[]> {
  const groups: Record<MicroflowReferenceSourceType, MicroflowReference[]> = {
    microflow: [],
    workflow: [],
    page: [],
    form: [],
    button: [],
    schedule: [],
    api: [],
    unknown: []
  };
  references.forEach(reference => groups[reference.sourceType].push(reference));
  return groups;
}

export function getReferenceImpactSummary(references: MicroflowReference[]): MicroflowReferenceImpactSummary {
  return references.reduce<MicroflowReferenceImpactSummary>(
    (summary, reference) => ({
      ...summary,
      total: summary.total + 1,
      [reference.impactLevel]: summary[reference.impactLevel] + 1
    }),
    { total: 0, none: 0, low: 0, medium: 0, high: 0 }
  );
}

export function getReferenceTypeLabel(sourceType: MicroflowReferenceSourceType): string {
  const labels: Record<MicroflowReferenceSourceType, string> = {
    microflow: "微流",
    workflow: "工作流",
    page: "页面",
    form: "表单",
    button: "按钮",
    schedule: "定时任务",
    api: "API",
    unknown: "未知来源"
  };
  return labels[sourceType];
}

export function getReferenceKindLabel(referenceKind: MicroflowReferenceKind): string {
  const labels: Record<MicroflowReferenceKind, string> = {
    callMicroflow: "调用微流",
    pageAction: "页面动作",
    workflowActivity: "工作流活动",
    apiExposure: "API 暴露",
    scheduledJob: "定时作业",
    unknown: "未知引用"
  };
  return labels[referenceKind];
}

export function getImpactLevelLabel(level: MicroflowImpactLevel): string {
  const labels: Record<MicroflowImpactLevel, string> = {
    none: "无影响",
    low: "低影响",
    medium: "中影响",
    high: "高影响"
  };
  return labels[level];
}

export function getImpactLevelColor(level: MicroflowImpactLevel): "grey" | "blue" | "orange" | "red" {
  if (level === "high") {
    return "red";
  }
  if (level === "medium") {
    return "orange";
  }
  if (level === "low") {
    return "blue";
  }
  return "grey";
}

export function isMicroflowReferenced(references: MicroflowReference[]): boolean {
  return references.some(reference => reference.active !== false);
}

export function canDeleteMicroflowFromReferences(references: MicroflowReference[]): boolean {
  return !isMicroflowReferenced(references);
}

export function resolveReferenceDisplayName(
  reference: MicroflowReference,
  resourceIndex?: Record<string, StudioMicroflowDefinitionView>
): string {
  if (reference.sourceType === "microflow" && reference.sourceId && resourceIndex?.[reference.sourceId]) {
    const resource = resourceIndex[reference.sourceId];
    return resource.displayName || resource.name;
  }

  return reference.sourceName || reference.sourceId || reference.id;
}

function flattenObjects(collection: MicroflowObjectCollection | undefined): MicroflowObject[] {
  if (!collection) {
    return [];
  }

  return collection.objects.flatMap(object => [
    object,
    ...("objectCollection" in object ? flattenObjects(object.objectCollection) : [])
  ]);
}

export function parseMicroflowCallees(
  schema: MicroflowAuthoringSchema | undefined,
  sourceMicroflowId: string,
  resourceIndex?: Record<string, StudioMicroflowDefinitionView>
): StudioMicroflowCalleeView[] {
  if (!schema) {
    return [];
  }

  return flattenObjects(schema.objectCollection)
    .filter(object => object.kind === "actionActivity" && object.action.kind === "callMicroflow")
    .map(object => {
      const targetMicroflowId = object.action.targetMicroflowId?.trim() || undefined;
      const targetResource = targetMicroflowId ? resourceIndex?.[targetMicroflowId] : undefined;
      const targetMicroflowQualifiedName = object.action.targetMicroflowQualifiedName?.trim() || undefined;
      const staleReason = !targetMicroflowId
        ? "missingTargetId"
        : targetMicroflowId === sourceMicroflowId
          ? "selfCall"
          : targetResource
            ? undefined
            : "targetNotFound";

      return {
        sourceMicroflowId,
        sourceNodeId: object.id,
        sourceNodeName: object.caption || object.action.caption,
        targetMicroflowId,
        targetMicroflowName: targetResource?.displayName || targetResource?.name,
        targetMicroflowQualifiedName: targetResource?.qualifiedName || targetMicroflowQualifiedName,
        targetModuleId: targetResource?.moduleId,
        referenceKind: "callMicroflow" as const,
        stale: Boolean(staleReason),
        staleReason
      };
    });
}

export function buildStaleCallReferenceWarnings(callees: StudioMicroflowCalleeView[]): string[] {
  return callees
    .filter(callee => callee.stale)
    .map(callee => {
      if (callee.staleReason === "missingTargetId") {
        return `${callee.sourceNodeName || callee.sourceNodeId}: missing targetMicroflowId`;
      }
      if (callee.staleReason === "selfCall") {
        return `${callee.sourceNodeName || callee.sourceNodeId}: direct self call`;
      }
      return `${callee.sourceNodeName || callee.sourceNodeId}: target microflow not found`;
    });
}
