import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { Button, Dropdown, Empty, Input, Modal, Select, Spin, Table, Tabs, Toast, Tooltip, Typography } from "@douyinfe/semi-ui";
import {
  IconSearch,
  IconCode,
  IconFolder,
  IconArticle,
  IconPuzzle,
  IconList,
  IconBox,
  IconHistogram,
  IconLink,
  IconChevronDown
} from "@douyinfe/semi-icons";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { useNavigate, useSearchParams } from "react-router-dom";
import {
  chatflowEditorPath,
  orgWorkspaceDatabaseDetailPath,
  orgWorkspaceKnowledgeBaseDetailPath,
  orgWorkspacePluginDetailPath,
  workflowEditorPath,
  type WorkspaceLibraryTab
} from "@atlas/app-shell-shared";
import { useAppI18n } from "../i18n";
import { useWorkspaceContext } from "../workspace-context";
import { LibraryCreateDropdown } from "./components/library-create-dropdown";
import { LibraryCreateModal } from "./components/library-create-modal";
import { LibraryImportModal } from "./components/library-import-modal";
import {
  getLibraryPaged,
  type AiWorkspaceLibraryItem,
  type LibraryResourceType,
  type LibrarySource
} from "../../services/api-ai-workspace";
import type { AppMessageKey } from "../messages";
import { deleteAiDatabase } from "../../services/api-ai-database";
import { MigrationWizardDrawer } from "./components/migration-wizard/migration-wizard-drawer";
import { deleteTenantDataSource } from "../../services/api-tenant-datasource";
import { APP_PERMISSIONS } from "../../constants/permissions";
import { useOptionalPermissionContext } from "../permission-context";

type LibraryTabKey =
  | "all"
  | "plugin"
  | "workflow"
  | "knowledge-base"
  | "card"
  | "prompt"
  | "database"
  | "voice"
  | "memory";

interface TabDef {
  key: LibraryTabKey;
  labelKey: AppMessageKey;
  resourceType?: LibraryResourceType;
}

const TAB_DEFS: TabDef[] = [
  { key: "all", labelKey: "cozeLibraryTabAll" },
  { key: "plugin", labelKey: "cozeLibraryTabPlugin", resourceType: "plugin" },
  { key: "workflow", labelKey: "cozeLibraryTabWorkflow", resourceType: "workflow" },
  { key: "knowledge-base", labelKey: "cozeLibraryTabKnowledge", resourceType: "knowledge-base" },
  { key: "card", labelKey: "cozeLibraryTabCard", resourceType: "card" },
  { key: "prompt", labelKey: "cozeLibraryTabPrompt", resourceType: "prompt" },
  { key: "database", labelKey: "cozeLibraryTabDatabase", resourceType: "database" },
  { key: "voice", labelKey: "cozeLibraryTabVoice", resourceType: "voice" },
  { key: "memory", labelKey: "cozeLibraryTabMemory", resourceType: "memory" }
];

type SubTypeValue = "all" | string;

function iconOf(rt: LibraryResourceType) {
  const map: Record<string, ReactNode> = {
    plugin: <IconPuzzle />,
    workflow: <IconCode />,
    "knowledge-base": <IconFolder />,
    card: <IconBox />,
    prompt: <IconArticle />,
    database: <IconList />,
    voice: <IconHistogram />,
    memory: <IconLink />,
    agent: <IconBox />,
    app: <IconBox />
  };
  return map[rt] ?? <IconFolder />;
}

