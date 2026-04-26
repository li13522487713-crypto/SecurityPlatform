import type { MicroflowReference } from "../resource/resource-types";

export function groupMicroflowReferencesByType(references: MicroflowReference[]) {
  return references.reduce<Record<MicroflowReference["sourceType"], MicroflowReference[]>>((groups, reference) => {
    groups[reference.sourceType] = [...(groups[reference.sourceType] ?? []), reference];
    return groups;
  }, {} as Record<MicroflowReference["sourceType"], MicroflowReference[]>);
}
import type {
  MicroflowImpactLevel,
  MicroflowReference,
  MicroflowReferenceImpactSummary,
  MicroflowReferenceKind,
  MicroflowReferenceSourceType
} from "./microflow-reference-types";

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
