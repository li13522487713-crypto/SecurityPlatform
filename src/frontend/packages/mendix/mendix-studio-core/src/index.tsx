import "./studio.css";

import { Button, Card, Space, Toast, Typography } from "@douyinfe/semi-ui";
import { IconArrowRight } from "@douyinfe/semi-icons";

import { StudioHeader } from "./components/studio-header";
import { AppExplorer } from "./components/app-explorer";
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

const { Text } = Typography;

export function MendixStudioApp({ appId }: { appId?: string }) {
  const activeTab = useMendixStudioStore(state => state.activeTab);

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
          flex: 1,
          minHeight: 0,
          overflow: "hidden"
        }}
      >
        {/* App Explorer */}
        <AppExplorer />

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

          {/* 内容区（Toolbox + Canvas + Structure） */}
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

            {/* 组件结构树（仅 page 时有意义，其他 tab 也保留） */}
            <WidgetStructurePanel />
          </div>

          {/* 底部 Errors + Debug Trace */}
          <BottomPanel />
        </div>

        {/* 右侧属性面板 */}
        <PropertiesPanel />

        {/* 最右侧 Inspector Rail */}
        <RightInspectorRail />
      </div>

      {/* 运行预览侧拉板 */}
      <RuntimePreview />
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
