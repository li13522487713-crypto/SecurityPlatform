export type {
  ApiResponse,
  AuthProfile,
  AuthTokenResult,
  PagedRequest
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
