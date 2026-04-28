import type { MicroflowPublishStatus, MicroflowResource, MicroflowResourceStatus } from "./resource-types";

export function formatMicroflowDate(iso?: string): string {
  if (!iso) {
    return "";
  }
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return iso;
  }
  const pad = (value: number) => String(value).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export function microflowStatusLabel(status: MicroflowResourceStatus): string {
  if (status === "published") {
    return "已发布";
  }
  if (status === "archived") {
    return "已归档";
  }
  return "草稿";
}

export function microflowStatusColor(status: MicroflowResourceStatus): "blue" | "green" | "grey" {
  if (status === "published") {
    return "green";
  }
  if (status === "archived") {
    return "grey";
  }
  return "blue";
}

export function microflowPublishStatusLabel(status?: MicroflowPublishStatus): string {
  if (status === "published") {
    return "已发布";
  }
  if (status === "changedAfterPublish") {
    return "发布后已修改";
  }
  return "未发布";
}

export function nextMicroflowVersion(version: string): string {
  const parts = version.replace(/^v/u, "").split(".").map(value => Number(value));
  if (!parts.length || parts.some(value => !Number.isFinite(value))) {
    return "1.0.0";
  }
  while (parts.length < 3) {
    parts.push(0);
  }
  parts[2] += 1;
  return parts.join(".");
}

export function canRunMicroflowAction(resource: MicroflowResource, action: keyof NonNullable<MicroflowResource["permissions"]>): boolean {
  if (action === "canPublish" && resource.archived) {
    return false;
  }
  return resource.permissions?.[action] ?? false;
}
