import type { MicroflowApiResponse } from "./api-envelope";
import type { MicroflowApiPageResult } from "./api-envelope";
import type { MicroflowCreateInput, MicroflowDuplicateInput, MicroflowResource, MicroflowResourcePatch, MicroflowResourceQuery } from "../../resource/resource-types";

/**
 * 列表查询：与 `MicroflowResourceQuery` 同字段，**排除** 仅资源库 UI 使用的 `view`。
 * 多选 `status` / `publishStatus` / `tags` 均为 **OR** 语义。
 */
export type ListMicroflowsRequest = Omit<MicroflowResourceQuery, "view">;

export type ListMicroflowsResponse = MicroflowApiPageResult<MicroflowResource>;

export interface CreateMicroflowRequest {
  workspaceId?: string;
  input: MicroflowCreateInput;
}

export type CreateMicroflowResponse = MicroflowApiResponse<MicroflowResource>;

export type GetMicroflowResponse = MicroflowApiResponse<MicroflowResource>;

export interface UpdateMicroflowResourceRequest {
  patch: MicroflowResourcePatch;
}

export type UpdateMicroflowResourceResponse = MicroflowApiResponse<MicroflowResource>;

export interface DuplicateMicroflowRequest {
  name?: string;
  displayName?: string;
  moduleId?: string;
  moduleName?: string;
  tags?: string[];
}

export type DuplicateMicroflowFromResourceResponse = MicroflowApiResponse<MicroflowResource>;

export interface RenameMicroflowRequest {
  name: string;
  displayName?: string;
}

export type RenameMicroflowResponse = MicroflowApiResponse<MicroflowResource>;

export interface ToggleFavoriteMicroflowRequest {
  favorite: boolean;
}

export type ToggleFavoriteMicroflowResponse = MicroflowApiResponse<MicroflowResource>;

export type ArchiveMicroflowResponse = MicroflowApiResponse<MicroflowResource>;
export type RestoreMicroflowResponse = MicroflowApiResponse<MicroflowResource>;
export type DeleteMicroflowResponse = MicroflowApiResponse<{ id: string }>;
