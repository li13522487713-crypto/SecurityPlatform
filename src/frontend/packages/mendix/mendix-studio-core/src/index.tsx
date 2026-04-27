import "./studio.css";

import { useEffect, useMemo } from "react";
import { Button, Card, Space, Toast, Typography } from "@douyinfe/semi-ui";
import { IconArrowRight, IconFullScreenStroked } from "@douyinfe/semi-icons";
import { MicroflowEditor } from "@atlas/microflow";

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
  const activeTab = useMendixStudioStore(state => state.activeTab);
  const microflowSchema = useMendixStudioStore(state => state.microflowSchema);
  const microflowImmersive = useMendixStudioStore(state => state.microflowImmersive);
  const setMicroflowSchema = useMendixStudioStore(state => state.setMicroflowSchema);
  const setMicroflowImmersive = useMendixStudioStore(state => state.setMicroflowImmersive);
  const setStudioContext = useMendixStudioStore(state => state.setStudioContext);

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

  const isMicroflow = activeTab === "microflowDesigner";

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
        <ExplorerSplitLayout explorer={<AppExplorer />}>
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
              <WorkbenchToolbar />

              {/* 内容区 */}
              {isMicroflow ? (
                /* 微流编辑器：占满中央列，自带节点面板/属性面板/底部面板 */
                <div style={{ flex: 1, minHeight: 0, overflow: "hidden" }}>
                  <MicroflowEditor
                    schema={microflowSchema}
                    onSchemaChange={setMicroflowSchema}
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

      {/* 沉浸模式覆盖层 */}
      {microflowImmersive && isMicroflow && (
        <div
          style={{
            position: "fixed",
            inset: 0,
            zIndex: 1000,
            background: "#fff"
          }}
        >
          <MicroflowEditor
            schema={microflowSchema}
            onSchemaChange={setMicroflowSchema}
            immersive={true}
            toolbarSuffix={
              <button
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 4,
                  padding: "4px 10px",
                  fontSize: 12,
                  border: "1px solid #d9d9d9",
                  borderRadius: 4,
                  background: "#fff",
                  cursor: "pointer",
                  color: "#374151"
                }}
                onClick={() => setMicroflowImmersive(false)}
              >
                <IconFullScreenStroked style={{ fontSize: 13 }} />
                退出沉浸
              </button>
            }
          />
        </div>
      )}
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
          工作区: <strong style={{ color: "#374151" }}>{workspaceId}</strong>
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
                  Procurement Approval（示例）
                </div>
                <div style={{ fontSize: 12, color: "#6b7280" }}>
                  采购审批工作流 · Domain Model · Microflow · Workflow
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
              Toast.info({ content: "新建应用功能开发中", duration: 2 });
            }}
          >
            + 新建应用
          </Button>
        </Space>

        <div style={{ marginTop: 24, paddingTop: 20, borderTop: "1px solid #f0f2f5" }}>
          <Text type="tertiary" size="small">
            Mendix Studio Core · Atlas Security Platform · v0.0.0
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
