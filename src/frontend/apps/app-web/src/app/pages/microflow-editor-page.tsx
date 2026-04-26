import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Button, Checkbox, Input, Modal, Space, Spin, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import { IconArrowLeft, IconExpand } from "@douyinfe/semi-icons";
import {
  createLocalMicroflowApiClient,
  MicroflowEditor,
  type MicroflowResource,
  type MicroflowSchema
} from "@atlas/microflow";
import { cozeWorkspaceLibraryPath } from "@atlas/app-shell-shared";
import { useNavigate, useParams } from "react-router-dom";
import { useAppI18n } from "../i18n";
import { useWorkspaceContext } from "../workspace-context";

const { Text } = Typography;

function formatDate(iso?: string): string {
  if (!iso) {
    return "";
  }
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return iso;
  }
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")} ${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`;
}

function nextVersion(version: string): string {
  const normalized = version.trim().replace(/^v/u, "");
  const parts = normalized.split(".").map(part => Number(part));
  if (parts.length > 0 && parts.every(part => Number.isFinite(part))) {
    const nextParts = parts.length === 1 ? [parts[0] ?? 0, 0] : parts;
    nextParts[nextParts.length - 1] = (nextParts[nextParts.length - 1] ?? 0) + 1;
    return `v${nextParts.join(".")}`;
  }
  return "v1";
}

