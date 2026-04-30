import "./studio.css";

import { useEffect, useMemo, useRef, useState } from "react";
import { Button, Card, Modal, SideSheet, Space, Toast, Typography } from "@douyinfe/semi-ui";
import type { MicroflowEditorHandle, MicroflowWorkbenchLayoutState, MicroflowWorkbenchStatus } from "@atlas/microflow";

import type { MicroflowAdapterFactoryConfig } from "./microflow/config/microflow-adapter-config";
import type { MicroflowAdapterBundle } from "./microflow/adapter/microflow-adapter-factory";
import { createMicroflowAdapterBundle } from "./microflow/adapter/microflow-adapter-factory";
import { StudioHeader } from "./components/studio-header";
import { AppExplorer } from "./components/app-explorer";
import { ExplorerSplitLayout } from "./components/explorer-split-layout";
import { WidgetToolbox } from "./components/widget-toolbox";
import { WorkbenchTabs } from "./components/workbench-tabs";
import { WorkbenchToolbar } from "./components/workbench-toolbar";
import { MicroflowWorkbenchToolbar } from "./components/microflow-workbench-toolbar";
import { MicroflowStudioBottomPanel } from "./components/microflow-studio-bottom-panel";
import { WorkbenchCommandPalette } from "./components/workbench-command-palette";
import { PageDesignerCanvas } from "./components/page-designer-canvas";
import { WidgetStructurePanel } from "./components/widget-structure-panel";
import { PropertiesPanel } from "./components/properties-panel";
import { RightInspectorRail } from "./components/right-inspector-rail";
import { BottomPanel } from "./components/bottom-panel";
import { RuntimePreview } from "./components/runtime-preview";
import { useMendixStudioStore } from "./store";
import { MicroflowResourceEditorHost } from "./microflow/studio/MicroflowResourceEditorHost";
import { mapMicroflowResourceToStudioDefinitionView } from "./microflow/studio/studio-microflow-mappers";
import { MicroflowReferencesDrawer } from "./microflow/references/MicroflowReferencesDrawer";
import { MicroflowWorkbenchCommandBus } from "./microflow/workbench/microflow-workbench-command-bus";
import { resolveMicroflowActivation } from "./microflow/workbench/microflow-activation-guard";
import { MicroflowWorkbenchErrorBus } from "./microflow/workbench/microflow-workbench-error-bus";
import { getMendixStudioCopy } from "./i18n/copy";

interface PendingMicroflowActivation {
  microflowId: string;
  reason: "explorer" | "deeplink" | "references";
}

export interface MendixStudioAppProps {
  appId?: string;
  workspaceId?: string;
  tenantId?: string;
  currentUser?: MicroflowAdapterFactoryConfig["currentUser"];
  adapterConfig?: MicroflowAdapterFactoryConfig;
  adapterBundle?: MicroflowAdapterBundle;
  useNativeMicroflowDocking?: boolean;
}

const { Text } = Typography;

