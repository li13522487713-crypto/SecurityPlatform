import { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useShallow } from "zustand/react/shallow";
import { Avatar, Button, Input, Modal, Spin, Tag, TextArea, Toast, Typography } from "@coze-arch/coze-design";
import { type SaveSpaceRet, SpaceType } from "@coze-arch/bot-api/playground_api";
import type { BotSpace } from "@coze-arch/bot-api/developer_api";
import { useSpaceStore } from "@coze-foundation/space-store-adapter";
import { rememberLastWorkspaceId } from "../layouts/workspace-shell";
import { useAppI18n } from "../i18n";

const NAME_MAX = 128;
const DESC_MAX = 1024;

interface SpaceStoreSnapshot {
  spaceList: BotSpace[];
  loading: false | Promise<unknown>;
  inited?: boolean;
  fetchSpaces: (force?: boolean) => Promise<unknown>;
  createSpace: (request: {
    name: string;
    description: string;
    icon_uri: string;
    space_type: SpaceType;
  }) => Promise<SaveSpaceRet | undefined>;
}

interface NormalizedWorkspaceItem extends BotSpace {
  id: string;
  name: string;
  role_type?: number;
}

function roleLabel(roleType?: number): string {
  if (roleType === 1) {
    return "Owner";
  }

  if (roleType === 2) {
    return "Admin";
  }

  return "Member";
}

