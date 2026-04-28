import type { MicroflowDataType } from "@atlas/microflow";

import type { MicroflowVersionDiff, MicroflowVersionSummary } from "./microflow-version-types";

export function sortMicroflowVersionsDesc(versions: MicroflowVersionSummary[]) {
  return [...versions].sort((left, right) => new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime());
}

export function formatVersionStatus(status: MicroflowVersionSummary["status"]): string {
  const labels: Record<MicroflowVersionSummary["status"], string> = {
    draft: "草稿",
    published: "已发布",
    archived: "已归档",
    rolledBack: "已回滚"
  };
  return labels[status];
}

export function versionStatusColor(status: MicroflowVersionSummary["status"]): "blue" | "green" | "grey" | "orange" {
  if (status === "published") {
    return "green";
  }
  if (status === "rolledBack") {
    return "orange";
  }
  if (status === "archived") {
    return "grey";
  }
  return "blue";
}

export function formatMicroflowDataType(type?: MicroflowDataType): string {
  if (!type) {
    return "unknown";
  }
  const named = type as MicroflowDataType & { name?: string; entity?: string; itemType?: MicroflowDataType };
  if (named.kind === "list" && named.itemType) {
    return `List<${formatMicroflowDataType(named.itemType)}>`;
  }
  return named.name || named.entity || named.kind;
}

export function emptyVersionDiff(): MicroflowVersionDiff {
  return {
    addedParameters: [],
    removedParameters: [],
    changedParameters: [],
    addedObjects: [],
    removedObjects: [],
    changedObjects: [],
    addedFlows: [],
    removedFlows: [],
    breakingChanges: []
  };
}
