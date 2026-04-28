import "./studio.css";

import { useEffect, useMemo, useState } from "react";
import { Button, Card, Space, Toast, Typography } from "@douyinfe/semi-ui";
import { IconArrowRight } from "@douyinfe/semi-icons";

import type { MicroflowAdapterFactoryConfig } from "./microflow/config/microflow-adapter-config";
import type { MicroflowAdapterBundle } from "./microflow/adapter/microflow-adapter-factory";
import { createMicroflowAdapterBundle } from "./microflow/adapter/microflow-adapter-factory";
import { StudioHeader } from "./components/studio-header";
import { AppExplorer } from "./components/app-explorer";
import { ExplorerSplitLayout } from "./components/explorer-split-layout";
import { WidgetToolbox } from "./components/widget-toolbox";
import { WorkbenchTabs } from "./components/workbench-tabs";
import { WorkbenchToolbar } from "./components/workbench-toolbar";
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

              {/* 工具栏 */}
              <WorkbenchToolbar onViewMicroflowReferences={openReferencesPanel} />

              {/* 内容区 */}
              {isMicroflow ? (
                <div style={{ flex: 1, minHeight: 0, overflow: "hidden" }}>
                  {activeMicroflowId && activeMicroflowTabId ? (
                    <MicroflowResourceEditorHost
                      key={activeMicroflowTabId}
                      microflowId={activeMicroflowId}
                      workspaceId={workspaceId}
                      moduleId={activeWorkbenchTab?.moduleId ?? activeMicroflowResource?.moduleId}
                      adapterBundle={_resolvedBundle}
                      microflowResourceIndex={microflowResourcesById}
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
                        height: "100%",
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

export function MendixStudioIndexPage({
  workspaceId,
  onOpen
}: {
  workspaceId: string;
  onOpen: (appId: string) => void;
}) {
  const copy = getMendixStudioCopy();
  return (
    <div
      style={{
        minHeight: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        background: "linear-gradient(135deg, #f0f4ff 0%, #e8f3ff 100%)"
      }}
    >
      <Card
        style={{
          width: 480,
          boxShadow: "0 20px 60px rgba(0,0,0,0.12)",
          borderRadius: 12,
          border: "none"
        }}
        bodyStyle={{ padding: "40px 40px 32px" }}
      >
        {/* Logo */}
        <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 24 }}>
          <div
            style={{
              width: 36,
              height: 36,
              background: "#1677ff",
              borderRadius: 8,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              color: "#fff",
              fontWeight: 800,
              fontSize: 16
            }}
          >
            mx
          </div>
          <div>
            <div style={{ fontWeight: 700, fontSize: 18, color: "#1c2a3a" }}>Lowcode Studio</div>
            <div style={{ fontSize: 12, color: "#6b7280" }}>Mendix-compatible low-code IDE</div>
          </div>
        </div>

        <Text type="tertiary" style={{ display: "block", marginBottom: 20, fontSize: 13 }}>
          {copy.index.workspaceLabel}: <strong style={{ color: "#374151" }}>{workspaceId}</strong>
        </Text>

        <Space vertical style={{ width: "100%" }} spacing={12}>
          {/* 示例应用 */}
          <div
            style={{
              border: "1px solid #e5e7eb",
              borderRadius: 8,
              padding: "16px 20px",
              cursor: "pointer",
              transition: "all 0.2s",
              background: "#fff"
            }}
            onClick={() => onOpen("app_procurement")}
            onMouseEnter={e => {
              (e.currentTarget as HTMLDivElement).style.borderColor = "#1677ff";
              (e.currentTarget as HTMLDivElement).style.boxShadow = "0 0 0 3px rgba(22,119,255,0.12)";
            }}
            onMouseLeave={e => {
              (e.currentTarget as HTMLDivElement).style.borderColor = "#e5e7eb";
              (e.currentTarget as HTMLDivElement).style.boxShadow = "none";
            }}
          >
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
              <div>
                <div style={{ fontWeight: 600, fontSize: 14, color: "#1c2a3a", marginBottom: 4 }}>
                  {copy.index.sampleAppTitle}
                </div>
                <div style={{ fontSize: 12, color: "#6b7280" }}>
                  {copy.index.sampleAppDescription}
                </div>
              </div>
              <IconArrowRight style={{ color: "#1677ff", fontSize: 18, flexShrink: 0 }} />
            </div>
          </div>

          {/* 新建按钮 */}
          <Button
            theme="light"
            type="primary"
            block
            style={{ height: 40, fontSize: 14 }}
            onClick={() => {
              Toast.info({ content: copy.index.createAppInProgress, duration: 2 });
            }}
          >
            {copy.index.createAppButton}
          </Button>
        </Space>

        <div style={{ marginTop: 24, paddingTop: 20, borderTop: "1px solid #f0f2f5" }}>
          <Text type="tertiary" size="small">
            {copy.index.footer}
          </Text>
        </div>
      </Card>
    </div>
  );
}

export { useMendixStudioStore };
export { SAMPLE_PROCUREMENT_APP } from "./sample-app";
export * from "./microflow";
export { startMicroflowContractMockWorker, startMicroflowMockWorker, stopMicroflowMockWorker } from "./microflow/contracts/mock-api/browser";
export { createMicroflowContractMockHandlers, microflowContractMockOpenApiPaths } from "./microflow/contracts/mock-api";
/** 微流编辑器由 @atlas/microflow 实现，经本包再导出以作为统一对外 API。 */
export { MicroflowEditor, type MicroflowEditorProps } from "@atlas/microflow";