function formatDate(iso?: string): string {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  const pad = (n: number) => n.toString().padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

const TAB_KEYS = new Set<string>(TAB_DEFS.map(x => x.key));
const SETUP_CONSOLE_TOKEN_KEY = "atlas_setup_console_token";

function hasSetupConsoleToken(): boolean {
  if (typeof window === "undefined") {
    return false;
  }

  try {
    return Boolean(window.sessionStorage.getItem(SETUP_CONSOLE_TOKEN_KEY));
  } catch {
    return false;
  }
}

function tabFromSearch(query: string | null): LibraryTabKey {
  if (!query || query === "all") {
    return "all";
  }
  return TAB_KEYS.has(query) ? (query as LibraryTabKey) : "all";
}

/**
 * 历史 `?type=<非负整数>`：与 `/space/.../library?type=1`（如技能弹窗开插件资源）等旧链接兼容。
 * 0–8 与资源库 9 类顺序大致对齐，未识别则忽略。
 */
function legacyNumericTypeToTab(raw: string | null): { tab: LibraryTabKey; subType?: SubTypeValue } | null {
  if (raw == null || raw.trim() === "") {
    return null;
  }
  const n = raw.trim();
  if (!/^\d+$/u.test(n)) {
    return null;
  }
  const map: Record<string, { tab: LibraryTabKey; subType?: SubTypeValue }> = {
    "0": { tab: "all" },
    "1": { tab: "plugin" },
    "2": { tab: "workflow" },
    "3": { tab: "knowledge-base" },
    "4": { tab: "card" },
    "5": { tab: "prompt" },
    "6": { tab: "database" },
    "7": { tab: "voice" },
    "8": { tab: "memory" }
  };
  return map[n] ?? null;
}

function normalizeSubTypeForTab(tab: LibraryTabKey, raw: string | null): SubTypeValue {
  if (raw == null || raw === "" || raw === "all") {
    return "all";
  }
  const st = raw;
  switch (tab) {
    case "knowledge-base":
      return ["text", "table", "image"].includes(st) ? st : "all";
    case "workflow":
      return ["workflow", "chatflow"].includes(st) ? st : "all";
    case "plugin":
      return ["builtin", "custom"].includes(st) ? st : "all";
    case "database":
      return st === "table" ? st : "all";
    case "memory":
      return st === "long-term" ? st : "all";
    default:
      return "all";
  }
}

function readLibraryStateFromSearchParams(sp: URLSearchParams): { tab: LibraryTabKey; subType: SubTypeValue } {
  if (!sp.get("tab") && sp.has("type")) {
    const mapped = legacyNumericTypeToTab(sp.get("type"));
    if (mapped) {
      return {
        tab: mapped.tab,
        subType: mapped.subType ?? normalizeSubTypeForTab(mapped.tab, sp.get("subType"))
      };
    }
  }
  const tab = tabFromSearch(sp.get("tab"));
  return { tab, subType: normalizeSubTypeForTab(tab, sp.get("subType")) };
}

export function WorkspaceLibraryPage() {
  const { t } = useAppI18n();
  const permission = useOptionalPermissionContext();
  const workspace = useWorkspaceContext();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [activeTab, setActiveTab] = useState<LibraryTabKey>(
    readLibraryStateFromSearchParams(new URLSearchParams(searchParams.toString())).tab
  );
  const [source, setSource] = useState<LibrarySource>("all");
  const [subType, setSubType] = useState<SubTypeValue>(
    readLibraryStateFromSearchParams(new URLSearchParams(searchParams.toString())).subType
  );
  const [keyword, setKeyword] = useState("");
  const [items, setItems] = useState<AiWorkspaceLibraryItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [total, setTotal] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [importOpen, setImportOpen] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [createType, setCreateType] = useState<LibraryResourceType | null>(null);
  const [migrationSource, setMigrationSource] = useState<AiWorkspaceLibraryItem | null>(null);

  const subTypeOptions = useMemo(() => {
    const all = { value: "all" as const, label: t("cozeLibrarySubTypeAll") };
    const tab = TAB_DEFS.find(x => x.key === activeTab);
    const rt = tab?.resourceType;
    if (!rt || activeTab === "all") {
      return [all];
    }
    switch (rt) {
      case "knowledge-base":
        return [
          all,
          { value: "text", label: t("cozeLibrarySubTypeKbText") },
          { value: "table", label: t("cozeLibrarySubTypeKbTable") },
          { value: "image", label: t("cozeLibrarySubTypeKbImage") }
        ];
      case "workflow":
        return [
          all,
          { value: "workflow", label: t("cozeLibrarySubTypeWorkflow") },
          { value: "chatflow", label: t("cozeLibrarySubTypeChatflow") }
        ];
      case "plugin":
        return [
          all,
          { value: "builtin", label: t("cozeLibrarySubTypePluginBuiltin") },
          { value: "custom", label: t("cozeLibrarySubTypePluginCustom") }
        ];
      case "database":
        return [all, { value: "table", label: t("cozeLibraryTabDatabase") }];
      case "memory":
        return [all, { value: "long-term", label: t("cozeLibrarySubTypeMemoryLongTerm") }];
      default:
        return [all];
    }
  }, [activeTab, t]);

  useEffect(() => {
    const sp = new URLSearchParams(searchParams);
    if (sp.has("type")) {
      if (!sp.get("tab")) {
        const mapped = legacyNumericTypeToTab(sp.get("type"));
        if (mapped) {
          if (mapped.tab === "all") {
            sp.delete("tab");
          } else {
            sp.set("tab", mapped.tab);
          }
          if (mapped.subType && mapped.subType !== "all") {
            sp.set("subType", String(mapped.subType));
          }
        }
      }
      sp.delete("type");
      setSearchParams(sp, { replace: true });
      return;
    }
    const next = readLibraryStateFromSearchParams(new URLSearchParams(searchParams.toString()));
    setActiveTab(next.tab);
    setSubType(next.subType);
  }, [searchParams, setSearchParams]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const tab = TAB_DEFS.find(x => x.key === activeTab);
      const st = subType !== "all" ? subType : undefined;
      const result = await getLibraryPaged(
        { pageIndex, pageSize: 20 },
        { resourceType: tab?.resourceType, source, keyword, subType: st }
      );
      setItems(result.items);
      setTotal(result.total);
    } catch (error) {
      Toast.error((error as Error).message || t("cozeLibraryQueryFailed"));
      setItems([]);
      setTotal(0);
    } finally {
      setLoading(false);
    }
  }, [activeTab, source, keyword, pageIndex, subType, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const typeLabel = useCallback(
    (rt: LibraryResourceType) => {
      const m: Record<LibraryResourceType, AppMessageKey> = {
        plugin: "cozeLibraryTabPlugin",
        workflow: "cozeLibraryTabWorkflow",
        "knowledge-base": "cozeLibraryTabKnowledge",
        card: "cozeLibraryTabCard",
        prompt: "cozeLibraryTabPrompt",
        database: "cozeLibraryTabDatabase",
        voice: "cozeLibraryTabVoice",
        memory: "cozeLibraryTabMemory",
        agent: "cozeLibraryTypeAgent",
        app: "cozeLibraryTypeApp"
      };
      const key = m[rt];
      return key ? t(key) : String(rt);
    },
    [t]
  );

  const handleOpen = useCallback(
    (record: AiWorkspaceLibraryItem) => {
      switch (record.resourceType) {
        case "workflow":
          navigate(
            record.subType === "chatflow"
              ? chatflowEditorPath(String(record.resourceId))
              : workflowEditorPath(String(record.resourceId))
          );
          return;
        case "plugin":
          navigate(orgWorkspacePluginDetailPath(workspace.orgId, workspace.id, record.resourceId));
          return;
        case "knowledge-base":
          navigate(orgWorkspaceKnowledgeBaseDetailPath(workspace.orgId, workspace.id, record.resourceId));
          return;
        case "database":
          if (record.subType === "datasource") {
            navigate("/settings/system/datasources");
            return;
          }
          navigate(orgWorkspaceDatabaseDetailPath(workspace.orgId, workspace.id, record.resourceId));
          return;
        case "prompt":
          navigate("/ai/prompts");
          return;
        default:
          Toast.info(`${t("cozeLibraryDetailComingSoon")} (${typeLabel(record.resourceType)})`);
      }
    },
    [navigate, t, typeLabel, workspace.id, workspace.orgId]
  );

  const handleSelectCreate = useCallback((rt: LibraryResourceType) => {
    setCreateType(rt);
    setCreateOpen(true);
  }, []);

  const handleDeleteDatabase = useCallback(
    (record: AiWorkspaceLibraryItem) => {
      Modal.confirm({
        title: t("cozeLibraryDeleteDatabaseTitle"),
        content: t("cozeLibraryDeleteDatabaseContent"),
        okType: "danger",
        onOk: async () => {
          if (record.subType === "datasource") {
            await deleteTenantDataSource(record.resourceId);
          } else {
            await deleteAiDatabase(record.resourceId);
          }
          Toast.success(t("cozeLibraryDeleteDatabaseSuccess"));
          await load();
        }
      });
    },
    [load, t]
  );

  const handleOpenMigration = useCallback(
    (record: AiWorkspaceLibraryItem) => {
      if (!hasSetupConsoleToken()) {
        Toast.warning(t("setupConsoleMigrationAuthRequired"));
        return;
      }

      setMigrationSource(record);
    },
    [t]
  );

  const handleOpenStructure = useCallback(
    (record: AiWorkspaceLibraryItem) => {
      navigate(`/space/${workspace.id}/database/${encodeURIComponent(record.resourceId)}/structure`);
    },
    [navigate, workspace.id]
  );

  const canViewDataSources = !permission || permission.hasPermission(APP_PERMISSIONS.DATA_SOURCES_VIEW);

  const renderActionMenu = useCallback(
    (record: AiWorkspaceLibraryItem) => {
      const independentAiDatabase = record.resourceType === "database" && record.subType !== "datasource";
      const structureDisabled = !independentAiDatabase || !canViewDataSources;

      return (
        <Dropdown.Menu>
          <Dropdown.Item onClick={() => handleOpen(record)}>{t("cozeLibraryActionDetail")}</Dropdown.Item>
          <Dropdown.Item disabled>{t("cozeLibraryActionCopyToSpace")}</Dropdown.Item>
          {record.resourceType === "database" ? (
            <Tooltip content={independentAiDatabase ? t("cozeLibraryActionStructure") : t("cozeLibraryActionStructureDatasourceDisabled")}>
              <Dropdown.Item disabled={structureDisabled} onClick={() => handleOpenStructure(record)}>{t("cozeLibraryActionStructure")}</Dropdown.Item>
            </Tooltip>
          ) : null}
          {independentAiDatabase ? (
            <Dropdown.Item onClick={() => handleOpenMigration(record)}>{t("cozeLibraryActionMigrateDatabase")}</Dropdown.Item>
          ) : null}
          <Dropdown.Item disabled>{t("cozeLibraryActionBackup")}</Dropdown.Item>
          {record.resourceType === "database" ? (
            <Dropdown.Item type="danger" onClick={() => handleDeleteDatabase(record)}>
              {t("cozeLibraryActionDelete")}
            </Dropdown.Item>
          ) : (
            <Dropdown.Item disabled>{t("cozeLibraryActionDelete")}</Dropdown.Item>
          )}
        </Dropdown.Menu>
      );
    },
    [canViewDataSources, handleDeleteDatabase, handleOpen, handleOpenMigration, handleOpenStructure, t]
  );

  const columns: ColumnProps<AiWorkspaceLibraryItem>[] = useMemo(
    () => [
      {
        title: t("cozeLibraryColumnResource"),
        dataIndex: "name",
        width: "50%",
        render: (_: unknown, record: AiWorkspaceLibraryItem) => (
          <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
            <div
              style={{
                width: 36,
                height: 36,
                borderRadius: 8,
                background: "var(--semi-color-fill-0)",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                color: "var(--semi-color-primary)"
              }}
            >
              {iconOf(record.resourceType)}
            </div>
            <div style={{ minWidth: 0 }}>
              <div style={{ fontWeight: 500, color: "var(--semi-color-text-0)" }}>{record.name}</div>
              {record.description ? (
                <div
                  style={{
                    color: "var(--semi-color-text-2)",
                    fontSize: 12,
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap",
                    maxWidth: 360
                  }}
                >
                  {record.description}
                </div>
              ) : null}
            </div>
          </div>
        )
      },
      {
        title: t("cozeLibraryColumnType"),
        dataIndex: "resourceType",
        width: 160,
        render: (_: unknown, record: AiWorkspaceLibraryItem) => (
          <Typography.Text type="tertiary">{record.typeLabel ?? typeLabel(record.resourceType)}</Typography.Text>
        )
      },
      {
        title: t("cozeLibraryColumnUpdatedAt"),
        dataIndex: "updatedAt",
        width: 180,
        render: (_: unknown, record: AiWorkspaceLibraryItem) => (
          <Typography.Text type="tertiary">{formatDate(record.updatedAt)}</Typography.Text>
        )
      },
      {
        title: "",
        dataIndex: "_action",
        width: 60,
        render: (_: unknown, record: AiWorkspaceLibraryItem) => (
          <Dropdown trigger="click" position="bottomRight" render={renderActionMenu(record)}>
            <Button
              theme="borderless"
              type="tertiary"
              icon={<IconChevronDown />}
              size="small"
              onClick={event => {
                event.stopPropagation();
              }}
            />
          </Dropdown>
        )
      }
    ],
    [renderActionMenu, t, typeLabel]
  );

  return (
    <div
      className="coze-page coze-library-page"
      data-testid="coze-library-page"
      style={{ padding: 24, display: "flex", flexDirection: "column", gap: 16, height: "100%" }}
    >
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 16 }}>
        <Tabs
          type="button"
          activeKey={activeTab}
          onChange={key => {
            const next = key as LibraryTabKey;
            setActiveTab(next);
            setSubType("all");
            setPageIndex(1);
            const nextParams = new URLSearchParams(searchParams);
            nextParams.delete("subType");
            if (next === "all") {
              nextParams.delete("tab");
            } else {
              nextParams.set("tab", next as WorkspaceLibraryTab);
            }
            setSearchParams(nextParams, { replace: true });
          }}
          tabList={TAB_DEFS.map(x => ({ itemKey: x.key, tab: t(x.labelKey) }))}
        />
        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
          <Input
            prefix={<IconSearch />}
            placeholder={t("cozeLibrarySearchPlaceholder")}
            value={keyword}
            onChange={v => {
              setKeyword(v);
              setPageIndex(1);
            }}
            showClear
            style={{ width: 240 }}
          />
          <Button onClick={() => setImportOpen(true)}>{t("cozeLibraryImport")}</Button>
          <LibraryCreateDropdown onSelectType={handleSelectCreate} />
        </div>
      </div>

      <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
        {subTypeOptions.length > 1 ? (
          <Select
            value={subType}
            onChange={v => {
              const val = v as string;
              setSubType(val);
              setPageIndex(1);
              const nextParams = new URLSearchParams(searchParams);
              if (val === "all") {
                nextParams.delete("subType");
              } else {
                nextParams.set("subType", val);
              }
              setSearchParams(nextParams, { replace: true });
            }}
            style={{ minWidth: 120 }}
            optionList={subTypeOptions}
          />
        ) : (
          <Select
            value="all"
            style={{ minWidth: 120 }}
            optionList={subTypeOptions}
            disabled
          />
        )}
        <Select
          value={source}
          onChange={v => {
            setSource(v as LibrarySource);
            setPageIndex(1);
          }}
          style={{ width: 160 }}
          optionList={[
            { value: "all", label: t("cozeLibrarySourceAll") },
            { value: "official", label: t("cozeLibrarySourceOfficial") },
            { value: "custom", label: t("cozeLibrarySourceCustom") }
          ]}
        />
      </div>

      <div style={{ flex: 1, minHeight: 0, overflow: "auto" }}>
        {loading ? (
          <div style={{ display: "flex", justifyContent: "center", padding: 48 }}>
            <Spin />
          </div>
        ) : items.length === 0 ? (
          <Empty description={t("cozeLibraryEmpty")} style={{ padding: 48 }} />
        ) : (
          <Table
            columns={columns}
            dataSource={items}
            rowKey={record => `${record.resourceType}-${record.resourceId}`}
            onRow={record => ({
              onClick: () => handleOpen(record),
              style: { cursor: "pointer" }
            })}
            pagination={{
              currentPage: pageIndex,
              pageSize: 20,
              total,
              onPageChange: p => setPageIndex(p)
            }}
          />
        )}
      </div>

      <LibraryImportModal
        visible={importOpen}
        onClose={() => setImportOpen(false)}
        onImported={() => {
          setImportOpen(false);
          void load();
        }}
      />

      <LibraryCreateModal
        visible={createOpen}
        createType={createType}
        onClose={() => {
          setCreateOpen(false);
          setCreateType(null);
        }}
        onCreated={() => {
          setCreateOpen(false);
          setCreateType(null);
          void load();
        }}
      />

      <MigrationWizardDrawer
        visible={Boolean(migrationSource)}
        source={migrationSource}
        onClose={() => setMigrationSource(null)}
        onTargetCreated={() => void load()}
      />
    </div>
  );
}
