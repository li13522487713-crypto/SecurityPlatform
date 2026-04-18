import { useEffect, useMemo, useState } from "react";
import { Button, Card, Empty, Input, Select, TabPane, Tabs, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import type {
  WorkspaceMemberDto,
  WorkspaceResourceCardDto,
  WorkspaceRolePermissionDto
} from "../../services/api-org-workspaces";
import { useAppI18n } from "../i18n";

const { Title, Text } = Typography;

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
  const roleOptions = useMemo(() => [
    { label: t("workspaceSettingsRoleOwner"), value: "Owner" },
    { label: t("workspaceSettingsRoleAdmin"), value: "Admin" },
    { label: t("workspaceSettingsRoleMember"), value: "Member" }
  ], [t]);
  const actionOptions = useMemo(() => [
    { label: t("workspaceSettingsPermView"), value: "view" },
    { label: t("workspaceSettingsPermEdit"), value: "edit" },
    { label: t("workspaceSettingsPermPublish"), value: "publish" },
    { label: t("workspaceSettingsPermDelete"), value: "delete" },
    { label: t("workspaceSettingsPermManagePermission"), value: "manage-permission" }
  ], [t]);
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
    <div data-testid="workspace-settings-page" style={{ display: "flex", flexDirection: "column", gap: 16, padding: 16 }}>
      <Card bodyStyle={{ padding: 24 }}>
        <Text type="tertiary" style={{ textTransform: "uppercase", letterSpacing: "0.08em", fontSize: 12 }}>
          {t("workspaceSettingsKicker")}
        </Text>
        <Title heading={3} style={{ margin: "10px 0 0" }}>
          {t("workspaceSettingsTitle")}
        </Title>
        <Text type="tertiary">
          {t("workspaceSettingsSubtitle").replace("{workspace}", workspaceName)}
        </Text>
      </Card>

      <Tabs type="line" activeKey={activeTab} onChange={key => onTabChange((key as WorkspaceSettingsTab) ?? "members")}>
        <TabPane tab={t("workspaceSettingsMembersTab")} itemKey="members">
          <Card bodyStyle={{ padding: 24 }} style={{ marginTop: 12 }}>
            <div
              style={{
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                marginBottom: 16
              }}
            >
              <div>
                <Title heading={5} style={{ margin: 0 }}>
                  {t("workspaceSettingsMembersTitle")}
                </Title>
                <Text type="tertiary">{t("workspaceSettingsMembersSubtitle")}</Text>
              </div>
              <Button theme="light" onClick={() => void onRefreshMembers()}>
                {t("refresh")}
              </Button>
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 160px auto", gap: 12 }}>
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
                <Card bodyStyle={{ padding: 12 }} style={{ background: "var(--semi-color-fill-0)" }}>
                  <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                    <strong>{selectedCandidate.displayName || selectedCandidate.username}</strong>
                    <Text type="tertiary">{selectedCandidate.username}</Text>
                  </div>
                  <div style={{ display: "flex", gap: 6, marginTop: 8 }}>
                    <Tag color={selectedCandidate.isActive ? "green" : "grey"}>
                      {selectedCandidate.isActive
                        ? t("workspaceSettingsMemberStatusActive")
                        : t("workspaceSettingsMemberStatusInactive")}
                    </Tag>
                    {selectedCandidate.currentRoleCode ? (
                      <Tag color="blue">{selectedCandidate.currentRoleCode}</Tag>
                    ) : null}
                    {selectedCandidate.disabledReason ? (
                      <Tag color="orange">{selectedCandidate.disabledReason}</Tag>
                    ) : null}
                  </div>
                </Card>
              ) : null}

              {memberSearchKeyword.trim() ? (
                <Card bodyStyle={{ padding: 0 }}>
                  <div style={{ display: "flex", flexDirection: "column" }}>
                    {memberSearchLoading ? (
                      <div style={{ padding: 16 }}>
                        <Text type="tertiary">{t("loading")}</Text>
                      </div>
                    ) : memberSearchResults.length === 0 ? (
                      <div style={{ padding: 16 }}>
                        <Text type="tertiary">{t("workspaceSettingsMemberSearchEmpty")}</Text>
                      </div>
                    ) : (
                      memberSearchResults.map(item => {
                        const disabled = Boolean(item.disabledReason);
                        const active = memberUserId === item.id;
                        return (
                          <button
                            key={item.id}
                            type="button"
                            onClick={() => {
                              if (disabled) {
                                return;
                              }
                              setMemberUserId(item.id);
                            }}
                            disabled={disabled}
                            style={{
                              display: "flex",
                              alignItems: "center",
                              justifyContent: "space-between",
                              gap: 12,
                              padding: "10px 12px",
                              border: "none",
                              borderBottom: "1px solid var(--semi-color-border)",
                              background: active ? "var(--semi-color-primary-light-default)" : "transparent",
                              cursor: disabled ? "not-allowed" : "pointer",
                              textAlign: "left",
                              opacity: disabled ? 0.6 : 1
                            }}
                          >
                            <div style={{ display: "flex", flexDirection: "column" }}>
                              <strong>{item.displayName || item.username}</strong>
                              <Text type="tertiary" style={{ fontSize: 12 }}>
                                {item.username}
                              </Text>
                            </div>
                            <div style={{ display: "flex", gap: 4 }}>
                              <Tag color={item.isActive ? "green" : "grey"}>
                                {item.isActive
                                  ? t("workspaceSettingsMemberStatusActive")
                                  : t("workspaceSettingsMemberStatusInactive")}
                              </Tag>
                              {item.currentRoleCode ? <Tag color="blue">{item.currentRoleCode}</Tag> : null}
                              {item.disabledReason ? <Tag color="orange">{item.disabledReason}</Tag> : null}
                            </div>
                          </button>
                        );
                      })
                    )}
                  </div>
                </Card>
              ) : null}

              {memberSearchKeyword.trim() ? (
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <Text type="tertiary">
                    {t("workspaceSettingsMemberSearchPagination")
                      .replace("{from}", String(memberSearchTotal === 0 ? 0 : (memberSearchPageIndex - 1) * memberSearchPageSize + 1))
                      .replace("{to}", String(Math.min(memberSearchPageIndex * memberSearchPageSize, memberSearchTotal)))
                      .replace("{total}", String(memberSearchTotal))}
                  </Text>
                  <div style={{ display: "flex", gap: 8 }}>
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

            <div style={{ marginTop: 16 }}>
              {membersLoading ? (
                <Text type="tertiary">{t("loading")}</Text>
              ) : members.length === 0 ? (
                <Empty description={t("workspaceSettingsMembersEmpty")} />
              ) : (
                <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                  {members.map(member => (
                    <Card key={member.userId} bodyStyle={{ padding: 12 }}>
                      <div
                        style={{
                          display: "flex",
                          alignItems: "center",
                          justifyContent: "space-between",
                          gap: 12
                        }}
                      >
                        <div style={{ display: "flex", flexDirection: "column" }}>
                          <strong>{member.displayName}</strong>
                          <Text type="tertiary" style={{ fontSize: 12 }}>
                            {member.username}
                          </Text>
                        </div>
                        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
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
                          <Button
                            type="danger"
                            theme="borderless"
                            onClick={() => {
                              void onRemoveMember(member.userId).then(() => {
                                Toast.success(t("workspaceSettingsMemberRemoved"));
                              });
                            }}
                          >
                            {t("workspaceSettingsMemberRemove")}
                          </Button>
                        </div>
                      </div>
                    </Card>
                  ))}
                </div>
              )}
            </div>
          </Card>
        </TabPane>

        <TabPane tab={t("workspaceSettingsPermissionsTab")} itemKey="permissions">
          <Card bodyStyle={{ padding: 24 }} style={{ marginTop: 12 }}>
            <div
              style={{
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                marginBottom: 16
              }}
            >
              <div>
                <Title heading={5} style={{ margin: 0 }}>
                  {t("workspaceSettingsPermissionsTitle")}
                </Title>
                <Text type="tertiary">{t("workspaceSettingsPermissionsSubtitle")}</Text>
              </div>
              <Button theme="light" onClick={() => void onRefreshPermissions()}>
                {t("refresh")}
              </Button>
            </div>

            <div style={{ marginBottom: 16 }}>
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
              <Text type="tertiary">{t("loading")}</Text>
            ) : draftPermissions.length === 0 ? (
              <Empty description={t("workspaceSettingsPermissionsEmpty")} />
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {draftPermissions.map(item => (
                  <Card key={item.roleCode} bodyStyle={{ padding: 12 }}>
                    <div
                      style={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        gap: 12
                      }}
                    >
                      <div style={{ display: "flex", flexDirection: "column" }}>
                        <strong>{item.roleCode}</strong>
                        <Text type="tertiary" style={{ fontSize: 12 }}>
                          {t("workspaceSettingsPermissionActions")}
                        </Text>
                      </div>
                      <Select
                        multiple
                        style={{ width: 360 }}
                        value={item.actions}
                        optionList={actionOptions}
                        onChange={value => {
                          setDraftPermissions(current =>
                            current.map(entry =>
                              entry.roleCode === item.roleCode
                                ? { ...entry, actions: (value as string[]) ?? [] }
                                : entry
                            )
                          );
                        }}
                      />
                    </div>
                  </Card>
                ))}
              </div>
            )}

            <div style={{ display: "flex", justifyContent: "flex-end", marginTop: 16 }}>
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
          </Card>
        </TabPane>
      </Tabs>
    </div>
  );
}
