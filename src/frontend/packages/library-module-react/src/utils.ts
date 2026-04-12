import type {
  AiLibraryItem,
  DocumentProcessingStatus,
  KnowledgeBaseDto,
  KnowledgeBaseType,
  ResourceType
} from "./types";

export function formatDateTime(value?: string): string {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat(undefined, {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  }).format(date);
}

export function mapKnowledgeBaseToLibraryItem(item: KnowledgeBaseDto): AiLibraryItem {
  return {
    resourceType: "knowledge-base",
    resourceId: item.id,
    name: item.name,
    description: item.description,
    updatedAt: item.createdAt,
    path: `/ai/knowledge-bases/${item.id}`,
    resourceSubType: mapKnowledgeType(item.type),
    status: item.documentCount > 0 ? "ready" : "processing",
    documentCount: item.documentCount,
    chunkCount: item.chunkCount
  };
}

export function mapKnowledgeType(type: KnowledgeBaseType): string {
  switch (type) {
    case 1:
      return "table";
    case 2:
      return "image";
    default:
      return "text";
  }
}

export function normalizeResourcePath(path: string, appKey: string, spaceId: string): string {
  if (!path) {
    return `/apps/${encodeURIComponent(appKey)}/space/${encodeURIComponent(spaceId)}/develop`;
  }

  if (path.startsWith("/ai/knowledge-bases/")) {
    const id = path.slice("/ai/knowledge-bases/".length);
    return `/apps/${encodeURIComponent(appKey)}/space/${encodeURIComponent(spaceId)}/knowledge/${id}`;
  }

  if (path.startsWith("/ai/agents")) {
    return `/apps/${encodeURIComponent(appKey)}/space/${encodeURIComponent(spaceId)}/develop?focus=agents`;
  }

  if (path.startsWith("/ai/workflows")) {
    return `/apps/${encodeURIComponent(appKey)}/work_flow`;
  }

  if (path.startsWith("/ai/prompts")) {
    return `/apps/${encodeURIComponent(appKey)}/space/${encodeURIComponent(spaceId)}/assistant`;
  }

  return path;
}

export function resolveKnowledgeStatus(
  declaredStatus: string | undefined,
  documentCount: number | undefined,
  chunkCount: number | undefined
): string {
  if (declaredStatus) {
    return declaredStatus;
  }

  if ((documentCount ?? 0) === 0) {
    return "processing";
  }

  if ((chunkCount ?? 0) <= 0) {
    return "processing";
  }

  return "ready";
}

export function resolveDocumentStatus(status: DocumentProcessingStatus): string {
  switch (status) {
    case 1:
      return "processing";
    case 2:
      return "ready";
    case 3:
      return "failed";
    default:
      return "pending";
  }
}

export function isKnowledgeType(resourceType: ResourceType): boolean {
  return resourceType === "knowledge-base";
}
