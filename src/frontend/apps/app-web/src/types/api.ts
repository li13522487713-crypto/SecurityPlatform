export type {
  ApiResponse,
  AuthProfile,
  AuthTokenResult,
  JsonValue,
  PagedRequest,
  PagedResult
} from "@atlas/shared-core";

export interface RuntimeMenuItem {
  pageKey: string;
  title: string;
  routePath: string;
  icon?: string | null;
  sortOrder: number;
}

export interface RuntimeMenuResponse {
  appKey: string;
  items: RuntimeMenuItem[];
}
