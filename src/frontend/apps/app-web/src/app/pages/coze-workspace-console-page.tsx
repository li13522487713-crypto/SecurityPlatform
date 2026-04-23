import { useEffect, useMemo, useRef, useState } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import { useShallow } from "zustand/react/shallow";
import { Button, Empty, Input, Modal, Spin, TextArea, Toast, Typography } from "@coze-arch/coze-design";
import { IconCozIllusAdd } from "@coze-arch/coze-design/illustrations";
import { type SaveSpaceRet, SpaceType } from "@coze-arch/bot-api/playground_api";
import type { BotSpace } from "@coze-arch/bot-api/developer_api";
import { useSpaceStore } from "@coze-foundation/space-store-adapter";
import { readLastWorkspaceId, rememberLastWorkspaceId } from "../layouts/workspace-shell";
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
  const [createVisible, setCreateVisible] = useState(false);
  const [creating, setCreating] = useState(false);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");

  useEffect(() => {
    void fetchSpaces().catch(() => undefined);
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

  const fallbackWorkspaceId = useMemo(() => {
    const rememberedWorkspaceId = readLastWorkspaceId();
    if (rememberedWorkspaceId && visibleSpaces.some((item) => item.id === rememberedWorkspaceId)) {
      return rememberedWorkspaceId;
    }

    return visibleSpaces[0]?.id ?? "";
  }, [visibleSpaces]);

  if (!isLoading && fallbackWorkspaceId) {
    return <Navigate to={`/space/${encodeURIComponent(fallbackWorkspaceId)}/develop`} replace />;
  }

  return (
    <>
      <div
        data-testid="workspace-list-page"
        style={{
          display: "flex",
          flexDirection: "column",
          gap: 20,
          padding: "24px 12px 24px"
        }}
      >
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
        ) : (
          <div
            data-testid="workspace-empty-state"
            style={{
              minHeight: 420,
              display: "flex",
              flexDirection: "column",
              alignItems: "center",
              justifyContent: "center",
              gap: 16,
              borderRadius: 24,
              background: "#fff",
              border: "1px solid rgba(28, 31, 35, 0.08)"
            }}
          >
            <Empty
              image={<IconCozIllusAdd width="160" height="160" />}
              title={t("workspaceListEmpty")}
              description={t("workspaceListSubtitle")}
            />
            <Button
              type="primary"
              theme="solid"
              color="brand"
              size="large"
              onClick={() => setCreateVisible(true)}
              data-testid="workspace-create-btn"
            >
              {t("workspaceListCreate")}
            </Button>
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
