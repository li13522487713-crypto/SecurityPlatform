import type { MicroflowResource } from "../resource/resource-types";
import type { StudioMicroflowDefinitionView } from "./studio-microflow-types";

/**
 * 构造微流的限定名（qualifiedName）。
 *
 * 优先使用 moduleName.name；如果 moduleName 为空则使用 moduleId.name。
 */
export function getStudioMicroflowQualifiedName(resource: MicroflowResource): string {
  const prefix = resource.moduleName?.trim() || resource.moduleId;
  return `${prefix}.${resource.name}`;
}

/**
 * 将 MicroflowResource（后端适配器返回的原始资源）映射为
 * StudioMicroflowDefinitionView（Studio 展示层视图模型）。
 *
 * 不修改 MicroflowResource 结构，不修改 schema 结构，不引入新后端 DTO。
 */
export function mapMicroflowResourceToStudioDefinitionView(
  resource: MicroflowResource
): StudioMicroflowDefinitionView {
  const displayName =
    resource.displayName?.trim() || resource.name;

  const status: StudioMicroflowDefinitionView["status"] = resource.archived
    ? "archived"
    : resource.status;

  return {
    id: resource.id,
    moduleId: resource.moduleId,
    moduleName: resource.moduleName,
    name: resource.name,
    displayName,
    qualifiedName: getStudioMicroflowQualifiedName(resource),
    description: resource.description,
    status,
    publishStatus: resource.publishStatus,
    schemaId: resource.schemaId,
    version: resource.version,
    latestPublishedVersion: resource.latestPublishedVersion,
    referenceCount: resource.referenceCount,
    favorite: resource.favorite,
    archived: resource.archived,
    createdAt: resource.createdAt,
    updatedAt: resource.updatedAt,
  };
}
