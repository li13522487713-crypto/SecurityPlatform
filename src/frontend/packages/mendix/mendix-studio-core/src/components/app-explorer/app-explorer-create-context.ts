import type { MicroflowModuleAsset } from "../../microflow/resource";
import { MicroflowsSectionKey } from "./MicroflowsTreeSection";

export interface ExplorerCreateContextNode {
  key: string;
  kind?: string;
  moduleId?: string;
  folderId?: string;
  folderPath?: string;
}

export interface ExplorerCreateContext {
  appId?: string;
  workspaceId?: string;
  moduleId?: string;
  moduleName?: string;
  folderId?: string;
  folderPath?: string;
  sourceNodeKey: string;
}

function parseModuleIdFromExplorerKey(key?: string): string | undefined {
  if (!key) {
    return undefined;
  }
  const [, moduleId] = key.split(":");
  return moduleId || undefined;
}

export function resolveExplorerCreateContext(input: {
  node: ExplorerCreateContextNode;
  modules: MicroflowModuleAsset[];
  appId?: string;
  workspaceId?: string;
  fallbackModuleId?: string;
}): ExplorerCreateContext {
  const { node, modules, appId, workspaceId, fallbackModuleId } = input;
  const moduleIds = new Set(modules.map(module => module.moduleId).filter(Boolean));
  const explicitModuleId = node.moduleId
    ?? (node.key.startsWith(`${MicroflowsSectionKey}:`) ? parseModuleIdFromExplorerKey(node.key) : undefined)
    ?? (node.kind === "module" ? parseModuleIdFromExplorerKey(node.key) : undefined);
  const fallbackIsValid = fallbackModuleId && moduleIds.has(fallbackModuleId);
  const moduleId = explicitModuleId && (moduleIds.size === 0 || moduleIds.has(explicitModuleId))
    ? explicitModuleId
    : fallbackIsValid
      ? fallbackModuleId
      : undefined;
  const module = modules.find(item => item.moduleId === moduleId);
  return {
    appId,
    workspaceId,
    moduleId,
    moduleName: module?.name || module?.qualifiedName || moduleId,
    folderId: node.folderId,
    folderPath: node.folderPath,
    sourceNodeKey: node.key
  };
}
