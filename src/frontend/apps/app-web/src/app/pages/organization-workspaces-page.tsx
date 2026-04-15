import { useDeferredValue, useMemo } from "react";
import { Button, Empty, Input, Modal, Skeleton, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import { useState } from "react";
import type {
  WorkspaceCreateRequest,
  WorkspaceSummaryDto,
  WorkspaceUpdateRequest
} from "@/services/api-org-workspaces";
import { useAppI18n } from "../i18n";

interface OrganizationWorkspacesPageProps {
  loading: boolean;
  canManage: boolean;
  saving: boolean;
  deletingWorkspaceId: string | null;
  keyword: string;
  items: WorkspaceSummaryDto[];
  onKeywordChange: (value: string) => void;
  onOpenWorkspace: (workspaceId: string) => void;
  onCreateWorkspace: (request: WorkspaceCreateRequest) => Promise<void>;
  onUpdateWorkspace: (workspaceId: string, request: WorkspaceUpdateRequest) => Promise<void>;
  onDeleteWorkspace: (workspaceId: string) => Promise<void>;
}

export function OrganizationWorkspacesPage({
  loading,
  canManage,
  saving,
  deletingWorkspaceId,
  keyword,
  items,
  onKeywordChange,
  onOpenWorkspace,
  onCreateWorkspace,
  onUpdateWorkspace,
  onDeleteWorkspace
}: OrganizationWorkspacesPageProps) {
  const { t } = useAppI18n();
  const deferredKeyword = useDeferredValue(keyword.trim().toLowerCase());
  const [createVisible, setCreateVisible] = useState(false);
  const [editTarget, setEditTarget] = useState<WorkspaceSummaryDto | null>(null);
  const [archiveTarget, setArchiveTarget] = useState<WorkspaceSummaryDto | null>(null);
  const [createName, setCreateName] = useState("");
  const [createDescription, setCreateDescription] = useState("");
  const [createIcon, setCreateIcon] = useState("");
  const [createAppInstanceId, setCreateAppInstanceId] = useState("");
  const [editName, setEditName] = useState("");
  const [editDescription, setEditDescription] = useState("");
  const [editIcon, setEditIcon] = useState("");

  const filteredItems = useMemo(() => {
    if (!deferredKeyword) {
      return items;
    }

    return items.filter(item => {
      const haystack = `${item.name} ${item.description ?? ""} ${item.appKey}`.toLowerCase();
      return haystack.includes(deferredKeyword);
    });
  }, [deferredKeyword, items]);

  const resetCreateDialog = () => {
    setCreateName("");
    setCreateDescription("");
    setCreateIcon("");
    setCreateAppInstanceId("");
  };

  const openEditDialog = (item: WorkspaceSummaryDto) => {
    setEditTarget(item);
    setEditName(item.name);
    setEditDescription(item.description ?? "");
    setEditIcon(item.icon ?? "");
  };

  return (
    <div className="atlas-workspaces-page" data-testid="workspace-list-page">
      <section className="atlas-workspaces-hero">
        <div className="atlas-workspaces-hero__copy">
          <span className="atlas-workspaces-hero__kicker">{t("workspaceListKicker")}</span>
          <Typography.Title heading={2} style={{ margin: 0 }}>
            {t("workspaceListTitle")}
          </Typography.Title>
          <Typography.Text type="tertiary">
            {t("workspaceListSubtitle")}
          </Typography.Text>
        </div>

        <div className="atlas-workspaces-hero__actions">
          <Input
            value={keyword}
            onChange={onKeywordChange}
            showClear
            placeholder={t("workspaceListSearchPlaceholder")}
            data-testid="workspace-list-search"
          />
          {canManage ? (
            <Button type="primary" theme="solid" loading={saving} onClick={() => setCreateVisible(true)}>
              {t("workspaceListCreate")}
            </Button>
          ) : null}
        </div>
      </section>

      {loading ? (
        <div className="atlas-workspaces-grid">
          {Array.from({ length: 3 }).map((_, index) => (
            <article key={index} className="atlas-workspace-card atlas-workspace-card--loading">
              <Skeleton placeholder={<Skeleton.Title style={{ width: "56%" }} />} loading active />
              <Skeleton placeholder={<Skeleton.Paragraph rows={3} />} loading active />
            </article>
          ))}
        </div>
      ) : filteredItems.length === 0 ? (
        <div className="atlas-workspaces-empty">
          <Empty description={t("workspaceListEmpty")} />
        </div>
      ) : (
        <div className="atlas-workspaces-grid">
          {filteredItems.map(item => (
            <article key={item.id} className="atlas-workspace-card">
              <div className="atlas-workspace-card__head">
                <div>
                  <Tag color="blue">{t("workspaceListWorkspaceTag")}</Tag>
                  <Typography.Title heading={5} style={{ margin: "10px 0 0" }}>
                    {item.name}
                  </Typography.Title>
                </div>
                <Tag color="light-blue">{item.roleCode}</Tag>
              </div>

              <Typography.Text type="tertiary" className="atlas-workspace-card__description">
                {item.description || t("workspaceListDescriptionFallback")}
              </Typography.Text>

              <div className="atlas-workspace-card__meta">
                <span>{t("workspaceListAppCount")}: {item.appCount}</span>
                <span>{t("workspaceListAgentCount")}: {item.agentCount}</span>
                <span>{t("workspaceListWorkflowCount")}: {item.workflowCount}</span>
              </div>

              <div className="atlas-workspace-card__footer">
                <div className="atlas-workspace-card__caption">
                  <strong>{item.appKey}</strong>
                  <span>{item.lastVisitedAt ? t("workspaceListVisited") : t("workspaceListCreated")}</span>
                </div>
                <div className="atlas-workspace-card__actions">
                  {canManage ? (
                    <Button
                      theme="light"
                      onClick={() => openEditDialog(item)}
                      disabled={saving || deletingWorkspaceId === item.id}
                    >
                      {t("workspaceListEdit")}
                    </Button>
                  ) : null}
                  {canManage ? (
                    <Button
                      type="danger"
                      theme="borderless"
                      loading={deletingWorkspaceId === item.id}
                      disabled={saving}
                      onClick={() => setArchiveTarget(item)}
                    >
                      {t("workspaceListArchive")}
                    </Button>
                  ) : null}
                  <Button
                    type="primary"
                    theme="solid"
                    onClick={() => onOpenWorkspace(item.id)}
                    data-testid={`workspace-open-${item.id}`}
                  >
                    {t("workspaceListOpen")}
                  </Button>
                </div>
              </div>
            </article>
          ))}
        </div>
      )}

      <Modal
        title={t("workspaceListCreateDialogTitle")}
        visible={createVisible}
        onCancel={() => {
          setCreateVisible(false);
          resetCreateDialog();
        }}
        onOk={() => {
          void onCreateWorkspace({
            name: createName.trim(),
            description: createDescription.trim() || undefined,
            icon: createIcon.trim() || undefined,
            appInstanceId: createAppInstanceId.trim()
          }).then(() => {
            Toast.success(t("workspaceListCreatedSuccess"));
            setCreateVisible(false);
            resetCreateDialog();
          }).catch(() => undefined);
        }}
        okButtonProps={{ disabled: !createName.trim() || !createAppInstanceId.trim(), loading: saving }}
      >
        <div className="atlas-develop-dialog">
          <Input value={createName} onChange={setCreateName} placeholder={t("workspaceListNamePlaceholder")} />
          <Input value={createDescription} onChange={setCreateDescription} placeholder={t("workspaceListDescriptionPlaceholder")} />
          <Input value={createIcon} onChange={setCreateIcon} placeholder={t("workspaceListIconPlaceholder")} />
          <Input value={createAppInstanceId} onChange={setCreateAppInstanceId} placeholder={t("workspaceListAppInstancePlaceholder")} />
        </div>
      </Modal>

      <Modal
        title={t("workspaceListEditDialogTitle")}
        visible={editTarget !== null}
        onCancel={() => {
          setEditTarget(null);
          setEditName("");
          setEditDescription("");
          setEditIcon("");
        }}
        onOk={() => {
          if (!editTarget) {
            return;
          }

          void onUpdateWorkspace(editTarget.id, {
            name: editName.trim(),
            description: editDescription.trim() || undefined,
            icon: editIcon.trim() || undefined
          }).then(() => {
            Toast.success(t("workspaceListUpdatedSuccess"));
            setEditTarget(null);
            setEditName("");
            setEditDescription("");
            setEditIcon("");
          }).catch(() => undefined);
        }}
        okButtonProps={{ disabled: !editName.trim(), loading: saving }}
      >
        <div className="atlas-develop-dialog">
          <Input value={editName} onChange={setEditName} placeholder={t("workspaceListNamePlaceholder")} />
          <Input value={editDescription} onChange={setEditDescription} placeholder={t("workspaceListDescriptionPlaceholder")} />
          <Input value={editIcon} onChange={setEditIcon} placeholder={t("workspaceListIconPlaceholder")} />
        </div>
      </Modal>

      <Modal
        title={t("workspaceListArchiveConfirmTitle")}
        visible={archiveTarget !== null}
        onCancel={() => setArchiveTarget(null)}
        onOk={() => {
          if (!archiveTarget) {
            return;
          }

          void onDeleteWorkspace(archiveTarget.id).then(() => {
            Toast.success(t("workspaceListArchivedSuccess"));
            setArchiveTarget(null);
          }).catch(() => undefined);
        }}
        okButtonProps={{
          type: "danger",
          theme: "solid",
          loading: archiveTarget ? deletingWorkspaceId === archiveTarget.id : false
        }}
      >
        <Typography.Text>
          {t("workspaceListArchiveConfirmContent").replace("{workspace}", archiveTarget?.name ?? "")}
        </Typography.Text>
      </Modal>
    </div>
  );
}
