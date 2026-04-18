import { useDeferredValue, useMemo, useState } from "react";
import { Button, Card, Empty, Input, Modal, Skeleton, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import type {
  WorkspaceAppInstanceCreateRequest,
  WorkspaceAppInstanceDto,
  WorkspaceCreateRequest,
  WorkspaceSummaryDto,
  WorkspaceUpdateRequest
} from "../../services/api-org-workspaces";
import { useAppI18n } from "../i18n";

const { Title, Text } = Typography;

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
  /** 1→N 模型：在已有工作空间内创建一个新的应用实例（AppManifest）。 */
  onCreateAppInstance?: (
    workspaceId: string,
    request: WorkspaceAppInstanceCreateRequest
  ) => Promise<WorkspaceAppInstanceDto>;
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
  onDeleteWorkspace,
  onCreateAppInstance
}: OrganizationWorkspacesPageProps) {
  const { t } = useAppI18n();
  const deferredKeyword = useDeferredValue(keyword.trim().toLowerCase());
  const [createVisible, setCreateVisible] = useState(false);
  const [editTarget, setEditTarget] = useState<WorkspaceSummaryDto | null>(null);
  const [archiveTarget, setArchiveTarget] = useState<WorkspaceSummaryDto | null>(null);
  const [createName, setCreateName] = useState("");
  const [createDescription, setCreateDescription] = useState("");
  const [createIcon, setCreateIcon] = useState("");
  const [editName, setEditName] = useState("");
  const [editDescription, setEditDescription] = useState("");
  const [editIcon, setEditIcon] = useState("");

  // 1→N 模型：「在工作空间内创建应用实例」的对话框状态
  const [appInstanceTarget, setAppInstanceTarget] = useState<WorkspaceSummaryDto | null>(null);
  const [appInstanceName, setAppInstanceName] = useState("");
  const [appInstanceDescription, setAppInstanceDescription] = useState("");
  const [appInstanceAppKey, setAppInstanceAppKey] = useState("");
  const [appInstanceSaving, setAppInstanceSaving] = useState(false);
  const resetAppInstanceDialog = () => {
    setAppInstanceTarget(null);
    setAppInstanceName("");
    setAppInstanceDescription("");
    setAppInstanceAppKey("");
    setAppInstanceSaving(false);
  };

  const filteredItems = useMemo(() => {
    if (!deferredKeyword) {
      return items;
    }

    return items.filter(item => {
      const haystack = `${item.name} ${item.description ?? ""} ${item.appKey ?? ""}`.toLowerCase();
      return haystack.includes(deferredKeyword);
    });
  }, [deferredKeyword, items]);

  const resetCreateDialog = () => {
    setCreateName("");
    setCreateDescription("");
    setCreateIcon("");
  };

  const openEditDialog = (item: WorkspaceSummaryDto) => {
    setEditTarget(item);
    setEditName(item.name);
    setEditDescription(item.description ?? "");
    setEditIcon(item.icon ?? "");
  };

  return (
    <div data-testid="workspace-list-page" style={{ display: "flex", flexDirection: "column", gap: 16, padding: 16 }}>
      <Card bodyStyle={{ padding: 24 }}>
        <div
          style={{
            display: "flex",
            alignItems: "flex-start",
            justifyContent: "space-between",
            gap: 16,
            flexWrap: "wrap"
          }}
        >
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            <Text type="tertiary" style={{ textTransform: "uppercase", letterSpacing: "0.08em", fontSize: 12 }}>
              {t("workspaceListKicker")}
            </Text>
            <Title heading={2} style={{ margin: 0 }}>
              {t("workspaceListTitle")}
            </Title>
            <Text type="tertiary">{t("workspaceListSubtitle")}</Text>
          </div>

          <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
            <Input
              value={keyword}
              onChange={onKeywordChange}
              showClear
              placeholder={t("workspaceListSearchPlaceholder")}
              data-testid="workspace-list-search"
              style={{ width: 240 }}
            />
            {canManage ? (
              <Button type="primary" theme="solid" loading={saving} onClick={() => setCreateVisible(true)}>
                {t("workspaceListCreate")}
              </Button>
            ) : null}
          </div>
        </div>
      </Card>

      {loading ? (
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))",
            gap: 16
          }}
        >
          {Array.from({ length: 3 }).map((_, index) => (
            <Card key={index} bodyStyle={{ padding: 24 }}>
              <Skeleton placeholder={<Skeleton.Title style={{ width: "56%" }} />} loading active />
              <Skeleton placeholder={<Skeleton.Paragraph rows={3} />} loading active />
            </Card>
          ))}
        </div>
      ) : filteredItems.length === 0 ? (
        <Card bodyStyle={{ padding: 48 }}>
          <Empty description={t("workspaceListEmpty")} />
        </Card>
      ) : (
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))",
            gap: 16
          }}
        >
          {filteredItems.map(item => (
            <Card key={item.id} bodyStyle={{ padding: 20 }}>
              <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: 12 }}>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <Tag color="blue">{t("workspaceListWorkspaceTag")}</Tag>
                    <Title heading={5} style={{ margin: "10px 0 0" }}>
                      {item.name}
                    </Title>
                  </div>
                  <Tag color="light-blue">{item.roleCode}</Tag>
                </div>

                <Text type="tertiary">{item.description || t("workspaceListDescriptionFallback")}</Text>

                <div style={{ display: "flex", flexWrap: "wrap", gap: 12, fontSize: 12, color: "var(--semi-color-text-2)" }}>
                  <span>
                    {t("workspaceListAppCount")}: {item.appCount}
                  </span>
                  <span>
                    {t("workspaceListAgentCount")}: {item.agentCount}
                  </span>
                  <span>
                    {t("workspaceListWorkflowCount")}: {item.workflowCount}
                  </span>
                </div>

                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    paddingTop: 12,
                    borderTop: "1px solid var(--semi-color-border)"
                  }}
                >
                  <div style={{ display: "flex", flexDirection: "column" }}>
                    <strong>{item.appKey}</strong>
                    <Text type="tertiary" style={{ fontSize: 12 }}>
                      {item.lastVisitedAt ? t("workspaceListVisited") : t("workspaceListCreated")}
                    </Text>
                  </div>
                  <div style={{ display: "flex", gap: 6 }}>
                    {canManage && onCreateAppInstance ? (
                      <Button
                        theme="light"
                        onClick={() => {
                          setAppInstanceTarget(item);
                          setAppInstanceName("");
                          setAppInstanceDescription("");
                          setAppInstanceAppKey("");
                        }}
                        disabled={saving || deletingWorkspaceId === item.id}
                        data-testid={`workspace-create-app-instance-${item.id}`}
                      >
                        创建应用实例
                      </Button>
                    ) : null}
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
              </div>
            </Card>
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
            icon: createIcon.trim() || undefined
          })
            .then(() => {
              Toast.success(t("workspaceListCreatedSuccess"));
              setCreateVisible(false);
              resetCreateDialog();
            })
            .catch(() => undefined);
        }}
        okButtonProps={{ disabled: !createName.trim(), loading: saving }}
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <Input value={createName} onChange={setCreateName} placeholder={t("workspaceListNamePlaceholder")} />
          <Input
            value={createDescription}
            onChange={setCreateDescription}
            placeholder={t("workspaceListDescriptionPlaceholder")}
          />
          <Input value={createIcon} onChange={setCreateIcon} placeholder={t("workspaceListIconPlaceholder")} />
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
          })
            .then(() => {
              Toast.success(t("workspaceListUpdatedSuccess"));
              setEditTarget(null);
              setEditName("");
              setEditDescription("");
              setEditIcon("");
            })
            .catch(() => undefined);
        }}
        okButtonProps={{ disabled: !editName.trim(), loading: saving }}
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <Input value={editName} onChange={setEditName} placeholder={t("workspaceListNamePlaceholder")} />
          <Input
            value={editDescription}
            onChange={setEditDescription}
            placeholder={t("workspaceListDescriptionPlaceholder")}
          />
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

          void onDeleteWorkspace(archiveTarget.id)
            .then(() => {
              Toast.success(t("workspaceListArchivedSuccess"));
              setArchiveTarget(null);
            })
            .catch(() => undefined);
        }}
        okButtonProps={{
          type: "danger",
          theme: "solid",
          loading: archiveTarget ? deletingWorkspaceId === archiveTarget.id : false
        }}
      >
        <Text>
          {t("workspaceListArchiveConfirmContent").replace("{workspace}", archiveTarget?.name ?? "")}
        </Text>
      </Modal>

      {/* 1→N 模型：在工作空间内创建应用实例 */}
      <Modal
        title={`在「${appInstanceTarget?.name ?? ""}」中创建应用实例`}
        visible={appInstanceTarget !== null}
        onCancel={() => resetAppInstanceDialog()}
        onOk={() => {
          if (!appInstanceTarget || !onCreateAppInstance) {
            return;
          }
          setAppInstanceSaving(true);
          void onCreateAppInstance(appInstanceTarget.id, {
            name: appInstanceName.trim(),
            description: appInstanceDescription.trim() || undefined,
            appKey: appInstanceAppKey.trim() || undefined
          })
            .then(result => {
              Toast.success(`应用实例已创建：${result.appKey}`);
              resetAppInstanceDialog();
            })
            .catch(() => {
              setAppInstanceSaving(false);
            });
        }}
        okButtonProps={{ disabled: !appInstanceName.trim(), loading: appInstanceSaving }}
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <Input value={appInstanceName} onChange={setAppInstanceName} placeholder="应用实例名称（必填）" />
          <Input
            value={appInstanceDescription}
            onChange={setAppInstanceDescription}
            placeholder="应用实例描述（可选）"
          />
          <Input
            value={appInstanceAppKey}
            onChange={setAppInstanceAppKey}
            placeholder="AppKey（留空则后端自动生成）"
          />
        </div>
      </Modal>
    </div>
  );
}
