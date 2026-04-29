import { useCallback, useEffect, useMemo, useRef, useState, type Ref } from "react";
import { Button, Modal, Space, Switch, Tag, Toast, Tooltip, Typography } from "@douyinfe/semi-ui";
import { IconArrowLeft } from "@douyinfe/semi-icons";
import { MicroflowEditor, type MicroflowApiClient, type MicroflowEditorHandle, type MicroflowSchema, type SaveMicroflowRequest } from "@atlas/microflow";
import type { MicroflowMetadataAdapter, MicroflowMetadataCatalog } from "@atlas/microflow/metadata";

import type { MicroflowResourceAdapter } from "../adapter/microflow-resource-adapter";
import type { MicroflowValidationAdapter } from "../adapter/microflow-validation-adapter";
import { getMicroflowApiError, getMicroflowErrorUserMessage, isVersionConflictError } from "../adapter/http/microflow-api-error";
import type { MicroflowAdapterMode } from "../config/microflow-adapter-config";
import { PublishMicroflowModal } from "../publish/PublishMicroflowModal";
import { MicroflowReferencesDrawer } from "../references/MicroflowReferencesDrawer";
import { MicroflowVersionsDrawer } from "../versions/MicroflowVersionsDrawer";
import type { MicroflowResource } from "../resource/resource-types";
import type { StudioMicroflowDefinitionView } from "../studio/studio-microflow-types";
import { canRunMicroflowAction, formatMicroflowDate, microflowPublishStatusLabel, microflowStatusColor, microflowStatusLabel } from "../resource/resource-utils";
import { createMicroflowEditorApiClient } from "./editor-save-bridge";
import { useMendixStudioStore } from "../../store";

const { Text } = Typography;
const AUTOSAVE_DEBOUNCE_MS = 4000;

type SaveReason = "manual" | "autosave" | "force";

interface SaveConflictDetails {
  remoteVersion?: string;
  remoteSchemaId?: string;
  remoteUpdatedAt?: string;
  remoteUpdatedBy?: string;
  remoteConcurrencyStamp?: string;
  baseVersion?: string;
}

export interface MendixMicroflowEditorEntryProps {
  resource: MicroflowResource;
  adapter: MicroflowResourceAdapter;
  workspaceId?: string;
  moduleId?: string;
  metadataAdapter?: MicroflowMetadataAdapter;
  metadataCatalog?: MicroflowMetadataCatalog;
  runtimeAdapter?: MicroflowApiClient;
  validationAdapter?: MicroflowValidationAdapter;
  adapterMode?: MicroflowAdapterMode;
  apiBaseUrl?: string;
  onSave?: (resource: MicroflowResource) => void;
  onPublish?: (resource: MicroflowResource) => void;
  onDirtyChange?: (dirty: boolean) => void;
  onOpenMicroflow?: (microflowId: string) => void;
  onRefreshResourceList?: () => void | Promise<void>;
  microflowResourceIndex?: Record<string, StudioMicroflowDefinitionView>;
  onBack?: () => void;
  readonly?: boolean;
  /**
   * Mendix Studio Workbench 在外置工具栏模式下需要远程触发保存 / 校验 / 运行
   * 等动作。宿主把 ref 透传给 MicroflowEditor，编辑器内部隐藏顶部工具栏，避免
   * 出现双层 toolbar；旧独立路由 `/microflow/:id/editor` 不传 toolbarMode，
   * 因此默认 internal 行为不受影响。
   */
  editorRef?: Ref<MicroflowEditorHandle>;
  toolbarMode?: "internal" | "external";
}

function parseSaveConflictDetails(details?: string): SaveConflictDetails {
  if (!details) {
    return {};
  }
  try {
    const parsed = JSON.parse(details) as SaveConflictDetails;
    if (parsed && typeof parsed === "object") {
      return parsed;
    }
  } catch {
    // 老后端可能只返回 schemaId/version 字符串，保留兼容展示。
  }
  return { remoteVersion: details, remoteSchemaId: details };
}