export function MicroflowEditorPage() {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const workspace = useWorkspaceContext();
  const editorFrameRef = useRef<HTMLDivElement>(null);
  const { microflowId = "" } = useParams<{ microflowId: string }>();
  const apiClient = useMemo(() => createLocalMicroflowApiClient(), []);
  const [resource, setResource] = useState<MicroflowResource>();
  const [schema, setSchema] = useState<MicroflowSchema>();
  const [loading, setLoading] = useState(true);
  const [publishOpen, setPublishOpen] = useState(false);
  const [publishVersion, setPublishVersion] = useState("v1");
  const [releaseNote, setReleaseNote] = useState("");
  const [overwriteCurrent, setOverwriteCurrent] = useState(true);
  const [publishing, setPublishing] = useState(false);
  const [fullscreen, setFullscreen] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const loaded = await apiClient.getMicroflow(microflowId);
      setResource(loaded);
      setSchema(loaded.schema);
      setPublishVersion(nextVersion(loaded.version));
    } catch (error) {
      Toast.error((error as Error).message || t("microflowLoadFailed"));
    } finally {
      setLoading(false);
    }
  }, [apiClient, microflowId, t]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    function syncFullscreen() {
      setFullscreen(document.fullscreenElement === editorFrameRef.current);
    }

    document.addEventListener("fullscreenchange", syncFullscreen);
    return () => document.removeEventListener("fullscreenchange", syncFullscreen);
  }, []);

  const backToLibrary = useCallback(() => {
    navigate(cozeWorkspaceLibraryPath(workspace.id, "microflow"));
  }, [navigate, workspace.id]);

  async function toggleFullscreen() {
    const target = editorFrameRef.current;
    if (!target) {
      return;
    }

    if (document.fullscreenElement === target) {
      await document.exitFullscreen?.();
      return;
    }

    await target.requestFullscreen?.();
  }

  async function handlePublish() {
    if (!schema || !resource) {
      return;
    }
    setPublishing(true);
    try {
      await apiClient.saveMicroflow({ schema });
      const response = await apiClient.publishMicroflow(resource.id, {
        version: publishVersion,
        releaseNote,
        overwriteCurrent
      });
      if (response.resource) {
        setResource(response.resource);
        setSchema(response.resource.schema);
      }
      Toast.success(t("microflowPublishSuccess"));
      setPublishOpen(false);
    } finally {
      setPublishing(false);
    }
  }

  if (loading || !resource || !schema) {
    return (
      <div style={{ display: "flex", justifyContent: "center", alignItems: "center", height: "100%" }}>
        <Spin />
      </div>
    );
  }

  return (
    <div
      ref={editorFrameRef}
      style={{
        height: "100dvh",
        minHeight: fullscreen ? 0 : 720,
        background: "var(--semi-color-bg-0, #f7f8fa)",
        overflow: "hidden"
      }}
    >
      <MicroflowEditor
        schema={schema}
        apiClient={apiClient}
        immersive={fullscreen}
        onSchemaChange={setSchema}
        onSaveComplete={() => void load()}
        onPublish={() => {
          setPublishVersion(nextVersion(resource.version));
          setPublishOpen(true);
        }}
        toolbarPrefix={
          <Space align="center">
            <Button icon={<IconArrowLeft />} theme="borderless" onClick={backToLibrary}>
              {t("microflowBackToLibrary")}
            </Button>
            <Button icon={<IconExpand />} theme="borderless" onClick={() => void toggleFullscreen()}>
              {fullscreen ? t("microflowExitFullscreen") : t("microflowEnterFullscreen")}
            </Button>
            <Tag color={resource.status === "published" ? "green" : resource.status === "archived" ? "grey" : "blue"}>
              {resource.status === "published" ? t("microflowStatusPublished") : resource.status === "archived" ? t("microflowStatusArchived") : t("microflowStatusDraft")}
            </Tag>
            <Tag>{resource.version}</Tag>
            <Text type="tertiary">{resource.lastModifiedBy ?? resource.ownerName} · {formatDate(resource.updatedAt)}</Text>
          </Space>
        }
        labels={{
          save: t("microflowToolbarSave"),
          validate: t("microflowToolbarValidate"),
          testRun: t("microflowToolbarTestRun"),
          fitView: t("microflowToolbarFitView"),
          publish: t("microflowToolbarPublish"),
          undo: t("microflowToolbarUndo"),
          redo: t("microflowToolbarRedo"),
          format: t("microflowToolbarFormat"),
          settings: t("microflowToolbarSettings"),
          more: t("microflowToolbarMore"),
          nodePanel: t("microflowEditorNodes"),
          properties: t("microflowEditorProperties"),
          problems: t("microflowEditorProblems"),
          debug: t("microflowEditorDebug")
        }}
        nodePanelLabels={{
          nodesTab: t("microflowNodePanelNodesTab"),
          componentsTab: t("microflowNodePanelComponentsTab"),
          templatesTab: t("microflowNodePanelTemplatesTab"),
          searchPlaceholder: t("microflowNodePanelSearchPlaceholder"),
          filterTitle: t("microflowNodePanelFilterTitle"),
          filterAll: t("microflowNodePanelFilterAll"),
          filterFavorites: t("microflowNodePanelFilterFavorites"),
          filterEnabled: t("microflowNodePanelFilterEnabled"),
          favoritesTitle: t("microflowNodePanelFavoritesTitle"),
          favoritesEmpty: t("microflowNodePanelFavoritesEmpty"),
          addToCanvas: t("microflowNodePanelAddToCanvas"),
          favorite: t("microflowNodePanelFavorite"),
          unfavorite: t("microflowNodePanelUnfavorite"),
          viewDocumentation: t("microflowNodePanelViewDocumentation"),
          copyNodeType: t("microflowNodePanelCopyNodeType"),
          copied: t("microflowNodePanelCopied"),
          disabled: t("microflowNodePanelDisabled"),
          emptyTitle: t("microflowNodePanelEmptyTitle"),
          emptyDescription: t("microflowNodePanelEmptyDescription"),
          clearSearch: t("microflowNodePanelClearSearch"),
          footerHint: t("microflowNodePanelFooterHint"),
          componentsPlaceholder: t("microflowNodePanelComponentsPlaceholder"),
          templatesPlaceholder: t("microflowNodePanelTemplatesPlaceholder"),
          inputs: t("microflowNodePanelInputs"),
          outputs: t("microflowNodePanelOutputs"),
          useCases: t("microflowNodePanelUseCases")
        }}
      />

      <Modal
        visible={publishOpen}
        title={t("microflowPublishTitle")}
        onCancel={() => setPublishOpen(false)}
        onOk={() => void handlePublish()}
        confirmLoading={publishing}
        okText={t("microflowToolbarPublish")}
      >
        <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
          <Input value={publishVersion} onChange={setPublishVersion} prefix={t("microflowPublishVersion")} />
          <Input value={releaseNote} onChange={setReleaseNote} prefix={t("microflowPublishReleaseNote")} />
          <Checkbox checked={overwriteCurrent} onChange={event => setOverwriteCurrent(Boolean(event.target.checked))}>
            {t("microflowPublishOverwrite")}
          </Checkbox>
        </Space>
      </Modal>
    </div>
  );
}
