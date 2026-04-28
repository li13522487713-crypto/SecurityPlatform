import type { MicroflowApiError } from "../../microflow/contracts/api/api-envelope";
import type { MicroflowFolder } from "../../microflow/folders/microflow-folder-types";
import type { MicroflowValidationSummary } from "../../store";
import type { StudioMicroflowDefinitionView } from "../../microflow/studio/studio-microflow-types";
import type { ExplorerTreeNode, MicroflowLoadStatus } from "./AppExplorerContainer";

export const MicroflowsSectionKey = "microflows";

export function createMicroflowStateChildren(
  status: MicroflowLoadStatus,
  microflows: StudioMicroflowDefinitionView[],
  error?: MicroflowApiError,
  validationSummaryByMicroflowId: Record<string, MicroflowValidationSummary> = {},
  folders: MicroflowFolder[] = [],
  moduleId?: string
): ExplorerTreeNode[] {
  if (status === "loading" || status === "idle") {
    return [{ key: `microflows:${moduleId ?? "default"}:loading`, label: "Loading microflows...", kind: "loading", readonly: true }];
  }

  if (status === "error") {
    return [{
      key: `microflows:${moduleId ?? "default"}:error`,
      label: "Load failed",
      kind: "error",
      moduleId,
      readonly: true,
      errorMessage: error?.message,
      error,
      action: "retryMicroflows"
    }];
  }

  if (microflows.length === 0 && folders.length === 0) {
    return [{ key: `microflows:${moduleId ?? "default"}:empty`, label: "No microflows", kind: "empty", moduleId, readonly: true }];
  }

  return mapMicroflowResourcesToTree(microflows, folders, validationSummaryByMicroflowId);
}

export function mapMicroflowResourcesToTree(
  microflows: StudioMicroflowDefinitionView[],
  folders: MicroflowFolder[],
  validationSummaryByMicroflowId: Record<string, MicroflowValidationSummary> = {}
): ExplorerTreeNode[] {
  const childrenByParentId = new Map<string, ExplorerTreeNode[]>();
  const folderById = new Map(folders.map(folder => [folder.id, folder]));

  const appendChild = (parentId: string, child: ExplorerTreeNode) => {
    childrenByParentId.set(parentId, [...(childrenByParentId.get(parentId) ?? []), child]);
  };

  for (const folder of folders) {
    appendChild(folder.parentFolderId ?? "", {
      key: `microflow-folder:${folder.id}`,
      label: folder.name,
      icon: "F",
      kind: "folder",
      moduleId: folder.moduleId,
      folderId: folder.id,
      folderPath: folder.path,
      name: folder.name,
      dynamic: true,
      title: folder.path,
      children: []
    });
  }

  for (const resource of microflows) {
    const parentId = resource.folderId && folderById.has(resource.folderId) ? resource.folderId : "";
    appendChild(parentId, {
      key: `microflow:${resource.id}`,
      label: resource.displayName || resource.name,
      icon: "M",
      kind: "microflow",
      moduleId: resource.moduleId,
      folderId: resource.folderId,
      folderPath: resource.folderPath,
      microflowId: resource.id,
      resourceId: resource.id,
      name: resource.name,
      displayName: resource.displayName,
      qualifiedName: resource.qualifiedName,
      status: resource.status,
      publishStatus: resource.publishStatus,
      referenceCount: resource.referenceCount,
      problemSummary: validationSummaryByMicroflowId[resource.id],
      dynamic: true,
      title: resource.folderPath ? `${resource.folderPath}/${resource.name}` : resource.qualifiedName
    });
  }

  const attach = (parentId = ""): ExplorerTreeNode[] => (childrenByParentId.get(parentId) ?? [])
    .map(node => node.kind === "folder" && node.folderId
      ? { ...node, children: attach(node.folderId) }
      : node)
    .sort((left, right) => {
      if (left.kind === "folder" && right.kind !== "folder") {
        return -1;
      }
      if (left.kind !== "folder" && right.kind === "folder") {
        return 1;
      }
      return left.label.localeCompare(right.label, undefined, { sensitivity: "base" });
    });

  return attach();
}

export function createMicroflowLeafNodes(
  microflows: StudioMicroflowDefinitionView[],
  validationSummaryByMicroflowId: Record<string, MicroflowValidationSummary> = {}
): ExplorerTreeNode[] {
  return microflows.map(resource => ({
    key: `microflow:${resource.id}`,
    label: resource.displayName || resource.name,
    icon: "M",
    kind: "microflow",
    moduleId: resource.moduleId,
    folderId: resource.folderId,
    folderPath: resource.folderPath,
    microflowId: resource.id,
    resourceId: resource.id,
    name: resource.name,
    displayName: resource.displayName,
    qualifiedName: resource.qualifiedName,
    status: resource.status,
    publishStatus: resource.publishStatus,
    referenceCount: resource.referenceCount,
    problemSummary: validationSummaryByMicroflowId[resource.id],
    dynamic: true,
    title: resource.qualifiedName
  }));
}