export function MendixMicroflowEditorEntry({ resource, adapter, workspaceId, moduleId, metadataAdapter, metadataCatalog, runtimeAdapter, validationAdapter, adapterMode, apiBaseUrl, onSave, onPublish, onDirtyChange, onOpenMicroflow, onRefreshResourceList, microflowResourceIndex, onBack, readonly, editorRef, toolbarMode }: MendixMicroflowEditorEntryProps) {
  const [schema, setSchema] = useState<MicroflowSchema>(resource.schema);
  const [autosaveEnabled, setAutosaveEnabled] = useState(false);
  const [publishOpen, setPublishOpen] = useState(false);
  const [versionsOpen, setVersionsOpen] = useState(false);
  const [referencesOpen, setReferencesOpen] = useState(false);
  const [currentResource, setCurrentResource] = useState(resource);
  const saveState = useMendixStudioStore(state => state.saveStateByMicroflowId[currentResource.id]);
  const markMicroflowDirty = useMendixStudioStore(state => state.markMicroflowDirty);
  const updateMicroflowSaveState = useMendixStudioStore(state => state.updateMicroflowSaveState);
  const setMicroflowValidationState = useMendixStudioStore(state => state.setMicroflowValidationState);
  const mountedRef = useRef(false);
  const saveRefreshSeqRef = useRef(0);
  const latestSchemaRef = useRef<MicroflowSchema>(resource.schema);
  const currentResourceRef = useRef(resource);
  const inFlightSaveRef = useRef<Promise<MicroflowResource> | undefined>();
  const queuedSaveRef = useRef<{ reason: SaveReason; force?: boolean } | undefined>();
  const autosaveTimerRef = useRef<number>();
  const saveRequestSeqRef = useRef(0);
  const effectiveReadonly = readonly || currentResource.archived || !(currentResource.permissions?.canEdit ?? true);
  const publishDisabled = currentResource.archived || !canRunMicroflowAction(currentResource, "canPublish");

  const clearAutosaveTimer = useCallback(() => {
    if (autosaveTimerRef.current !== undefined) {
      window.clearTimeout(autosaveTimerRef.current);
      autosaveTimerRef.current = undefined;
    }
  }, []);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      saveRefreshSeqRef.current += 1;
      clearAutosaveTimer();
    };
  }, [clearAutosaveTimer]);

  useEffect(() => {
    setCurrentResource(resource);
    currentResourceRef.current = resource;
    setSchema(resource.schema);
    latestSchemaRef.current = resource.schema;
    onDirtyChange?.(false);
    updateMicroflowSaveState(resource.id, {
      tabId: `microflow:${resource.id}`,
      dirty: false,
      saving: false,
      queued: false,
      status: "idle",
      schemaId: resource.schemaId,
      baseVersion: resource.schemaId || resource.version,
      localVersion: resource.version,
      remoteVersion: resource.version,
      lastError: undefined,
      conflict: undefined
    });
  }, [onDirtyChange, resource, updateMicroflowSaveState]);

  const applySavedResource = useCallback((saved: MicroflowResource, durationMs?: number) => {
    if (!mountedRef.current || saved.id !== currentResourceRef.current.id) {
      return;
    }
    const tabStillOpen = useMendixStudioStore.getState().workbenchTabs.some(tab => tab.microflowId === saved.id || tab.resourceId === saved.id);
    if (!tabStillOpen) {
      return;
    }
    currentResourceRef.current = saved;
    setCurrentResource(saved);
    setSchema(saved.schema);
    latestSchemaRef.current = saved.schema;
    onDirtyChange?.(false);
    onSave?.(saved);
    void onRefreshResourceList?.();
    updateMicroflowSaveState(saved.id, {
      tabId: `microflow:${saved.id}`,
      status: "saved",
      dirty: false,
      saving: false,
      queued: false,
      lastSavedAt: saved.updatedAt,
      lastSavedBy: saved.updatedBy || saved.ownerName || saved.ownerId,
      lastSaveDurationMs: durationMs,
      lastError: undefined,
      conflict: undefined,
      schemaId: saved.schemaId,
      baseVersion: saved.schemaId || saved.version,
      localVersion: saved.version,
      remoteVersion: saved.version
    });
  }, [onDirtyChange, onRefreshResourceList, onSave, updateMicroflowSaveState]);

  const saveLatestSchema = useCallback(async (reason: SaveReason, force = false): Promise<MicroflowResource> => {
    if (effectiveReadonly) {
      throw new Error("归档、只读或无编辑权限的微流不可保存。");
    }
    const resourceId = currentResourceRef.current.id;
    const currentSaveState = useMendixStudioStore.getState().saveStateByMicroflowId[resourceId];
    if (reason !== "force" && !currentSaveState?.dirty && !currentSaveState?.queued) {
      return currentResourceRef.current;
    }
    if (inFlightSaveRef.current) {
      queuedSaveRef.current = { reason, force };
      updateMicroflowSaveState(resourceId, {
        tabId: `microflow:${resourceId}`,
        status: "queued",
        dirty: true,
        saving: true,
        queued: true
      });
      return inFlightSaveRef.current;
    }

    const saveLoop = (async () => {
      let nextReason = reason;
      let nextForce = force;
      let lastSaved = currentResourceRef.current;
      do {
        const activeResource = currentResourceRef.current;
        const requestId = `${activeResource.id}:${Date.now()}:${++saveRequestSeqRef.current}`;
        const schemaToSave = latestSchemaRef.current;
        const startedAt = performance.now();
        queuedSaveRef.current = undefined;
        updateMicroflowSaveState(activeResource.id, {
          tabId: `microflow:${activeResource.id}`,
          status: nextReason === "autosave" ? "autosaving" : "saving",
          dirty: true,
          saving: true,
          queued: false,
          lastError: undefined,
          conflict: undefined,
          schemaId: activeResource.schemaId,
          baseVersion: activeResource.schemaId || activeResource.version,
          localVersion: activeResource.version
        });

        try {
          const saved = await adapter.saveMicroflowSchema(activeResource.id, schemaToSave, {
            baseVersion: nextForce ? undefined : activeResource.schemaId || activeResource.version,
            schemaId: activeResource.schemaId,
            version: activeResource.version,
            saveReason: nextReason,
            clientRequestId: requestId,
            force: nextForce
          });
          lastSaved = saved;
          currentResourceRef.current = saved;
          const queued = queuedSaveRef.current;
          if (queued) {
            updateMicroflowSaveState(saved.id, {
              tabId: `microflow:${saved.id}`,
              status: "queued",
              dirty: true,
              saving: true,
              queued: true,
              schemaId: saved.schemaId,
              baseVersion: saved.schemaId || saved.version,
              localVersion: saved.version,
              remoteVersion: saved.version
            });
            nextReason = queued.reason;
            nextForce = queued.force === true;
            continue;
          }
          applySavedResource(saved, Math.round(performance.now() - startedAt));
          return saved;
        } catch (caught) {
          const apiError = getMicroflowApiError(caught);
          if (isVersionConflictError(apiError)) {
            const conflictDetails = parseSaveConflictDetails(apiError.details);
            updateMicroflowSaveState(activeResource.id, {
              tabId: `microflow:${activeResource.id}`,
              status: "conflict",
              dirty: true,
              saving: false,
              queued: false,
              lastError: apiError,
              conflict: {
                microflowId: activeResource.id,
                localVersion: activeResource.version,
                baseVersion: conflictDetails.baseVersion ?? activeResource.schemaId ?? activeResource.version,
                remoteVersion: conflictDetails.remoteVersion ?? conflictDetails.remoteSchemaId ?? apiError.details,
                remoteUpdatedAt: conflictDetails.remoteUpdatedAt,
                remoteUpdatedBy: conflictDetails.remoteUpdatedBy,
                traceId: apiError.traceId,
                message: apiError.message || "微流 Schema 已被其他保存更新。"
              }
            });
          } else {
            updateMicroflowSaveState(activeResource.id, {
              tabId: `microflow:${activeResource.id}`,
              status: "error",
              dirty: true,
              saving: false,
              queued: Boolean(queuedSaveRef.current),
              lastError: apiError,
              conflict: undefined
            });
          }
          onDirtyChange?.(true);
          throw caught;
        }
      } while (queuedSaveRef.current);
      return lastSaved;
    })();

    inFlightSaveRef.current = saveLoop;
    try {
      return await saveLoop;
    } finally {
      if (inFlightSaveRef.current === saveLoop) {
        inFlightSaveRef.current = undefined;
      }
    }
  }, [adapter, applySavedResource, effectiveReadonly, onDirtyChange, updateMicroflowSaveState]);

  const apiClient = useMemo(() => createMicroflowEditorApiClient(adapter, currentResource, runtimeAdapter, {
    saveMicroflow: async (request: SaveMicroflowRequest) => {
      latestSchemaRef.current = request.schema;
      return saveLatestSchema("manual");
    }
  }), [adapter, currentResource, runtimeAdapter, saveLatestSchema]);

  const scheduleAutosave = useCallback(() => {
    clearAutosaveTimer();
    if (!autosaveEnabled || effectiveReadonly) {
      return;
    }
    autosaveTimerRef.current = window.setTimeout(() => {
      autosaveTimerRef.current = undefined;
      if (!mountedRef.current || !latestSchemaRef.current || saveState?.status === "conflict") {
        return;
      }
      void saveLatestSchema("autosave").catch(caught => {
        Toast.error(`自动保存失败：${getMicroflowErrorUserMessage(caught)}`);
      });
    }, AUTOSAVE_DEBOUNCE_MS);
  }, [autosaveEnabled, clearAutosaveTimer, effectiveReadonly, saveLatestSchema, saveState?.status]);

  const handleReloadRemote = useCallback(async () => {
    const microflowId = currentResourceRef.current.id;
    try {
      clearAutosaveTimer();
      const [loadedResource, loadedSchema] = await Promise.all([
        adapter.getMicroflow(microflowId),
        adapter.getMicroflowSchema(microflowId)
      ]);
      if (!loadedResource) {
        throw new Error("远端微流已不存在。");
      }
      const nextResource = { ...loadedResource, schema: loadedSchema };
      applySavedResource(nextResource);
      Toast.success("已重新加载远端版本");
    } catch (caught) {
      Toast.error(getMicroflowErrorUserMessage(caught));
    }
  }, [adapter, applySavedResource, clearAutosaveTimer]);

  const handleKeepLocal = useCallback(() => {
    const microflowId = currentResourceRef.current.id;
    updateMicroflowSaveState(microflowId, {
      tabId: `microflow:${microflowId}`,
      status: "dirty",
      dirty: true,
      saving: false,
      queued: false,
      conflict: undefined
    });
    onDirtyChange?.(true);
  }, [onDirtyChange, updateMicroflowSaveState]);

  const handleForceSave = useCallback(() => {
    Modal.confirm({
      title: "覆盖远端版本？",
      content: "Force Save 会用当前本地 schema 覆盖远端最新保存。此操作不会做自动合并。",
      okText: "确认覆盖",
      cancelText: "取消",
      onOk: () => {
        void saveLatestSchema("force", true).then(saved => {
          Toast.success(`已覆盖保存 ${saved.version}`);
        }).catch(caught => {
          Toast.error(getMicroflowErrorUserMessage(caught));
        });
      }
    });
  }, [saveLatestSchema]);

  useEffect(() => {
    const handleSaveRequest = (event: Event) => {
      const detail = (event as CustomEvent<{ microflowId?: string; onSaved?: () => void; onError?: (error: unknown) => void }>).detail;
      if (detail?.microflowId !== currentResourceRef.current.id) {
        return;
      }
      void saveLatestSchema("manual").then(() => {
        detail.onSaved?.();
      }).catch(caught => {
        detail.onError?.(caught);
        Toast.error(getMicroflowErrorUserMessage(caught));
      });
    };
    window.addEventListener("atlas:microflow-save-request", handleSaveRequest);
    return () => window.removeEventListener("atlas:microflow-save-request", handleSaveRequest);
  }, [saveLatestSchema]);

  return (
    <div style={{ height: "100%", minHeight: 720, overflow: "hidden", background: "var(--semi-color-bg-0)" }}>
      <MicroflowEditor
        key={`${currentResource.id}:${currentResource.schemaId}:${currentResource.version}`}
        schema={schema}
        apiClient={apiClient}
        metadataAdapter={metadataAdapter}
        metadataCatalog={metadataCatalog}
        metadataWorkspaceId={workspaceId}
        metadataModuleId={moduleId ?? currentResource.moduleId}
        validationAdapter={validationAdapter}
        readonly={effectiveReadonly}
        editorRef={editorRef}
        toolbarMode={toolbarMode}
        onSchemaChange={nextSchema => {
          setSchema(nextSchema);
          latestSchemaRef.current = nextSchema;
          if (inFlightSaveRef.current) {
            queuedSaveRef.current = { reason: autosaveEnabled ? "autosave" : "manual" };
          }
          onDirtyChange?.(true);
          markMicroflowDirty(currentResource.id, true);
          updateMicroflowSaveState(currentResource.id, {
            tabId: `microflow:${currentResource.id}`,
            status: inFlightSaveRef.current ? "queued" : "dirty",
            dirty: true,
            saving: Boolean(inFlightSaveRef.current),
            queued: Boolean(inFlightSaveRef.current),
            schemaId: currentResource.schemaId,
            baseVersion: currentResource.schemaId || currentResource.version,
            localVersion: currentResource.version,
            lastError: undefined
          });
          scheduleAutosave();
        }}
        onSaveComplete={response => {
          const saved = currentResourceRef.current;
          if (response.microflowId === saved.id) {
            applySavedResource(saved);
          }
        }}
        onValidationStateChange={setMicroflowValidationState}
        onPublish={() => {
          if (!publishDisabled) {
            setPublishOpen(true);
          }
        }}
        toolbarPrefix={
          <Space align="center">
            {onBack ? (
              <Button icon={<IconArrowLeft />} theme="borderless" onClick={onBack}>
                返回资源库
              </Button>
            ) : null}
            <Text strong>{currentResource.displayName || currentResource.name}</Text>
            <Tag color={microflowStatusColor(currentResource.status)}>{microflowStatusLabel(currentResource.status)}</Tag>
            <Tag>{currentResource.version}</Tag>
            <Tag color={currentResource.publishStatus === "changedAfterPublish" ? "orange" : currentResource.publishStatus === "published" ? "green" : "grey"}>{microflowPublishStatusLabel(currentResource.publishStatus)}</Tag>
            <Tag color="blue">Latest {currentResource.latestPublishedVersion ?? "-"}</Tag>
            {adapterMode ? <Tag color={adapterMode === "http" ? "green" : "orange"}>{adapterMode === "http" ? `HTTP ${apiBaseUrl ?? ""}`.trim() : "本地模拟数据"}</Tag> : null}
            {currentResource.archived ? <Tag color="grey">只读归档</Tag> : null}
            <Text type="tertiary">{currentResource.updatedBy || currentResource.ownerName || "-"} · {formatMicroflowDate(currentResource.updatedAt)}</Text>
          </Space>
        }
        toolbarSuffix={
          <Space>
            <Tooltip content={autosaveEnabled ? `自动保存已开启，${AUTOSAVE_DEBOUNCE_MS / 1000}s debounce` : "自动保存关闭，需手动 Save"}>
              <Space>
                <Text size="small" type="tertiary">Autosave</Text>
                <Switch size="small" checked={autosaveEnabled} disabled={effectiveReadonly} onChange={setAutosaveEnabled} />
              </Space>
            </Tooltip>
            {saveState ? (
              <Tag color={saveState.status === "error" ? "red" : saveState.status === "conflict" ? "orange" : saveState.saving ? "blue" : saveState.dirty ? "orange" : "green"}>
                {saveState.status}
                {saveState.lastSavedAt ? ` · ${formatMicroflowDate(saveState.lastSavedAt)}` : ""}
                {saveState.lastSaveDurationMs ? ` · ${saveState.lastSaveDurationMs}ms` : ""}
              </Tag>
            ) : null}
            {saveState?.lastSavedBy ? <Text size="small" type="tertiary">by {saveState.lastSavedBy}</Text> : null}
            {saveState?.lastError ? (
              <Tooltip content={`${getMicroflowErrorUserMessage(saveState.lastError)}${saveState.lastError.traceId ? ` traceId=${saveState.lastError.traceId}` : ""}`}>
                <Tag color="red">保存失败{saveState.lastError.traceId ? ` · ${saveState.lastError.traceId}` : ""}</Tag>
              </Tooltip>
            ) : null}
            <Button size="small" disabled={publishDisabled} onClick={() => setPublishOpen(true)}>发布</Button>
            <Button size="small" onClick={() => setVersionsOpen(true)}>版本</Button>
            <Button size="small" onClick={() => setReferencesOpen(true)}>
              引用{typeof currentResource.referenceCount === "number" && currentResource.referenceCount > 0 ? ` ${currentResource.referenceCount}` : ""}
            </Button>
          </Space>
        }
      />
      <PublishMicroflowModal
        visible={publishOpen}
        resource={currentResource}
        adapter={adapter}
        validationAdapter={validationAdapter}
        dirty={Boolean(saveState?.dirty)}
        onSaveBeforePublish={() => saveLatestSchema("manual")}
        onClose={() => setPublishOpen(false)}
        onPublished={published => {
          setCurrentResource(published);
          setSchema(published.schema);
          latestSchemaRef.current = published.schema;
          currentResourceRef.current = published;
          markMicroflowDirty(published.id, false);
          updateMicroflowSaveState(published.id, {
            tabId: `microflow:${published.id}`,
            status: "saved",
            dirty: false,
            saving: false,
            queued: false,
            schemaId: published.schemaId,
            baseVersion: published.schemaId || published.version,
            localVersion: published.version,
            remoteVersion: published.version,
            lastSavedAt: published.updatedAt,
            lastSavedBy: published.updatedBy || published.ownerName || published.ownerId,
            lastError: undefined,
            conflict: undefined
          });
          onDirtyChange?.(false);
          onPublish?.(published);
          Toast.success("微流发布成功");
        }}
        onViewProblems={issues => Toast.warning(`当前有 ${issues.length} 个校验问题，请查看编辑器问题面板。`)}
        onViewReferences={() => {
          setPublishOpen(false);
          setReferencesOpen(true);
        }}
      />
      <Modal
        title="保存冲突"
        visible={saveState?.status === "conflict"}
        onCancel={() => undefined}
        footer={
          <Space>
            <Button onClick={handleReloadRemote}>Reload Remote</Button>
            <Button onClick={handleKeepLocal}>Keep Local</Button>
            <Button type="danger" onClick={handleForceSave}>Force Save</Button>
            <Button onClick={() => undefined}>Cancel</Button>
          </Space>
        }
      >
        <Space vertical align="start" style={{ width: "100%" }}>
          <Text strong>{currentResource.displayName || currentResource.name}</Text>
          <Text>该微流已被其他保存更新，当前本地更改未保存。</Text>
          <Text type="tertiary">local version: {saveState?.conflict?.localVersion ?? currentResource.version}</Text>
          <Text type="tertiary">baseVersion: {saveState?.conflict?.baseVersion ?? currentResource.schemaId}</Text>
          <Text type="tertiary">remote version: {saveState?.conflict?.remoteVersion ?? "后端未返回"}</Text>
          <Text type="tertiary">remote updatedAt: {saveState?.conflict?.remoteUpdatedAt ?? "后端未返回"}</Text>
          <Text type="tertiary">remote updatedBy: {saveState?.conflict?.remoteUpdatedBy ?? "后端未返回"}</Text>
          <Text type="tertiary">traceId: {saveState?.conflict?.traceId ?? saveState?.lastError?.traceId ?? "-"}</Text>
        </Space>
      </Modal>
      <MicroflowVersionsDrawer
        visible={versionsOpen}
        resource={currentResource}
        adapter={adapter}
        onClose={() => setVersionsOpen(false)}
        onResourceChange={next => {
          setCurrentResource(next);
          setSchema(next.schema);
          latestSchemaRef.current = next.schema;
          currentResourceRef.current = next;
          markMicroflowDirty(next.id, false);
          updateMicroflowSaveState(next.id, {
            tabId: `microflow:${next.id}`,
            status: "saved",
            dirty: false,
            saving: false,
            queued: false,
            schemaId: next.schemaId,
            baseVersion: next.schemaId || next.version,
            localVersion: next.version,
            remoteVersion: next.version,
            lastSavedAt: next.updatedAt,
            lastSavedBy: next.updatedBy || next.ownerName || next.ownerId,
            lastError: undefined,
            conflict: undefined
          });
          onDirtyChange?.(false);
          onSave?.(next);
        }}
        onCreated={() => undefined}
      />
      <MicroflowReferencesDrawer
        visible={referencesOpen}
        resource={currentResource}
        adapter={adapter}
        resourceIndex={microflowResourceIndex}
        getCurrentSchema={() => schema}
        onOpenMicroflow={onOpenMicroflow}
        onRefreshResourceList={onRefreshResourceList}
        onClose={() => setReferencesOpen(false)}
      />
    </div>
  );
}

export const MicroflowEditorEntry = MendixMicroflowEditorEntry;
