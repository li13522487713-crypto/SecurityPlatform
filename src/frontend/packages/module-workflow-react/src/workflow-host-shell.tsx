import { useEffect } from "react";
import { Button, Empty, Space, Typography } from "@douyinfe/semi-ui";
import type { WorkflowPageProps, WorkflowWorkbenchNavigation, WorkflowResourceMode } from "./types";

const DEFAULT_COZE_WORKFLOW_HOST_BASE = "http://127.0.0.1:5182";

interface WorkflowHostShellProps extends WorkflowPageProps, WorkflowWorkbenchNavigation {
  workflowId: string;
  onBack: () => void;
  backPath?: string;
  mode?: WorkflowResourceMode;
}

export function WorkflowHostShell({
  workflowId,
  mode = "workflow",
  onBack
}: WorkflowHostShellProps) {
  const hostUrl = buildCozeWorkflowHostUrl(workflowId, mode);

  useEffect(() => {
    if (typeof window === "undefined") {
      return;
    }

    window.location.assign(hostUrl);
  }, [hostUrl]);

  return (
    <section className="module-workflow__page module-workflow__editor-page">
      <div style={{ height: "100%", display: "flex", alignItems: "center", justifyContent: "center", padding: 24 }}>
        <Empty
          title={<Typography.Title heading={5} style={{ marginBottom: 8 }}>正在切换到 Coze Workflow Host</Typography.Title>}
          description="当前编辑器已切换为独立 Host 架构。若浏览器未自动跳转，可手动打开独立 Host。"
        >
          <Space>
            <Button theme="solid" type="primary" onClick={() => window.location.assign(hostUrl)}>
              打开 Coze Host
            </Button>
            <Button onClick={onBack}>
              返回 Atlas
            </Button>
          </Space>
        </Empty>
      </div>
    </section>
  );
}

function buildCozeWorkflowHostUrl(workflowId: string, mode: WorkflowResourceMode): string {
  const hostBase = (import.meta.env.VITE_COZE_WORKFLOW_HOST_BASE ?? DEFAULT_COZE_WORKFLOW_HOST_BASE).trim() || DEFAULT_COZE_WORKFLOW_HOST_BASE;
  const normalizedBase = hostBase.endsWith("/") ? hostBase.slice(0, -1) : hostBase;
  const returnUrl = typeof window !== "undefined" ? window.location.href : "";
  const params = new URLSearchParams({
    workflow_id: workflowId,
    mode,
    space_id: `atlas-${mode}`,
    return_url: returnUrl
  });
  return `${normalizedBase}/?${params.toString()}`;
}