export function CozeWorkspaceConsolePage() {
  const navigate = useNavigate();
  const { t } = useAppI18n();
  const nameRef = useRef<HTMLInputElement>(null);
  const {
    spaceList,
    loading,
    inited,
    fetchSpaces,
    createSpace
  } = useSpaceStore(
    useShallow((state: SpaceStoreSnapshot) => ({
      spaceList: state.spaceList,
      loading: state.loading,
      inited: state.inited,
      fetchSpaces: state.fetchSpaces,
      createSpace: state.createSpace
    }))
  );
  const [keyword, setKeyword] = useState("");
  const [createVisible, setCreateVisible] = useState(false);
  const [creating, setCreating] = useState(false);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");

  useEffect(() => {
    void fetchSpaces(true);
  }, [fetchSpaces]);

  useEffect(() => {
    if (!createVisible) {
      return;
    }

    setName("");
    setDescription("");
    const timer = window.setTimeout(() => nameRef.current?.focus(), 80);
    return () => window.clearTimeout(timer);
  }, [createVisible]);

  const isLoading = Boolean(loading) || !inited;
  const visibleSpaces = useMemo(
    () => spaceList
      .filter((item) => !item.hide_operation)
      .map((item) => ({
        ...item,
        id: String(item.id ?? "").trim(),
        name: String(item.name ?? item.id ?? "").trim()
      }))
      .filter((item): item is NormalizedWorkspaceItem => item.id.length > 0),
    [spaceList]
  );
  const filteredSpaces = useMemo(() => {
    const normalizedKeyword = keyword.trim().toLowerCase();
    if (!normalizedKeyword) {
      return visibleSpaces;
    }

    return visibleSpaces.filter((item) =>
      [item.name, item.description]
        .filter(Boolean)
        .some((field) => String(field).toLowerCase().includes(normalizedKeyword))
    );
  }, [keyword, visibleSpaces]);

  const openWorkspace = (workspaceId: string) => {
    rememberLastWorkspaceId(workspaceId);
    navigate(`/space/${encodeURIComponent(workspaceId)}/develop`);
  };

  const handleCreateWorkspace = async () => {
    const trimmedName = name.trim();
    if (!trimmedName) {
      Toast.warning(t("workspaceListNamePlaceholder"));
      return;
    }

    setCreating(true);
    try {
      const result = await createSpace({
        name: trimmedName,
        description: description.trim(),
        icon_uri: "",
        space_type: SpaceType.Team
      });
      const createdWorkspaceId = String(result?.id ?? "").trim();
      if (!createdWorkspaceId) {
        throw new Error(t("workspaceListActionFailed"));
      }

      Toast.success(t("workspaceListCreatedSuccess"));
      setCreateVisible(false);
      openWorkspace(createdWorkspaceId);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : t("workspaceListActionFailed"));
    } finally {
      setCreating(false);
    }
  };

  return (
    <>
      <div
        data-testid="workspace-list-page"
        style={{
          display: "flex",
          flexDirection: "column",
          gap: 20,
          padding: "8px 4px 24px"
        }}
      >
        <div
          style={{
            display: "flex",
            alignItems: "flex-start",
            justifyContent: "space-between",
            gap: 16,
            flexWrap: "wrap"
          }}
        >
          <div>
            <Typography.Title heading={4} style={{ margin: 0 }}>
              {t("workspaceListTitle")}
            </Typography.Title>
            <Typography.Text type="secondary" style={{ display: "block", marginTop: 8 }}>
              {t("workspaceListSubtitle")}
            </Typography.Text>
          </div>
          <Button
            type="primary"
            theme="solid"
            color="brand"
            onClick={() => setCreateVisible(true)}
            data-testid="workspace-create-btn"
          >
            {t("workspaceListCreate")}
          </Button>
        </div>

        <Input
          value={keyword}
          onChange={(value: string) => setKeyword(String(value ?? ""))}
          placeholder={t("workspaceListSearchPlaceholder")}
          style={{ maxWidth: 360 }}
        />

        {isLoading ? (
          <div
            style={{
              minHeight: 280,
              display: "flex",
              alignItems: "center",
              justifyContent: "center"
            }}
          >
            <Spin spinning size="large" />
          </div>
        ) : filteredSpaces.length === 0 ? (
          <div
            style={{
              minHeight: 280,
              display: "flex",
              flexDirection: "column",
              alignItems: "center",
              justifyContent: "center",
              gap: 16,
              borderRadius: 20,
              border: "1px solid rgba(28, 31, 35, 0.08)",
              background: "#fff"
            }}
          >
            <Typography.Title heading={5} style={{ margin: 0 }}>
              {keyword ? t("workspaceListRecommendEmpty") : t("workspaceListEmpty")}
            </Typography.Title>
            <Typography.Text type="secondary">
              {keyword ? t("workspaceListSearchPlaceholder") : t("workspaceListDescriptionFallback")}
            </Typography.Text>
            {!keyword ? (
              <Button
                type="primary"
                theme="solid"
                color="brand"
                onClick={() => setCreateVisible(true)}
              >
                {t("workspaceListCreate")}
              </Button>
            ) : null}
          </div>
        ) : (
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))",
              gap: 16
            }}
          >
            {filteredSpaces.map((item) => (
              <button
                key={item.id}
                type="button"
                data-testid={`workspace-card-${item.id}`}
                onClick={() => openWorkspace(item.id)}
                style={{
                  display: "flex",
                  flexDirection: "column",
                  gap: 14,
                  padding: 18,
                  borderRadius: 20,
                  border: "1px solid rgba(28, 31, 35, 0.08)",
                  background: "#fff",
                  cursor: "pointer",
                  textAlign: "left",
                  boxShadow: "0 8px 24px rgba(15, 23, 42, 0.06)"
                }}
              >
                <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
                  <Avatar shape="square" size="small" src={item.icon_url}>
                    {item.name}
                  </Avatar>
                  <div style={{ minWidth: 0, flex: 1 }}>
                    <Typography.Text
                      strong
                      style={{
                        display: "block",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                        whiteSpace: "nowrap"
                      }}
                    >
                      {item.name}
                    </Typography.Text>
                    <Typography.Text
                      type="secondary"
                      style={{
                        display: "block",
                        marginTop: 4,
                        minHeight: 20
                      }}
                    >
                      {item.description || t("workspaceListDescriptionFallback")}
                    </Typography.Text>
                  </div>
                </div>

                <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                  <Tag color="brand">{t("cozeShellWorkspaceSwitcherCreateTeamBadge")}</Tag>
                  <Tag>{roleLabel(item.role_type)}</Tag>
                </div>

                <div style={{ display: "flex", justifyContent: "flex-end" }}>
                  <Button type="primary" theme="borderless" color="brand">
                    {t("workspaceListOpen")}
                  </Button>
                </div>
              </button>
            ))}
          </div>
        )}
      </div>

      <Modal
        title={t("workspaceListCreateDialogTitle")}
        visible={createVisible}
        onCancel={() => {
          if (!creating) {
            setCreateVisible(false);
          }
        }}
        onOk={() => {
          void handleCreateWorkspace();
        }}
        okText={creating ? t("loading") : t("workspaceListCreate")}
        cancelText={t("cancel")}
        okButtonProps={{
          loading: creating,
          disabled: creating || !name.trim()
        }}
        data-testid="create-workspace-modal"
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          <div>
            <Typography.Text strong style={{ display: "block", marginBottom: 8 }}>
              {t("workspaceListCreateNameLabel")}
            </Typography.Text>
            <Input
              ref={nameRef}
              value={name}
              onChange={(value: string) => setName(String(value ?? ""))}
              maxLength={NAME_MAX}
              placeholder={t("workspaceListNamePlaceholder")}
              data-testid="create-workspace-name"
            />
          </div>
          <div>
            <Typography.Text strong style={{ display: "block", marginBottom: 8 }}>
              {t("workspaceListCreateDescriptionLabel")}
            </Typography.Text>
            <TextArea
              value={description}
              onChange={(value: string) => setDescription(String(value ?? ""))}
              maxCount={DESC_MAX}
              rows={4}
              placeholder={t("workspaceListDescriptionPlaceholder")}
              data-testid="create-workspace-desc"
            />
          </div>
        </div>
      </Modal>
    </>
  );
}
