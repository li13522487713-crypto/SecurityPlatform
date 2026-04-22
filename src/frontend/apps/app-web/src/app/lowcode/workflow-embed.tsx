import { Suspense } from "react";
import { lazyNamed } from "../lazy-named";
import { WorkflowRuntimeBoundary } from "../workflow-runtime-boundary";
import type { LowcodeWorkflowEditorProps } from "@atlas/lowcode-studio-react/services";

const loadCozeWorkflowPlaygroundModule = () => import("@coze-workflow/playground-adapter");
const CozeWorkflowPage = lazyNamed(loadCozeWorkflowPlaygroundModule, "WorkflowPage");

/**
 * 将 Coze DAG 编辑器包装成低代码 Studio 业务逻辑模式画布。
 *
 * 继承 app-web 已有的 WorkflowRuntimeBoundary（注入 Atlas 鉴权 / Bootstrap / cozelib i18n），
 * 复用 `/workflow/:id/editor` 路由相同的 CozeWorkflowPage，避免二次实现 DAG 渲染。
 */
export function LowcodeWorkflowEmbed({ workflowId, workspaceId }: LowcodeWorkflowEditorProps) {
  return (
    <Suspense fallback={<div style={{ padding: 24, color: "#64748b" }}>加载工作流编辑器…</div>}>
      <WorkflowRuntimeBoundary>
        <CozeWorkflowPage
          workflowId={workflowId}
          spaceId={workspaceId ?? ""}
          mode="workflow"
        />
      </WorkflowRuntimeBoundary>
    </Suspense>
  );
}
