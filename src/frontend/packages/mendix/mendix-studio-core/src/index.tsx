import "./studio.css";

import { useEffect, useMemo, useRef, useState } from "react";
import { Button, Card, Space, Toast, Typography } from "@douyinfe/semi-ui";
import type { MicroflowEditorHandle } from "@atlas/microflow";

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
import { getMendixStudioCopy } from "./i18n/copy";

export interface MendixStudioAppProps {
  appId?: string;
  workspaceId?: string;
  tenantId?: string;
  currentUser?: MicroflowAdapterFactoryConfig["currentUser"];
  adapterConfig?: MicroflowAdapterFactoryConfig;
  adapterBundle?: MicroflowAdapterBundle;
}

const { Text } = Typography;

export function MendixStudioApp({
  appId,
  workspaceId,
  tenantId,
  currentUser,
  adapterConfig,
  adapterBundle,
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

    openMicroflowWorkbenchTab(microflowId);
    setOpenedDeepLinkMicroflowId(microflowId);
  }, [microflowResourcesById, openMicroflowWorkbenchTab, openedDeepLinkMicroflowId]);

  const isMicroflow = activeWorkbenchTab?.kind === "microflow";
  const activeMicroflowId = isMicroflow
    ? activeWorkbenchTab.microflowId ?? activeWorkbenchTab.resourceId
    : undefined;
  const activeMicroflowResource = activeMicroflowId
    ? microflowResourcesById[activeMicroflowId]
    : undefined;
  const activeMicroflowTabId = isMicroflow ? activeWorkbenchTab.id : undefined;
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
        <ExplorerSplitLayout explorer={<AppExplorer adapterBundle={_resolvedBundle} appId={appId} workspaceId={workspaceId} refreshToken={microflowResourceRefreshToken} onViewMicroflowReferences={openReferencesPanel} />}>
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

              {/* 工具栏：微流模式渲染外置工具栏，否则渲染页面/通用工具栏 */}
              {isMicroflow ? (
                <MicroflowWorkbenchToolbar
                  microflowId={activeMicroflowId}
                  editorRef={microflowEditorHandleRef}
                  onViewReferences={openReferencesPanel}
                />
              ) : (
                <WorkbenchToolbar onViewMicroflowReferences={openReferencesPanel} />
              )}

              {/* 内容区 */}
              {isMicroflow ? (
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
                      onRefreshResourceList={() => setMicroflowResourceRefreshToken(token => token + 1)}
                      onCloseTab={() => closeWorkbenchTab(activeMicroflowTabId, { force: true })}
                      onOpenMicroflow={openMicroflowWorkbenchTab}
                      onDirtyChange={dirty => markMicroflowDirty(activeMicroflowId, dirty)}
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
                  </div>
                  <MicroflowStudioBottomPanel
                    microflowId={activeMicroflowId}
                    resource={activeMicroflowResource}
                    adapterBundle={_resolvedBundle}
                  />
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
            {!isMicroflow && <PropertiesPanel />}

            {/* 最右侧 Inspector Rail（微流模式下隐藏） */}
            {!isMicroflow && <RightInspectorRail />}
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
          onOpenMicroflow={openMicroflowWorkbenchTab}
          onRefreshResourceList={() => setMicroflowResourceRefreshToken(token => token + 1)}
          onClose={() => setReferencesMicroflowId(undefined)}
        />
      ) : null}
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
