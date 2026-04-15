import { useEffect, useMemo, useState } from "react";
import { Button, Empty, Input, Select, TabPane, Tabs, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import type {
  WorkspaceMemberDto,
  WorkspaceResourceCardDto,
  WorkspaceRolePermissionDto
} from "../../services/api-org-workspaces";
import { useAppI18n } from "../i18n";

type WorkspaceSettingsTab = "members" | "permissions";

interface WorkspaceSettingsPageProps {
  activeTab: WorkspaceSettingsTab;
  workspaceName: string;
  membersLoading: boolean;
  memberSearchLoading: boolean;
  memberSearchPageIndex: number;
  memberSearchPageSize: number;
  memberSearchTotal: number;
  permissionsLoading: boolean;
  resourcesLoading: boolean;
  members: WorkspaceMemberDto[];
  memberSearchResults: Array<{
    id: string;
    username: string;
    displayName: string;
    isActive: boolean;
    disabledReason?: string;
    currentRoleCode?: string;
  }>;
  resources: WorkspaceResourceCardDto[];
  selectedResourceKey: string;
  selectedPermissions: WorkspaceRolePermissionDto[];
  onSelectResource: (key: string) => void;
  onTabChange: (tab: WorkspaceSettingsTab) => void;
  onSearchMembers: (keyword: string, pageIndex?: number) => void;
  onRefreshMembers: () => Promise<void>;
  onRefreshPermissions: () => Promise<void>;
  onAddMember: (request: { userId: string; roleCode: string }) => Promise<void>;
  onUpdateMemberRole: (userId: string, roleCode: string) => Promise<void>;
  onRemoveMember: (userId: string) => Promise<void>;
  onSavePermissions: (items: Array<{ roleCode: string; actions: string[] }>) => Promise<void>;
}

const roleOptions = [
  { label: "Owner", value: "Owner" },
  { label: "Admin", value: "Admin" },
  { label: "Member", value: "Member" }
];

const actionOptions = [
  { label: "view", value: "view" },
  { label: "edit", value: "edit" },
  { label: "publish", value: "publish" },
  { label: "delete", value: "delete" },
  { label: "manage-permission", value: "manage-permission" }
];

export function WorkspaceSettingsPage({
  activeTab,
  workspaceName,
  membersLoading,
  memberSearchLoading,
  memberSearchPageIndex,
  memberSearchPageSize,
  memberSearchTotal,
  permissionsLoading,
  resourcesLoading,
  members,
  memberSearchResults,
  resources,
  selectedResourceKey,
  selectedPermissions,
  onSelectResource,
  onTabChange,
  onSearchMembers,
  onRefreshMembers,
  onRefreshPermissions,
  onAddMember,
  onUpdateMemberRole,
  onRemoveMember,
  onSavePermissions
}: WorkspaceSettingsPageProps) {
  const { t } = useAppI18n();
  const [memberSearchKeyword, setMemberSearchKeyword] = useState("");
  const [memberUserId, setMemberUserId] = useState("");
  const [memberRoleCode, setMemberRoleCode] = useState("Member");
  const [draftPermissions, setDraftPermissions] = useState<Array<{ roleCode: string; actions: string[] }>>([]);

  useEffect(() => {
    setDraftPermissions(selectedPermissions.map(item => ({
      roleCode: item.roleCode,
      actions: [...item.actions]
    })));
  }, [selectedPermissions]);

  const resourceOptions = useMemo(() => resources.map(item => ({
    label: `${item.name} (${item.resourceType})`,
    value: `${item.resourceType}:${item.resourceId}`
  })), [resources]);

  const selectedCandidate = useMemo(
    () => memberSearchResults.find(item => item.id === memberUserId) ?? null,
    [memberSearchResults, memberUserId]
  );

  return (
    <div className="atlas-workspace-settings-page" data-testid="workspace-settings-page">
      <section className="atlas-workspace-settings-hero">
        <div>
          <span className="atlas-workspace-settings-hero__kicker">{t("workspaceSettingsKicker")}</span>
          <Typography.Title heading={3} style={{ margin: "10px 0 0" }}>
            {t("workspaceSettingsTitle")}
          </Typography.Title>
          <Typography.Text type="tertiary">
            {t("workspaceSettingsSubtitle").replace("{workspace}", workspaceName)}
          </Typography.Text>
        </div>
      </section>

      <Tabs type="line" activeKey={activeTab} onChange={key => onTabChange((key as WorkspaceSettingsTab) ?? "members")}>
        <TabPane tab={t("workspaceSettingsMembersTab")} itemKey="members">
          <section className="atlas-workspace-settings-card">
            <div className="atlas-workspace-settings-card__head">
              <div>
                <Typography.Title heading={5} style={{ margin: 0 }}>
                  {t("workspaceSettingsMembersTitle")}
                </Typography.Title>
                <Typography.Text type="tertiary">
                  {t("workspaceSettingsMembersSubtitle")}
                </Typography.Text>
              </div>
              <Button theme="light" onClick={() => void onRefreshMembers()}>
                {t("refresh")}
              </Button>
            </div>

            <div className="atlas-workspace-settings-member-search">
              <div className="atlas-workspace-settings-member-form">
                <Input
                  value={memberSearchKeyword}
                  onChange={value => {
                    setMemberSearchKeyword(value);
                    onSearchMembers(value, 1);
                  }}
                  placeholder={t("workspaceSettingsMemberSearchPlaceholder")}
                />
                <Select
                  value={memberRoleCode}
                  optionList={roleOptions}
                  onChange={value => setMemberRoleCode(String(value))}
                />
                <Button
                  type="primary"
                  theme="solid"
                  disabled={!memberUserId.trim()}
                  onClick={() => {
                    if (selectedCandidate?.disabledReason) {
                      Toast.warning(selectedCandidate.disabledReason);
                      return;
                    }
                    void onAddMember({
                      userId: memberUserId.trim(),
                      roleCode: memberRoleCode
                    }).then(() => {
                      setMemberUserId("");
                      setMemberRoleCode("Member");
                      setMemberSearchKeyword("");
                      Toast.success(t("workspaceSettingsMemberAdded"));
                    });
                  }}
                >
                  {t("workspaceSettingsMemberAdd")}
                </Button>
              </div>

              {selectedCandidate ? (
                <div className="atlas-workspace-settings-selected-candidate">
                  <div>
                    <strong>{selectedCandidate.displayName || selectedCandidate.username}</strong>
                    <span>{selectedCandidate.username}</span>
                  </div>
                  <div className="atlas-workspace-settings-selected-candidate__meta">
                    <Tag color={selectedCandidate.isActive ? "green" : "grey"}>
                      {selectedCandidate.isActive ? t("workspaceSettingsMemberStatusActive") : t("workspaceSettingsMemberStatusInactive")}
                    </Tag>
                    {selectedCandidate.currentRoleCode ? (
                      <Tag color="blue">{selectedCandidate.currentRoleCode}</Tag>
                    ) : null}
                    {selectedCandidate.disabledReason ? (
                      <Tag color="orange">{selectedCandidate.disabledReason}</Tag>
                    ) : null}
                  </div>
                </div>
              ) : null}

              {memberSearchKeyword.trim() ? (
                <div className="atlas-workspace-settings-search-results">
                  {memberSearchLoading ? (
                    <div className="atlas-workspace-settings-search-empty">
                      <Typography.Text type="tertiary">{t("loading")}</Typography.Text>
                    </div>
                  ) : memberSearchResults.length === 0 ? (
                    <div className="atlas-workspace-settings-search-empty">
                      <Typography.Text type="tertiary">{t("workspaceSettingsMemberSearchEmpty")}</Typography.Text>
                    </div>
                  ) : (
                    memberSearchResults.map(item => (
                      <button
                        key={item.id}
                        type="button"
                        className={`atlas-workspace-settings-search-item${memberUserId === item.id ? " is-active" : ""}${item.disabledReason ? " is-disabled" : ""}`}
                        onClick={() => {
                          if (item.disabledReason) {
                            return;
                          }
                          setMemberUserId(item.id);
                        }}
                        disabled={Boolean(item.disabledReason)}
                      >
                        <div>
                          <strong>{item.displayName || item.username}</strong>
                          <span>{item.username}</span>
                        </div>
                        <div className="atlas-workspace-settings-search-item__meta">
                          <Tag color={item.isActive ? "green" : "grey"}>
                            {item.isActive ? t("workspaceSettingsMemberStatusActive") : t("workspaceSettingsMemberStatusInactive")}
                          </Tag>
                          {item.currentRoleCode ? (
                            <Tag color="blue">{item.currentRoleCode}</Tag>
                          ) : null}
                          {item.disabledReason ? (
                            <Tag color="orange">{item.disabledReason}</Tag>
                          ) : null}
                        </div>
                      </button>
                    ))
                  )}
                </div>
              ) : null}

              {memberSearchKeyword.trim() ? (
                <div className="atlas-workspace-settings-search-pagination">
                  <Typography.Text type="tertiary">
                    {t("workspaceSettingsMemberSearchPagination")
                      .replace("{from}", String(memberSearchTotal === 0 ? 0 : (memberSearchPageIndex - 1) * memberSearchPageSize + 1))
                      .replace("{to}", String(Math.min(memberSearchPageIndex * memberSearchPageSize, memberSearchTotal)))
                      .replace("{total}", String(memberSearchTotal))}
                  </Typography.Text>
                  <div className="atlas-workspace-settings-search-pagination__actions">
                    <Button
                      theme="borderless"
                      disabled={memberSearchPageIndex <= 1}
                      onClick={() => onSearchMembers(memberSearchKeyword, memberSearchPageIndex - 1)}
                    >
                      {t("workspaceSettingsMemberSearchPrev")}
                    </Button>
                    <Button
                      theme="borderless"
                      disabled={memberSearchPageIndex * memberSearchPageSize >= memberSearchTotal}
                      onClick={() => onSearchMembers(memberSearchKeyword, memberSearchPageIndex + 1)}
                    >
                      {t("workspaceSettingsMemberSearchNext")}
                    </Button>
                  </div>
                </div>
              ) : null}
            </div>

            {membersLoading ? (
              <div className="atlas-develop-empty"><Typography.Text type="tertiary">{t("loading")}</Typography.Text></div>
            ) : members.length === 0 ? (
              <div className="atlas-develop-empty"><Empty description={t("workspaceSettingsMembersEmpty")} /></div>
            ) : (
              <div className="atlas-workspace-settings-list">
                {members.map(member => (
                  <div key={member.userId} className="atlas-workspace-settings-list__item">
                    <div>
                      <strong>{member.displayName}</strong>
                      <span>{member.username}</span>
                    </div>
                    <div className="atlas-workspace-settings-list__actions">
                      <Tag color="blue">{member.roleCode}</Tag>
                      <Select
                        value={member.roleCode}
                        optionList={roleOptions}
                        onChange={value => {
                          void onUpdateMemberRole(member.userId, String(value)).then(() => {
                            Toast.success(t("workspaceSettingsMemberUpdated"));
                          });
                        }}
                      />
                      <Button type="danger" theme="borderless" onClick={() => {
                        void onRemoveMember(member.userId).then(() => {
                          Toast.success(t("workspaceSettingsMemberRemoved"));
                        });
                      }}>
                        {t("workspaceSettingsMemberRemove")}
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </section>
        </TabPane>

        <TabPane tab={t("workspaceSettingsPermissionsTab")} itemKey="permissions">
          <section className="atlas-workspace-settings-card">
            <div className="atlas-workspace-settings-card__head">
              <div>
                <Typography.Title heading={5} style={{ margin: 0 }}>
                  {t("workspaceSettingsPermissionsTitle")}
                </Typography.Title>
                <Typography.Text type="tertiary">
                  {t("workspaceSettingsPermissionsSubtitle")}
                </Typography.Text>
              </div>
              <Button theme="light" onClick={() => void onRefreshPermissions()}>
                {t("refresh")}
              </Button>
            </div>

            <div className="atlas-workspace-settings-permission-toolbar">
              <Select
                style={{ width: "100%" }}
                value={selectedResourceKey}
                optionList={resourceOptions}
                loading={resourcesLoading}
                placeholder={t("workspaceSettingsPermissionResourcePlaceholder")}
                onChange={value => onSelectResource(String(value))}
              />
            </div>

            {permissionsLoading ? (
              <div className="atlas-develop-empty"><Typography.Text type="tertiary">{t("loading")}</Typography.Text></div>
            ) : draftPermissions.length === 0 ? (
              <div className="atlas-develop-empty"><Empty description={t("workspaceSettingsPermissionsEmpty")} /></div>
            ) : (
              <div className="atlas-workspace-settings-list">
                {draftPermissions.map(item => (
                  <div key={item.roleCode} className="atlas-workspace-settings-list__item">
                    <div>
                      <strong>{item.roleCode}</strong>
                      <span>{t("workspaceSettingsPermissionActions")}</span>
                    </div>
                    <Select
                      multiple
                      style={{ width: 360 }}
                      value={item.actions}
                      optionList={actionOptions}
                      onChange={value => {
                        setDraftPermissions(current => current.map(entry => entry.roleCode === item.roleCode
                          ? { ...entry, actions: (value as string[]) ?? [] }
                          : entry));
                      }}
                    />
                  </div>
                ))}
              </div>
            )}

            <div className="atlas-workspace-settings-footer">
              <Button
                type="primary"
                theme="solid"
                disabled={!selectedResourceKey}
                onClick={() => {
                  void onSavePermissions(draftPermissions).then(() => {
                    Toast.success(t("workspaceSettingsPermissionsSaved"));
                  });
                }}
              >
                {t("workspaceSettingsPermissionsSave")}
              </Button>
            </div>
          </section>
        </TabPane>
      </Tabs>
    </div>
  );
}
