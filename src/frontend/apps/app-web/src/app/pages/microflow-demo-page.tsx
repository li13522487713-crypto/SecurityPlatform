import { MendixMicroflowResourceTab, createMicroflowEditorPath } from "@atlas/mendix-studio-core";
import { useNavigate } from "react-router-dom";
import { Typography } from "@douyinfe/semi-ui";

import { createAppMicroflowAdapterConfig } from "../microflow-adapter-config";

const { Text } = Typography;

/** 独立 /microflow 内部验证路由；正式产品入口为工作区资源库「微流」Tab。 */
export function MicroflowDemoPage() {
  const navigate = useNavigate();

  return (
    <div style={{ height: "calc(100vh - 60px)", minHeight: 720, padding: 16, display: "flex", flexDirection: "column", gap: 8 }}>
      <Text type="tertiary" size="small">内部验证页（非主入口）</Text>
      <div style={{ flex: 1, minHeight: 0 }}>
        <MendixMicroflowResourceTab
          adapterConfig={createAppMicroflowAdapterConfig({})}
          onOpenMicroflow={resourceId => navigate(createMicroflowEditorPath(resourceId))}
        />
      </div>
    </div>
  );
}
