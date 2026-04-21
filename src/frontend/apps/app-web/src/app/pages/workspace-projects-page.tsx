import { useEffect, useMemo, useState } from "react";
import { Avatar, Button, Dropdown, Empty, Input, Modal, Select, Spin, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import { IconFolder, IconMore, IconPlus } from "@douyinfe/semi-icons";
import {
  appEditorPath,
  agentEditorPath,
  workspaceProjectsFolderPath,
  workspaceProjectsPath
} from "@atlas/app-shell-shared";
import { useNavigate, useParams } from "react-router-dom";
import { useAppI18n } from "../i18n";
import type { AppMessageKey } from "../messages";
import { useWorkspaceContext } from "../workspace-context";
import { CreateFolderModal } from "../components/create-folder-modal";
import { GlobalCreateModal } from "../components/global-create-modal";
import {
  copyWorkspaceIdeResourceToWorkspace,
  deleteWorkspaceIdeResource,
  duplicateWorkspaceIdeResource,
  getWorkspaceIdeResources,
  migrateWorkspaceIdeResource,
  updateWorkspaceIdeFavorite,
  type WorkspaceIdeResourceCardDto
} from "../../services/api-workspace-ide";
import { getWorkspaces, type WorkspaceSummaryDto } from "../../services/api-org-workspaces";
import { createFolder, listFolders, moveItemToFolder, type FolderListItem } from "../../services/mock";

type ProjectsResourceTypeFilter = "all" | "agent" | "app";
type ProjectsStatusFilter = "all" | "draft" | "published" | "archived";
type WorkspaceActionMode = "migrate" | "copy";

type ProjectsResourceCard = WorkspaceIdeResourceCardDto & {
  resourceType: "agent" | "app";
};

interface MoveDialogState {
  resource: ProjectsResourceCard;
  folderKeyword: string;
  selectedFolderId: string;
  submitting: boolean;
}

interface WorkspaceDialogState {
  resource: ProjectsResourceCard;
  mode: WorkspaceActionMode;
  workspaceKeyword: string;
  selectedWorkspaceId: string;
  submitting: boolean;
}

export function WorkspaceProjectsPage() {
  const { t } = useAppI18n();
  const workspace = useWorkspaceContext();
  const { folderId } = useParams<{ folderId?: string }>();
  const navigate = useNavigate();

  const [keyword, setKeyword] = useState("");
  const [resourceTypeFilter, setResourceTypeFilter] = useState<ProjectsResourceTypeFilter>("all");
  const [statusFilter, setStatusFilter] = useState<ProjectsStatusFilter>("all");
  const [loading, setLoading] = useState(true);
  const [resources, setResources] = useState<ProjectsResourceCard[]>([]);
  const [folders, setFolders] = useState<FolderListItem[]>([]);
  const [actionLoadingKey, setActionLoadingKey] = useState<string | null>(null);
  const [globalCreateOpen, setGlobalCreateOpen] = useState(false);
  const [createFolderOpen, setCreateFolderOpen] = useState(false);
  const [moveDialog, setMoveDialog] = useState<MoveDialogState | null>(null);
  const [workspaceDialog, setWorkspaceDialog] = useState<WorkspaceDialogState | null>(null);
  const [workspaceOptions, setWorkspaceOptions] = useState<WorkspaceSummaryDto[]>([]);
  const [workspaceOptionsLoading, setWorkspaceOptionsLoading] = useState(false);

  const normalizedKeyword = keyword.trim();
  const projectsRootPath = workspaceProjectsPath(workspace.id);

  const currentFolder = useMemo(
    () => folders.find(item => item.id === folderId),
    [folderId, folders]
  );

  const visibleFolders = useMemo(() => {
    if (folderId) {
      return [];
    }
    if (!normalizedKeyword) {
      return folders;
    }
    const lowered = normalizedKeyword.toLowerCase();
    return folders.filter(item => item.name.toLowerCase().includes(lowered));
  }, [folderId, folders, normalizedKeyword]);

  const filteredWorkspaceOptions = useMemo(() => {
    if (!workspaceDialog) {
      return workspaceOptions;
    }
    const filterText = workspaceDialog.workspaceKeyword.trim().toLowerCase();
    if (!filterText) {
      return workspaceOptions;
    }
    return workspaceOptions.filter(item => item.name.toLowerCase().includes(filterText));
  }, [workspaceDialog, workspaceOptions]);

  const filteredMoveFolders = useMemo(() => {
    if (!moveDialog) {
      return folders;
    }
    const filterText = moveDialog.folderKeyword.trim().toLowerCase();
    if (!filterText) {
      return folders;
    }
    return folders.filter(item => item.name.toLowerCase().includes(filterText));
  }, [folders, moveDialog]);

  const loadData = async () => {
    if (!workspace.id) {
      return;
    }
    setLoading(true);
    try {
      const [resourceResult, folderResult] = await Promise.all([
        getWorkspaceIdeResources({
          pageIndex: 1,
          pageSize: 120,
          keyword: normalizedKeyword || undefined,
          resourceType: resourceTypeFilter === "all" ? undefined : resourceTypeFilter,
          status: statusFilter === "all" ? undefined : statusFilter,
          folderId: folderId || undefined,
          workspaceId: workspace.id
        }),
        listFolders(workspace.id, { pageIndex: 1, pageSize: 200 })
      ]);

      const projectResources = resourceResult.items.filter(isProjectsResourceCard);
      setResources(projectResources);
      setFolders(folderResult.items);
    } catch (error) {
      Toast.error((error as Error).message || t("cozeCreateFailed"));
      setResources([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadData();
  }, [workspace.id, normalizedKeyword, resourceTypeFilter, statusFilter, folderId]);

  const ensureWorkspaceOptions = async () => {
    const orgId = workspace.orgId || getTenantId() || "";
    if (!orgId || !workspace.id) {
      return [];
    }
    setWorkspaceOptionsLoading(true);
    try {
      const items = await getWorkspaces(orgId);
      const filteredItems = items.filter(item => item.id !== workspace.id);
      setWorkspaceOptions(filteredItems);
      return filteredItems;
    } catch (error) {
      Toast.error((error as Error).message || t("cozeCreateFailed"));
      setWorkspaceOptions([]);
      return [];
    } finally {
      setWorkspaceOptionsLoading(false);
    }
  };

  const handleOpenFolder = (targetFolderId: string) => {
    const targetPath = workspaceProjectsFolderPath(workspace.id, targetFolderId);
    navigate(targetPath);
  };

  const handleOpenResource = (item: ProjectsResourceCard) => {
    if (item.resourceType === "agent") {
      navigate(agentEditorPath(item.resourceId));
      return;
    }
    navigate(appEditorPath(item.resourceId));
  };

  const handleToggleFavorite = async (item: ProjectsResourceCard) => {
    const actionKey = `${item.resourceType}-${item.resourceId}-favorite`;
    setActionLoadingKey(actionKey);
    try {
      await updateWorkspaceIdeFavorite(item.resourceType, item.resourceId, !item.isFavorite);
      setResources(previous =>
        previous.map(resource =>
          resource.resourceId === item.resourceId && resource.resourceType === item.resourceType
            ? { ...resource, isFavorite: !resource.isFavorite }
            : resource
        )
      );
    } catch (error) {
      Toast.error((error as Error).message || t("cozeCreateFailed"));
    } finally {
      setActionLoadingKey(null);
    }
  };

  const handleDuplicate = async (item: ProjectsResourceCard) => {
    const actionKey = `${item.resourceType}-${item.resourceId}-duplicate`;
    setActionLoadingKey(actionKey);
    try {
      await duplicateWorkspaceIdeResource(item.resourceType, item.resourceId, {
        workspaceId: workspace.id,
        folderId: folderId || item.folderId
      });
      Toast.success(t("cozeProjectsMenuDuplicateSuccess"));
      await loadData();
    } catch (error) {
      Toast.error((error as Error).message || t("cozeCreateFailed"));
    } finally {
      setActionLoadingKey(null);
    }
  };

  const openMoveDialog = (item: ProjectsResourceCard) => {
    setMoveDialog({
      resource: item,
      folderKeyword: "",
      selectedFolderId: item.folderId ?? "",
      submitting: false
    });
  };

  const handleMoveConfirm = async () => {
    if (!moveDialog || !workspace.id) {
      return;
    }
    if (!moveDialog.selectedFolderId) {
      Toast.warning(t("cozeProjectsMoveSelectFolderRequired"));
      return;
    }

    setMoveDialog(prev => (prev ? { ...prev, submitting: true } : prev));
    try {
      await moveItemToFolder(workspace.id, moveDialog.selectedFolderId, {
        itemType: moveDialog.resource.resourceType,
        itemId: moveDialog.resource.resourceId
      });
      Toast.success(t("cozeProjectsMenuMoveSuccess"));
      setMoveDialog(null);
      await loadData();
    } catch (error) {
      Toast.error((error as Error).message || t("cozeCreateFailed"));
      setMoveDialog(prev => (prev ? { ...prev, submitting: false } : prev));
    }
  };

  const openWorkspaceActionDialog = async (item: ProjectsResourceCard, mode: WorkspaceActionMode) => {
    const options = await ensureWorkspaceOptions();
    setWorkspaceDialog({
      resource: item,
      mode,
      workspaceKeyword: "",
      selectedWorkspaceId: options[0]?.id ?? "",
      submitting: false
    });
  };

  const handleWorkspaceActionConfirm = async () => {
    if (!workspaceDialog) {
      return;
    }

    if (!workspaceDialog.selectedWorkspaceId) {
      Toast.warning(t("cozeProjectsWorkspaceSelectRequired"));
      return;
    }

    setWorkspaceDialog(prev => (prev ? { ...prev, submitting: true } : prev));
    try {
      if (workspaceDialog.mode === "migrate") {
        await migrateWorkspaceIdeResource(workspaceDialog.resource.resourceType, workspaceDialog.resource.resourceId, {
          sourceWorkspaceId: workspace.id,
          targetWorkspaceId: workspaceDialog.selectedWorkspaceId
        });
        Toast.success(t("cozeProjectsMenuMigrateSuccess"));
      } else {
        await copyWorkspaceIdeResourceToWorkspace(workspaceDialog.resource.resourceType, workspaceDialog.resource.resourceId, {
          sourceWorkspaceId: workspace.id,
          targetWorkspaceId: workspaceDialog.selectedWorkspaceId
        });
        Toast.success(t("cozeProjectsMenuCopyWorkspaceSuccess"));
      }
      setWorkspaceDialog(null);
      await loadData();
    } catch (error) {
      Toast.error((error as Error).message || t("cozeCreateFailed"));
      setWorkspaceDialog(prev => (prev ? { ...prev, submitting: false } : prev));
    }
  };

  const handleDelete = (item: ProjectsResourceCard) => {
    Modal.confirm({
      title: t("cozeProjectsDeleteConfirmTitle"),
      content: t("cozeProjectsDeleteConfirmMessage").replace("{name}", item.name),
      okType: "danger",
      okText: t("cozeProjectsMenuDelete"),
      cancelText: t("cozeCommonGoBack"),
      onOk: async () => {
        await deleteWorkspaceIdeResource(item.resourceType, item.resourceId);
        Toast.success(t("cozeProjectsMenuDeleteSuccess"));
        await loadData();
      }
    });
  };

  const handleMoveModalCreateFolder = async () => {
    if (!workspace.id) {
      return;
    }
    const folderName = `${t("cozeProjectsMoveDefaultFolderName")} ${Date.now().toString().slice(-4)}`;
    try {
      const result = await createFolder(workspace.id, { name: folderName });
      await loadData();
      setMoveDialog(previous => previous ? { ...previous, selectedFolderId: result.folderId } : previous);
      Toast.success(t("cozeCreateSuccess"));
    } catch (error) {
      Toast.error((error as Error).message || t("cozeCreateFailed"));
    }
  };

  return (
    <div className="coze-page coze-projects-page" data-testid="coze-projects-page">
      <header className="coze-page__header coze-projects-page__header">
        <div className="coze-projects-page__title-wrap">
          <Typography.Title heading={3} style={{ margin: 0 }}>
            {t("cozeProjectsTitle")}
          </Typography.Title>
          {folderId ? (
            <div className="coze-projects-page__breadcrumb" data-testid="coze-projects-breadcrumb">
              <button
                type="button"
                className="coze-projects-page__breadcrumb-link"
                onClick={() => navigate(projectsRootPath)}
              >
                {t("cozeProjectsTitle")}
              </button>
              <span className="coze-projects-page__breadcrumb-divider">›</span>
              <span>{currentFolder?.name ?? folderId}</span>
            </div>
          ) : null}
        </div>
        <div className="coze-projects-page__actions">
          <Input
            value={keyword}
            onChange={value => setKeyword(value)}
            placeholder={t("cozeProjectsSearchPlaceholder")}
            showClear
            style={{ width: 290 }}
          />
          <Button icon={<IconPlus />} onClick={() => setCreateFolderOpen(true)} data-testid="coze-projects-create-folder">
            {t("cozeProjectsCreateFolder")}
          </Button>
          <Button
            theme="solid"
            type="primary"
            icon={<IconPlus />}
            onClick={() => setGlobalCreateOpen(true)}
            data-testid="coze-projects-create-project"
          >
            {t("cozeProjectsCreateProject")}
          </Button>
        </div>
      </header>

      {!folderId ? (
        <section className="coze-projects-page__folder-section">
          {visibleFolders.length > 0 ? (
            <div className="coze-projects-folder-list">
              {visibleFolders.map(item => (
                <button
                  key={item.id}
                  type="button"
                  className="coze-projects-folder-item"
                  onClick={() => handleOpenFolder(item.id)}
                  data-testid={`coze-projects-folder-${item.id}`}
                >
                  <div className="coze-projects-folder-item__left">
                    <IconFolder size="large" />
                    <span>{item.name}</span>
                  </div>
                  <IconMore />
                </button>
              ))}
            </div>
          ) : (
            <div className="coze-projects-page__folder-empty">{t("cozeProjectsFoldersEmpty")}</div>
          )}
        </section>
      ) : null}

      <section className="coze-projects-page__projects-header">
        <Typography.Title heading={5} style={{ margin: 0 }}>
          {t("cozeProjectsSectionTitle")}
        </Typography.Title>
      </section>

      <section className="coze-page__toolbar">
        <Select
          style={{ width: 160 }}
          value={resourceTypeFilter}
          optionList={[
            { label: t("cozeProjectsFilterTypeAll"), value: "all" },
            { label: t("cozeProjectsFilterTypeAgent"), value: "agent" },
            { label: t("cozeProjectsFilterTypeApp"), value: "app" }
          ]}
          onChange={value => setResourceTypeFilter((value as ProjectsResourceTypeFilter) ?? "all")}
        />
        <Select
          style={{ width: 160 }}
          value={statusFilter}
          optionList={[
            { label: t("cozeProjectsFilterStatusAll"), value: "all" },
            { label: t("cozeProjectsFilterStatusDraft"), value: "draft" },
            { label: t("cozeProjectsFilterStatusPublished"), value: "published" },
            { label: t("cozeProjectsFilterStatusArchived"), value: "archived" }
          ]}
          onChange={value => setStatusFilter((value as ProjectsStatusFilter) ?? "all")}
        />
      </section>

      <section className="coze-page__body">
        {loading ? (
          <div className="coze-page__loading">
            <Spin />
          </div>
        ) : resources.length === 0 ? (
          <Empty title={t("cozeProjectsEmptyTitle")} description={t("cozeProjectsEmptyTip")} />
        ) : (
          <div className="coze-projects-resource-grid">
            {resources.map(item => {
              const actionPrefix = `${item.resourceType}-${item.resourceId}`;
              const menuItems = [
                {
                  node: "item" as const,
                  key: `${actionPrefix}-duplicate`,
                  name: t("cozeProjectsMenuDuplicate"),
                  onClick: () => {
                    void handleDuplicate(item);
                  }
                },
                {
                  node: "item" as const,
                  key: `${actionPrefix}-move`,
                  name: t("cozeProjectsMenuMove"),
                  onClick: () => openMoveDialog(item)
                },
                {
                  node: "item" as const,
                  key: `${actionPrefix}-migrate`,
                  name: t("cozeProjectsMenuMigrate"),
                  onClick: () => {
                    void openWorkspaceActionDialog(item, "migrate");
                  }
                },
                {
                  node: "item" as const,
                  key: `${actionPrefix}-copy`,
                  name: t("cozeProjectsMenuCopyToWorkspace"),
                  onClick: () => {
                    void openWorkspaceActionDialog(item, "copy");
                  }
                },
                {
                  node: "item" as const,
                  key: `${actionPrefix}-delete`,
                  type: "danger" as const,
                  name: t("cozeProjectsMenuDelete"),
                  onClick: () => handleDelete(item)
                }
              ];

              return (
                <article
                  key={`${item.resourceType}-${item.resourceId}`}
                  className="coze-projects-resource-card"
                  data-testid={`coze-project-card-${item.resourceType}-${item.resourceId}`}
                >
                  <button
                    type="button"
                    className="coze-projects-resource-card__main"
                    onClick={() => handleOpenResource(item)}
                  >
                    <div className="coze-projects-resource-card__head">
                      <div className="coze-projects-resource-card__text">
                        <strong>{item.name}</strong>
                        <span>{item.description || "—"}</span>
                      </div>
                      <Avatar size="medium" color={item.resourceType === "app" ? "orange" : "light-blue"}>
                        {(item.name || "R").slice(0, 1).toUpperCase()}
                      </Avatar>
                    </div>
                  </button>

                  <div className="coze-projects-resource-card__meta-row">
                    {renderCardTags(item, t)}
                  </div>

                  <div className="coze-projects-resource-card__owner">
                    {item.ownerDisplayName || "RootUser"} · {t("cozeProjectsEditedBy")} {item.lastEditedByDisplayName || item.ownerDisplayName || "RootUser"} · {t("cozeProjectsEditedAt")} {formatShortDateTime(item.lastEditedAt || item.updatedAt)}
                  </div>

                  <div className="coze-projects-resource-card__actions">
                    <Button
                      size="small"
                      loading={actionLoadingKey === `${actionPrefix}-favorite`}
                      onClick={() => {
                        void handleToggleFavorite(item);
                      }}
                    >
                      {item.isFavorite ? "★" : "☆"}
                    </Button>
                    <Dropdown trigger="click" position="bottomRight" menu={menuItems}>
                      <Button icon={<IconMore />} size="small" />
                    </Dropdown>
                  </div>
                </article>
              );
            })}
          </div>
        )}
      </section>

      <GlobalCreateModal
        visible={globalCreateOpen}
        workspaceId={workspace.id}
        onClose={() => {
          setGlobalCreateOpen(false);
          void loadData();
        }}
      />
      <CreateFolderModal
        visible={createFolderOpen}
        workspaceId={workspace.id}
        onClose={() => {
          setCreateFolderOpen(false);
          void loadData();
        }}
      />

      <Modal
        title={t("cozeProjectsMoveModalTitle")}
        visible={Boolean(moveDialog)}
        width={980}
        onCancel={() => setMoveDialog(null)}
        onOk={() => void handleMoveConfirm()}
        okText={t("homeEnter")}
        confirmLoading={Boolean(moveDialog?.submitting)}
      >
        <div className="coze-projects-move-modal">
          <Input
            value={moveDialog?.folderKeyword ?? ""}
            onChange={value => setMoveDialog(previous => previous ? { ...previous, folderKeyword: value } : previous)}
            placeholder={t("cozeProjectsMoveSearchPlaceholder")}
            showClear
          />
          <div className="coze-projects-move-modal__tree">
            <div className="coze-projects-move-modal__root">{t("cozeProjectsTitle")}</div>
            {filteredMoveFolders.map(item => (
              <button
                key={item.id}
                type="button"
                className={`coze-projects-move-modal__folder${moveDialog?.selectedFolderId === item.id ? " is-selected" : ""}`}
                onClick={() => setMoveDialog(previous => previous ? { ...previous, selectedFolderId: item.id } : previous)}
              >
                <IconFolder size="small" />
                <span>{item.name}</span>
              </button>
            ))}
            <button
              type="button"
              className="coze-projects-move-modal__create"
              onClick={() => {
                void handleMoveModalCreateFolder();
              }}
            >
              <IconPlus size="small" />
              {t("cozeProjectsMoveCreateFolder")}
            </button>
          </div>
        </div>
      </Modal>

      <Modal
        title={workspaceDialog?.mode === "migrate" ? t("cozeProjectsMenuMigrate") : t("cozeProjectsMenuCopyToWorkspace")}
        visible={Boolean(workspaceDialog)}
        width={780}
        onCancel={() => setWorkspaceDialog(null)}
        onOk={() => void handleWorkspaceActionConfirm()}
        okText={t("homeEnter")}
        confirmLoading={Boolean(workspaceDialog?.submitting)}
      >
        <div className="coze-projects-workspace-modal">
          <Input
            value={workspaceDialog?.workspaceKeyword ?? ""}
            onChange={value => setWorkspaceDialog(previous => previous ? { ...previous, workspaceKeyword: value } : previous)}
            placeholder={t("cozeProjectsWorkspaceSearchPlaceholder")}
            showClear
          />
          <div className="coze-projects-workspace-modal__list">
            {workspaceOptionsLoading ? (
              <div className="coze-page__loading">
                <Spin size="small" />
              </div>
            ) : filteredWorkspaceOptions.length === 0 ? (
              <Empty title={t("cozeProjectsWorkspaceEmptyTitle")} description={t("cozeProjectsWorkspaceEmptyTip")} />
            ) : (
              filteredWorkspaceOptions.map(item => (
                <button
                  key={item.id}
                  type="button"
                  className={`coze-projects-workspace-modal__item${workspaceDialog?.selectedWorkspaceId === item.id ? " is-selected" : ""}`}
                  onClick={() => setWorkspaceDialog(previous => previous ? { ...previous, selectedWorkspaceId: item.id } : previous)}
                >
                  <span className="coze-projects-workspace-modal__name">{item.name}</span>
                  <span className="coze-projects-workspace-modal__meta">{item.id}</span>
                </button>
              ))
            )}
          </div>
        </div>
      </Modal>
    </div>
  );
}

function isProjectsResourceCard(item: WorkspaceIdeResourceCardDto): item is ProjectsResourceCard {
  return item.resourceType === "agent" || item.resourceType === "app";
}

function renderCardTags(item: ProjectsResourceCard, t: (key: AppMessageKey) => string) {
  if (item.resourceType === "agent") {
    return <Tag size="small">{t("cozeProjectsFilterTypeAgent")}</Tag>;
  }
  return (
    <>
      <Tag size="small">{t("cozeProjectsFilterTypeApp")}</Tag>
      <Tag size="small" color="blue">
        {t("cozeProjectsTagLowcode")}
      </Tag>
    </>
  );
}

function formatShortDateTime(value?: string): string {
  if (!value) {
    return "--";
  }
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }
  const month = String(parsed.getMonth() + 1).padStart(2, "0");
  const day = String(parsed.getDate()).padStart(2, "0");
  const hours = String(parsed.getHours()).padStart(2, "0");
  const minutes = String(parsed.getMinutes()).padStart(2, "0");
  return `${month}-${day} ${hours}:${minutes}`;
}
