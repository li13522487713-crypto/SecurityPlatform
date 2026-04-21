import type { PagedResult } from "@atlas/shared-react-core/types";
import {
  createLowcodeApp,
  deleteLowcodeApp,
  getLowcodeAppsPaged,
  type LowcodeAppListQuery
} from "../../services/api-lowcode-apps";
import { navigateToLowcodeStudio } from "../navigation/lowcode-studio-navigator";

export interface ProjectAppCapabilities {
  canFavorite: boolean;
  canDuplicate: boolean;
  canMove: boolean;
  canMigrate: boolean;
  canCopyToWorkspace: boolean;
  canDelete: boolean;
}

export interface ProjectAppCard {
  id: string;
  name: string;
  description?: string;
  status: string;
  updatedAt: string;
}

export interface ProjectAppCreateRequest {
  name: string;
  description?: string;
  workspaceId?: string;
  locale?: string;
}

export interface ProjectAppGateway {
  list: (query?: LowcodeAppListQuery) => Promise<PagedResult<ProjectAppCard>>;
  create: (request: ProjectAppCreateRequest) => Promise<{ appId: string }>;
  delete: (appId: string) => Promise<void>;
  open: (appId: string) => void;
  getCapabilities: () => ProjectAppCapabilities;
}

export interface LowcodeProjectAppGatewayOptions {
  canDelete?: boolean;
  navigate?: (path: string) => void;
}

const LOWCODE_APP_CAPABILITIES: ProjectAppCapabilities = {
  canFavorite: false,
  canDuplicate: false,
  canMove: false,
  canMigrate: false,
  canCopyToWorkspace: false,
  canDelete: true
};

function normalizeLocale(locale?: string): "zh-CN" | "en-US" {
  return locale === "en-US" ? "en-US" : "zh-CN";
}

function buildAppCode(name: string): string {
  const slug = name
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 24);
  const suffix = `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
  // 后端校验：code 必须以字母开头（^[a-zA-Z]...）。
  // 若 slug 为空或首字符为数字（如纯数字名称），统一加 app- 前缀。
  const isValidPrefix = slug && /^[a-z]/.test(slug);
  const prefix = isValidPrefix ? slug : `app-${slug || ""}`;
  return `${prefix}-${suffix}`.replace(/^-+|-+$/g, "").replace(/-{2,}/g, "-");
}

export function createLowcodeProjectAppGateway(options?: LowcodeProjectAppGatewayOptions): ProjectAppGateway {
  const canDelete = options?.canDelete ?? true;
  const navigate = options?.navigate;

  return {
    async list(query) {
      const result = await getLowcodeAppsPaged(query);
      return {
        ...result,
        items: result.items.map(item => ({
          id: item.id,
          name: item.displayName,
          description: item.description,
          status: item.status,
          updatedAt: item.updatedAt
        }))
      };
    },
    async create(request) {
      const trimmedName = request.name.trim();
      const appId = await createLowcodeApp({
        code: buildAppCode(trimmedName),
        displayName: trimmedName,
        description: request.description?.trim() || undefined,
        targetTypes: "web",
        defaultLocale: normalizeLocale(request.locale),
        workspaceId: request.workspaceId,
        theme: null
      });
      return { appId };
    },
    delete: deleteLowcodeApp,
    open(appId) {
      navigateToLowcodeStudio(appId, navigate);
    },
    getCapabilities() {
      return {
        ...LOWCODE_APP_CAPABILITIES,
        canDelete
      };
    }
  };
}