export function MendixStudioApp({
  appId,
  workspaceId,
  tenantId,
  currentUser,
  adapterConfig,
  adapterBundle,
  useNativeMicroflowDocking = true,
}: MendixStudioAppProps) {
  const copy = getMendixStudioCopy();
  const activeTab = useMendixStudioStore(state => state.activeTab);
  const activeWorkbenchTab = useMendixStudioStore(state =>
    state.activeWorkbenchTabId
      ? state.workbenchTabs.find(tab => tab.id === state.activeWorkbenchTabId)
      : undefined
  );
  const dirtyByWorkbenchTabId = useMendixStudioStore(state => state.dirtyByWorkbenchTabId);
  const saveStateByMicroflowId = useMendixStudioStore(state => state.saveStateByMicroflowId);
  const setStudioContext = useMendixStudioStore(state => state.setStudioContext);
  const microflowResourcesById = useMendixStudioStore(state => state.microflowResourcesById);
  const markMicroflowDirty = useMendixStudioStore(state => state.markMicroflowDirty);
  const upsertStudioMicroflow = useMendixStudioStore(state => state.upsertStudioMicroflow);
  const updateMicroflowWorkbenchTabFromResource = useMendixStudioStore(state => state.updateMicroflowWorkbenchTabFromResource);
  const openMicroflowWorkbenchTab = useMendixStudioStore(state => state.openMicroflowWorkbenchTab);
  const closeWorkbenchTab = useMendixStudioStore(state => state.closeWorkbenchTab);
  const [microflowResourceRefreshToken, setMicroflowResourceRefreshToken] = useState(0);
  const [referencesMicroflowId, setReferencesMicroflowId] = useState<string>();
  const [openedDeepLinkMicroflowId, setOpenedDeepLinkMicroflowId] = useState<string>();
  const [microflowWorkbenchStatus, setMicroflowWorkbenchStatus] = useState<MicroflowWorkbenchStatus | null>(null);
  const [microflowWorkbenchLayout, setMicroflowWorkbenchLayout] = useState<MicroflowWorkbenchLayoutState | null>(null);
  const [commandBus] = useState(() => new MicroflowWorkbenchCommandBus());
  const [errorBus] = useState(() => new MicroflowWorkbenchErrorBus({
    onUnauthorizedRedirect: () => {
      if (typeof window === "undefined") {
        return;
      }
      const redirectTarget = `${window.location.pathname}${window.location.search}${window.location.hash}`;
      Toast.error("登录已失效，正在跳转到登录页。");
      window.setTimeout(() => {
        window.location.assign(`/sign?redirect=${encodeURIComponent(redirectTarget)}`);
      }, 0);
    },
    onOpenProblems: () => {
      void commandBus.execute("microflow.openPanel", { panel: "problems" }).catch(() => undefined);
    },
  }));
  const [errorBusSnapshot, setErrorBusSnapshot] = useState(() => errorBus.getSnapshot());
  const [pendingMicroflowActivation, setPendingMicroflowActivation] = useState<PendingMicroflowActivation>();
  const [commandPaletteOpen, setCommandPaletteOpen] = useState(false);

  // 创建 adapter bundle；如果构建失败，仅 console.warn，不阻断页面渲染。
  const _resolvedBundle = useMemo<MicroflowAdapterBundle | undefined>(() => {
    if (adapterBundle) return adapterBundle;
    try {
      return createMicroflowAdapterBundle({
        ...adapterConfig,
        workspaceId: adapterConfig?.workspaceId ?? workspaceId,
        tenantId: adapterConfig?.tenantId ?? tenantId,
        currentUser: adapterConfig?.currentUser ?? currentUser,
      });
    } catch (err) {
      console.warn("[MendixStudioApp] Failed to create microflow adapter bundle:", err);
      return undefined;
    }
  }, [adapterBundle, adapterConfig, workspaceId, tenantId, currentUser]);

  // 把 appId/workspaceId 同步到 store 基础上下文（不触发 API 请求）。
  useEffect(() => {
    setStudioContext({ appId, workspaceId });
  }, [appId, workspaceId, setStudioContext]);

  useEffect(() => {
    if (typeof window === "undefined") {
      return;
    }

    const microflowId = new URLSearchParams(window.location.search).get("microflowId")?.trim();
    if (!microflowId || openedDeepLinkMicroflowId === microflowId) {
      return;
    }

    if (!microflowResourcesById[microflowId]) {
      return;
    }

    requestOpenMicroflow(microflowId, "deeplink");
    setOpenedDeepLinkMicroflowId(microflowId);
  }, [microflowResourcesById, openedDeepLinkMicroflowId]);

  const isMicroflow = activeWorkbenchTab?.kind === "microflow";
  const activeMicroflowId = isMicroflow
    ? activeWorkbenchTab.microflowId ?? activeWorkbenchTab.resourceId
    : undefined;
  const activeMicroflowResource = activeMicroflowId
    ? microflowResourcesById[activeMicroflowId]
    : undefined;
  const activeMicroflowTabId = isMicroflow ? activeWorkbenchTab.id : undefined;
  const hasActiveWorkbenchTab = Boolean(activeWorkbenchTab);
  // The workbench-level toolbar drives the active microflow editor through this
  // shared imperative ref. The ref is reset whenever the active microflow tab
  // changes (via the editor's `key` prop) so each tab has an isolated handle.
  const microflowEditorHandleRef = useRef<MicroflowEditorHandle | null>(null);
  const referencesResource = referencesMicroflowId
    ? microflowResourcesById[referencesMicroflowId]
    : undefined;
  const openReferencesPanel = (microflowId: string) => {
    setReferencesMicroflowId(microflowId);
  };

  const activateMicroflowWorkbenchTab = (microflowId: string) => {
    openMicroflowWorkbenchTab(microflowId);
    useMendixStudioStore.getState().setSelected("microflow", microflowId);
    useMendixStudioStore.getState().setSelectedExplorerNodeId(`microflow:${microflowId}`);
    const resource = microflowResourcesById[microflowId];
    if (resource?.moduleId) {
      useMendixStudioStore.getState().setActiveModuleId(resource.moduleId);
    }
  };

  const requestOpenMicroflow = (microflowId: string, reason: PendingMicroflowActivation["reason"]) => {
    const decision = resolveMicroflowActivation({
      targetMicroflowId: microflowId,
      activeWorkbenchTabId: useMendixStudioStore.getState().activeWorkbenchTabId,
      workbenchTabs: useMendixStudioStore.getState().workbenchTabs,
      dirtyByWorkbenchTabId: useMendixStudioStore.getState().dirtyByWorkbenchTabId,
    });
    if (decision.kind === "confirm-dirty") {
      setPendingMicroflowActivation({ microflowId, reason });
      return;
    }
    activateMicroflowWorkbenchTab(microflowId);
  };

  useEffect(() => {
    commandBus.bindContext({
      microflowId: activeMicroflowId,
      tabId: activeMicroflowTabId,
      getEditorHandle: () => microflowEditorHandleRef.current,
      openReferencesPanel,
    });
  }, [activeMicroflowId, activeMicroflowTabId, commandBus]);

  useEffect(() => {
    if (activeMicroflowId !== microflowWorkbenchStatus?.microflowId) {
      setMicroflowWorkbenchStatus(null);
      setMicroflowWorkbenchLayout(null);
    }
  }, [activeMicroflowId, microflowWorkbenchStatus?.microflowId]);

  useEffect(() => {
    const unsubscribe = errorBus.subscribe(setErrorBusSnapshot);
    const detach = errorBus.attach();
    return () => {
      unsubscribe();
      detach();
      errorBus.dispose();
    };
  }, [errorBus]);

  useEffect(() => {
    if (activeMicroflowId) {
      errorBus.clearReadonlyReason();
    }
  }, [activeMicroflowId, errorBus]);

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      const commandKey = event.ctrlKey || event.metaKey;
      const isEditable = event.target instanceof HTMLElement
        && Boolean(event.target.closest("input, textarea, select, [contenteditable='true']"));
      if (isEditable) {
        return;
      }
      if (commandKey && event.key.toLowerCase() === "k" && activeMicroflowId) {
        event.preventDefault();
        setCommandPaletteOpen(true);
      }
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [activeMicroflowId]);

  useEffect(() => {
    const hasDirtyTab = Object.values(dirtyByWorkbenchTabId).some(Boolean);
    const hasSavingTab = Object.values(saveStateByMicroflowId).some(state => state.saving || state.queued);
    if (!hasDirtyTab && !hasSavingTab) {
      return undefined;
    }

    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [dirtyByWorkbenchTabId, saveStateByMicroflowId]);

  if (!_resolvedBundle) {
    return (
      <div
        className="mendix-studio-root"
        style={{
          display: "flex",
          flexDirection: "column",
          height: "100vh",
          width: "100%",
          overflow: "hidden",
          background: "#f0f2f5"
        }}
        data-app-id={appId}
      >
        <StudioHeader />
        <div style={{ flex: 1, display: "flex", alignItems: "center", justifyContent: "center", padding: 24 }}>
          <Card style={{ width: 520, borderRadius: 12 }}>
            <Space vertical align="start" spacing={12}>
              <Text strong>{copy.app.initFailedTitle}</Text>
              <Text type="tertiary" size="small">
                {copy.app.initFailedDescription}
              </Text>
              <Text size="small">{copy.app.workspaceIdLabel}: {workspaceId || "-"}</Text>
              <Text size="small">{copy.app.appIdLabel}: {appId || "-"}</Text>
              <Button onClick={() => window.location.reload()}>{copy.common.refreshPage}</Button>
            </Space>
          </Card>
        </div>
      </div>
    );
  }

  return (
    <div
      className="mendix-studio-root"
      style={{
        display: "flex",
        flexDirection: "column",
        height: "100vh",
        width: "100%",
        overflow: "hidden",
        background: "#f0f2f5"
      }}
      data-app-id={appId}
    >
      {/* 顶部 Header */}
      <StudioHeader />

      {/* 主体区域 */}
      <div
        style={{
          display: "flex",
          flexDirection: "row",
          flex: 1,
          minHeight: 0,
          minWidth: 0,
          overflow: "hidden"
        }}
      >
        <ExplorerSplitLayout
          mode={isMicroflow ? "microflowDesigner" : "normal"}
          defaultCollapsed={isMicroflow}
          activeTool={isMicroflow ? "toolbox" : "explorer"}
          explorer={<AppExplorer adapterBundle={_resolvedBundle} appId={appId} workspaceId={workspaceId} refreshToken={microflowResourceRefreshToken} onViewMicroflowReferences={openReferencesPanel} onOpenMicroflow={microflowId => requestOpenMicroflow(microflowId, "explorer")} />}
        >
          <div
            style={{
              display: "flex",
              flexDirection: "row",
              flex: 1,
              minWidth: 0,
              overflow: "hidden"
            }}
          >
            {/* 中间工作台 */}
            <div
              style={{
                display: "flex",
                flexDirection: "column",
                flex: 1,
                minWidth: 0,
                overflow: "hidden"
              }}
            >
              {/* Tab 栏 */}
              <WorkbenchTabs />

              {/* 工具栏：仅在已打开资源时显示，避免空工作台自动呈现 Page/Workflow 操作。 */}
              {hasActiveWorkbenchTab ? (
                isMicroflow ? (
                  <MicroflowWorkbenchToolbar
                    microflowId={activeMicroflowId}
                    editorRef={microflowEditorHandleRef}
                    status={microflowWorkbenchStatus}
                    commandBus={commandBus}
                    onViewReferences={openReferencesPanel}
                  />
                ) : (
                  <WorkbenchToolbar onViewMicroflowReferences={openReferencesPanel} />
                )
              ) : null}

              {/* 内容区 */}
              {!hasActiveWorkbenchTab ? (
                <div
                  style={{
                    flex: 1,
                    minHeight: 0,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    background: "#f8fafc"
                  }}
                  data-testid="mendix-studio-empty-workbench"
                >
                  <Card style={{ width: 420, borderRadius: 8 }}>
                    <Space vertical align="start" spacing={8}>
                      <Text strong>{copy.app.emptyWorkbenchTitle}</Text>
                      <Text type="tertiary" size="small">
                        {copy.app.emptyWorkbenchDescription}
                      </Text>
                    </Space>
                  </Card>
                </div>
              ) : isMicroflow ? (
                <div
                  style={{
                    flex: 1,
                    minHeight: 0,
                    overflow: "hidden",
                    display: "flex",
                    flexDirection: "column"
                  }}
                >
                  <div style={{ flex: 1, minHeight: 0, display: "flex", flexDirection: "column" }}>
                  {activeMicroflowId && activeMicroflowTabId ? (
                    <MicroflowResourceEditorHost
                      key={activeMicroflowTabId}
                      microflowId={activeMicroflowId}
                      workspaceId={workspaceId}
                      moduleId={activeWorkbenchTab?.moduleId ?? activeMicroflowResource?.moduleId}
                      adapterBundle={_resolvedBundle}
                      microflowResourceIndex={microflowResourcesById}
                      editorRef={microflowEditorHandleRef}
                      toolbarMode="external"
                      shellMode={useNativeMicroflowDocking ? "editor-native-layout" : "legacy-host-layout"}
                      onLayoutStateChange={setMicroflowWorkbenchLayout}
                      onWorkbenchStatusChange={setMicroflowWorkbenchStatus}
                      readonly={Boolean(errorBusSnapshot.readonlyReason)}
                      onRefreshResourceList={() => setMicroflowResourceRefreshToken(token => token + 1)}
                      onCloseTab={() => closeWorkbenchTab(activeMicroflowTabId, { force: true })}
                      onOpenMicroflow={openMicroflowWorkbenchTab}
                      onDirtyChange={dirty => markMicroflowDirty(activeMicroflowId, dirty)}
                      readonly={Boolean(errorBusSnapshot.readonlyReason)}
                      onResourceUpdated={resource => {
                        const view = mapMicroflowResourceToStudioDefinitionView(resource);
                        upsertStudioMicroflow(view);
                        updateMicroflowWorkbenchTabFromResource(view);
                      }}
                    />
                  ) : (
                    <div
                      style={{
                        flex: 1,
                        minHeight: 0,
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        background: "#fff"
                      }}
                    >
                      <Card style={{ width: 420, borderRadius: 12 }}>
                        <Text strong>
                          {activeMicroflowId ? copy.app.microflowMissingTitle : copy.app.microflowTabMissingIdTitle}
                        </Text>
                        <div style={{ marginTop: 8 }}>
                          <Text type="tertiary" size="small">
                            {copy.app.microflowMissingDescription}
                          </Text>
                        </div>
                      </Card>
                    </div>
                  )}
                  {!useNativeMicroflowDocking ? (
                    <MicroflowStudioBottomPanel
                      microflowId={activeMicroflowId}
                      resource={activeMicroflowResource}
                      adapterBundle={_resolvedBundle}
                    />
                  ) : null}
                  </div>
                </div>
              ) : (
                <>
                  <div
                    style={{
                      display: "flex",
                      flex: 1,
                      minHeight: 0,
                      overflow: "hidden"
                    }}
                  >
                    {/* 只在 pageBuilder Tab 显示 Widget Toolbox */}
                    {activeTab === "pageBuilder" && <WidgetToolbox />}

                    {/* 中央画布 */}
                    <PageDesignerCanvas />

                    {/* 组件结构树 */}
                    <WidgetStructurePanel />
                  </div>

                  {/* 底部 Errors + Debug Trace */}
                  <BottomPanel />
                </>
              )}
            </div>

            {/* 右侧属性面板（微流模式下隐藏，MicroflowEditor 自带） */}
            {hasActiveWorkbenchTab && !isMicroflow && <PropertiesPanel />}

            {/* 最右侧 Inspector Rail（微流模式下隐藏） */}
            {hasActiveWorkbenchTab && !isMicroflow && <RightInspectorRail />}
          </div>
        </ExplorerSplitLayout>
      </div>

      {/* 运行预览侧拉板 */}
      <RuntimePreview />
      {_resolvedBundle?.resourceAdapter ? (
        <MicroflowReferencesDrawer
          visible={Boolean(referencesMicroflowId)}
          resource={referencesResource}
          adapter={_resolvedBundle.resourceAdapter}
          resourceIndex={microflowResourcesById}
          onOpenMicroflow={microflowId => requestOpenMicroflow(microflowId, "references")}
          onRefreshResourceList={() => setMicroflowResourceRefreshToken(token => token + 1)}
          onClose={() => setReferencesMicroflowId(undefined)}
        />
      ) : null}
      <WorkbenchCommandPalette
        visible={commandPaletteOpen}
        status={microflowWorkbenchStatus}
        commandBus={commandBus}
        onClose={() => setCommandPaletteOpen(false)}
      />
      <Modal
        title="切换前处理未保存更改"
        visible={Boolean(pendingMicroflowActivation)}
        onCancel={() => setPendingMicroflowActivation(undefined)}
        footer={
          <Space>
            <Button
              type="primary"
              disabled={!activeMicroflowId || Boolean(activeMicroflowId && saveStateByMicroflowId[activeMicroflowId]?.saving)}
              loading={Boolean(activeMicroflowId && saveStateByMicroflowId[activeMicroflowId]?.saving)}
              onClick={() => {
                if (!activeMicroflowId || !pendingMicroflowActivation) {
                  return;
                }
                window.dispatchEvent(new CustomEvent("atlas:microflow-save-request", {
                  detail: {
                    microflowId: activeMicroflowId,
                    onSaved: () => {
                      const nextTarget = pendingMicroflowActivation;
                      setPendingMicroflowActivation(undefined);
                      activateMicroflowWorkbenchTab(nextTarget.microflowId);
                    },
                  },
                }));
              }}
            >
              Save
            </Button>
            <Button onClick={() => setPendingMicroflowActivation(undefined)}>
              Cancel
            </Button>
            <Button
              type="danger"
              onClick={() => {
                const currentActiveTabId = useMendixStudioStore.getState().activeWorkbenchTabId;
                const nextTarget = pendingMicroflowActivation;
                setPendingMicroflowActivation(undefined);
                if (currentActiveTabId) {
                  closeWorkbenchTab(currentActiveTabId, { force: true });
                }
                if (nextTarget) {
                  activateMicroflowWorkbenchTab(nextTarget.microflowId);
                }
              }}
            >
              Discard
            </Button>
          </Space>
        }
      >
        <Space vertical align="start" spacing={8}>
          <Text strong>{activeWorkbenchTab?.title ?? "当前微流"}</Text>
          <Text type="tertiary">当前微流存在未保存更改。为避免切换后丢失本地草稿，请先保存，或显式放弃更改后再打开目标微流。</Text>
          {pendingMicroflowActivation ? (
            <Text type="tertiary">
              目标微流：{microflowResourcesById[pendingMicroflowActivation.microflowId]?.displayName ?? pendingMicroflowActivation.microflowId}
            </Text>
          ) : null}
        </Space>
      </Modal>
      <SideSheet
        visible={Boolean(errorBusSnapshot.activeError)}
        title={errorBusSnapshot.activeError?.category === "permission" ? "微流权限受限" : "微流工作台错误"}
        width="min(640px, calc(100vw - 32px))"
        onCancel={() => errorBus.clearActiveError()}
        footer={
          <Space>
            <Button onClick={() => errorBus.clearActiveError()}>关闭</Button>
            {errorBusSnapshot.activeError?.category === "validation" ? (
              <Button
                type="primary"
                onClick={() => {
                  void commandBus.execute("microflow.openPanel", { panel: "problems" }).catch(() => undefined);
                  errorBus.clearActiveError();
                }}
              >
                打开问题面板
              </Button>
            ) : null}
          </Space>
        }
      >
        <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
          <Text strong>{errorBusSnapshot.activeError?.message ?? "微流工作台发生未知错误。"}</Text>
          <Text type="tertiary">分类: {errorBusSnapshot.activeError?.category ?? "-"}</Text>
          <Text type="tertiary">HTTP: {errorBusSnapshot.activeError?.httpStatus ?? "-"}</Text>
          <Text type="tertiary">TraceId: {errorBusSnapshot.activeError?.traceId ?? "-"}</Text>
          {errorBusSnapshot.readonlyReason ? <Text type="warning">当前工作台已进入只读模式，直到切换微流或刷新页面。</Text> : null}
          {microflowWorkbenchLayout ? (
            <Card style={{ width: "100%" }}>
              <Space vertical align="start" spacing={6}>
                <Text strong>当前工作台状态</Text>
                <Text size="small">Shell 模式: {microflowWorkbenchLayout.shellMode}</Text>
                <Text size="small">底部标签: {microflowWorkbenchLayout.activeBottomTab}</Text>
                <Text size="small">底部停靠: {microflowWorkbenchLayout.bottomDockMode}</Text>
              </Space>
            </Card>
          ) : null}
        </Space>
      </SideSheet>
    </div>
  );
}

export { MendixStudioIndexPage } from "./mendix-studio-index-page";
export type { MendixStudioIndexPageProps } from "./mendix-studio-index-page";

export { useMendixStudioStore };
export * from "./microflow";
export { startMicroflowContractMockWorker, startMicroflowMockWorker, stopMicroflowMockWorker } from "./microflow/contracts/mock-api/browser";
export { createMicroflowContractMockHandlers, microflowContractMockOpenApiPaths } from "./microflow/contracts/mock-api";
/** 微流编辑器由 @atlas/microflow 实现，经本包再导出以作为统一对外 API。 */
export { MicroflowEditor, type MicroflowEditorProps } from "@atlas/microflow";
