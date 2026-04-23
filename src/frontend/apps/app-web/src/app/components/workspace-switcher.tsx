import { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useShallow } from "zustand/react/shallow";
import { Avatar, Button, Input, Modal, Select, Spin, Tag, TextArea, Toast, Typography } from "@coze-arch/coze-design";
import { type SaveSpaceRet, SpaceType } from "@coze-arch/bot-api/playground_api";
import type { BotSpace } from "@coze-arch/bot-api/developer_api";
import { useSpaceStore } from "@coze-foundation/space-store-adapter";
import { useAppI18n } from "../i18n";

interface WorkspaceSwitcherProps {
  workspaceId: string;
  workspaceLabel: string;
  onSelectWorkspace?: (workspaceId: string) => void;
}

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

export function WorkspaceSwitcher({ workspaceId, workspaceLabel, onSelectWorkspace }: WorkspaceSwitcherProps) {
  const { t } = useAppI18n();
  const navigate = useNavigate();
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
    if (!inited) {
      void fetchSpaces().catch(() => undefined);
    }
  }, [fetchSpaces, inited, workspaceId]);

  useEffect(() => {
    if (!createVisible) {
      return;
    }

    setName("");
    setDescription("");
    const timer = window.setTimeout(() => nameRef.current?.focus(), 80);
    return () => window.clearTimeout(timer);
  }, [createVisible]);

  const filtered = useMemo(() => {
    const normalizedKeyword = keyword.trim().toLowerCase();
    const visibleSpaces = spaceList
      .filter((item) => !item.hide_operation)
      .map((item) => ({
        ...item,
        id: String(item.id ?? "").trim(),
        name: String(item.name ?? item.id ?? "").trim()
      }))
      .filter((item): item is NormalizedWorkspaceItem => item.id.length > 0);
    if (!normalizedKeyword) {
      return visibleSpaces;
    }

    return visibleSpaces.filter((item) =>
      [item.name, item.description]
        .filter(Boolean)
        .some((field) => String(field).toLowerCase().includes(normalizedKeyword))
    );
  }, [keyword, spaceList]);

  const currentWorkspace = useMemo(
    () => filtered.find((item) => item.id === workspaceId) ?? spaceList.find((item) => item.id === workspaceId) ?? null,
    [filtered, spaceList, workspaceId]
  );

  const selectWorkspace = (targetWorkspaceId: string) => {
    if (onSelectWorkspace) {
      onSelectWorkspace(targetWorkspaceId);
      return;
    }

    navigate(`/space/${encodeURIComponent(targetWorkspaceId)}/develop`);
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
      selectWorkspace(createdWorkspaceId);
      if (!onSelectWorkspace) {
        navigate(`/space/${encodeURIComponent(createdWorkspaceId)}/develop`);
      }
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : t("workspaceListActionFailed"));
    } finally {
      setCreating(false);
    }
  };

  return (
    <>
      <div
        data-testid="coze-workspace-switcher-panel"
        style={{ display: "flex", flexDirection: "column", gap: 8, width: "100%" }}
      >
        <div data-testid="coze-workspace-switcher-trigger">
          <Select
            filter
            size="small"
            value={workspaceId || currentWorkspace?.id}
            optionList={[]}
            onSearch={(value: string) => setKeyword(String(value ?? ""))}
            onChange={(value: string | number | unknown[] | Record<string, unknown> | undefined) => {
              const nextWorkspaceId = String(value ?? "").trim();
              if (nextWorkspaceId) {
                selectWorkspace(nextWorkspaceId);
              }
            }}
            emptyContent={Boolean(loading) || !inited ? <Spin spinning size="small" /> : t("cozeShellWorkspaceSwitcherEmpty")}
            placeholder={t("cozeShellWorkspaceSwitcherTitle")}
            renderSelectedItem={(optionNode: { icon_url?: string; name?: string } | undefined) => (
              optionNode ? (
                <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                  <Avatar shape="square" size="extra-extra-small" src={optionNode.icon_url}>
                    {optionNode.name}
                  </Avatar>
                  <span>{optionNode.name}</span>
                </div>
              ) : (
                <span>{workspaceLabel || t("cozeShellWorkspaceSwitcherTitle")}</span>
              )
            )}
          >
            {filtered.map((item) => (
              <Select.Option
                value={item.id}
                key={item.id}
                {...item}
                data-testid={`coze-workspace-switcher-item-${item.id}`}
              >
                <div style={{ display: "flex", alignItems: "center", gap: 8, width: "100%" }}>
                  <Avatar shape="square" size="extra-small" src={item.icon_url}>
                    {item.name}
                  </Avatar>
                  <span style={{ flex: 1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                    {item.name}
                  </span>
                </div>
              </Select.Option>
            ))}
          </Select>
        </div>

        <div style={{ display: "flex", gap: 8 }}>
          <Button
            type="primary"
            theme="solid"
            color="brand"
            style={{ flex: 1 }}
            onClick={() => setCreateVisible(true)}
          >
            {t("cozeShellWorkspaceSwitcherCreateTeam")}
          </Button>
        </div>
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
          <Tag color="brand">{t("cozeShellWorkspaceSwitcherCreateTeamBadge")}</Tag>
        </div>
      </Modal>
    </>
  );
}
