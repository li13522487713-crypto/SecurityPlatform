import {
  communityWorksPath,
  docsPath,
  marketPluginsPath,
  marketTemplatesPath,
  openApiPath,
  workspaceEvaluationsPath,
  workspaceHomePath,
  workspaceProjectsPath,
  workspaceResourcesPath,
  workspaceSettingsPublishPath,
  workspaceTasksPath
} from "@atlas/app-shell-shared";
import type { AppMessageKey } from "./messages";

export type MenuGroupKey = "workspace" | "ecosystem";

export interface MenuItemConfig {
  /** 唯一 key（前端高亮、跳转、埋点都依赖它）。 */
  key: string;
  /** i18n 文案 key。 */
  labelKey: AppMessageKey;
  /** 一个字母/字符的临时图标占位（接入 IconSet 后替换）。 */
  iconGlyph: string;
  /**
   * 路径生成器。工作空间域菜单需要传入 `workspaceId`，平台域菜单忽略入参。
   */
  buildPath: (workspaceId: string) => string;
  /** 高亮匹配前缀。默认与 `buildPath()` 的返回值同前缀。 */
  matchPrefix?: (workspaceId: string) => string;
  /** 该菜单项需要的权限点；为空则全员可见。 */
  permission?: string;
  /** 测试用 data-testid 后缀。 */
  testIdSuffix: string;
}

export interface MenuGroupConfig {
  key: MenuGroupKey;
  titleKey: AppMessageKey;
  items: MenuItemConfig[];
}

/**
 * 12 项一级菜单 + 2 个分组（工作空间域 / 生态域）。
 *
 * 结构对齐：docs/coze_prd_docs/02-左侧导航与路由PRD.md +
 * docs/coze_prd_docs/07-前端路由表与菜单权限表.md。
 *
 * 权限点暂用占位（以 `coze.menu.*` 命名），等后端给到工作空间角色映射后再细化。
 */
export const MENU_GROUPS: MenuGroupConfig[] = [
  {
    key: "workspace",
    titleKey: "cozeMenuGroupWorkspace",
    items: [
      {
        key: "home",
        labelKey: "cozeMenuHome",
        iconGlyph: "首",
        buildPath: workspaceHomePath,
        testIdSuffix: "home"
      },
      {
        key: "projects",
        labelKey: "cozeMenuProjects",
        iconGlyph: "项",
        buildPath: workspaceProjectsPath,
        testIdSuffix: "projects"
      },
      {
        key: "resources",
        labelKey: "cozeMenuResources",
        iconGlyph: "资",
        buildPath: workspaceResourcesPath,
        testIdSuffix: "resources"
      },
      {
        key: "tasks",
        labelKey: "cozeMenuTasks",
        iconGlyph: "任",
        buildPath: workspaceTasksPath,
        testIdSuffix: "tasks"
      },
      {
        key: "evaluations",
        labelKey: "cozeMenuEvaluations",
        iconGlyph: "评",
        buildPath: workspaceEvaluationsPath,
        testIdSuffix: "evaluations"
      },
      {
        key: "settings",
        labelKey: "cozeMenuSettings",
        iconGlyph: "配",
        buildPath: workspaceSettingsPublishPath,
        matchPrefix: workspaceId => `/workspace/${encodeURIComponent(workspaceId)}/settings`,
        testIdSuffix: "settings"
      }
    ]
  },
  {
    key: "ecosystem",
    titleKey: "cozeMenuGroupEcosystem",
    items: [
      {
        key: "templates",
        labelKey: "cozeMenuTemplates",
        iconGlyph: "模",
        buildPath: () => marketTemplatesPath(),
        testIdSuffix: "templates"
      },
      {
        key: "plugins",
        labelKey: "cozeMenuPlugins",
        iconGlyph: "插",
        buildPath: () => marketPluginsPath(),
        testIdSuffix: "plugins"
      },
      {
        key: "community",
        labelKey: "cozeMenuCommunity",
        iconGlyph: "社",
        buildPath: () => communityWorksPath(),
        testIdSuffix: "community"
      },
      {
        key: "open-api",
        labelKey: "cozeMenuOpenApi",
        iconGlyph: "API",
        buildPath: () => openApiPath(),
        testIdSuffix: "open-api"
      },
      {
        key: "docs",
        labelKey: "cozeMenuDocs",
        iconGlyph: "文",
        buildPath: () => docsPath(),
        testIdSuffix: "docs"
      },
    ]
  }
];
