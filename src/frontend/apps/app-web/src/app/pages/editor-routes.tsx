import { Suspense, useMemo, useState } from "react";
import { Navigate, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { Button } from "@douyinfe/semi-ui";
import {
  chatflowEditorPath,
  workspaceProjectsPath,
  workflowEditorPath
} from "@atlas/app-shell-shared";
import { lazyNamed } from "../lazy-named";
import { useAppI18n } from "../i18n";
import { useWorkspaceContext } from "../workspace-context";
import { WorkflowRuntimeBoundary } from "../workflow-runtime-boundary";
import { TestsetDrawer } from "../components/testset-drawer";
import { PageShell } from "../_shared";
import { createAppWebLowcodeStudioHost } from "../lowcode/studio-host";

const loadCozeWorkflowPlaygroundModule = () => import("@coze-workflow/playground-adapter");
const loadLowcodeStudioModule = () => import("@atlas/lowcode-studio-react");

const CozeWorkflowPage = lazyNamed(loadCozeWorkflowPlaygroundModule, "WorkflowPage");
const LowcodeStudioApp = lazyNamed(loadLowcodeStudioModule, "LowcodeStudioApp");

function EditorLoading() {
  return <PageShell loading testId="coze-editor-loading" />;
}

/**
 * 智能体编辑器路由 - `/agent/:agentId/editor`
 *
 * 旧编辑器入口统一重定向到已经接入 Coze 原生壳的 `/space/:space_id/bot/:bot_id`。
 */
export function AgentEditorRoute() {
  const { agentId = "" } = useParams<{ agentId: string }>();
  const workspace = useWorkspaceContext();
  return <Navigate to={`/space/${encodeURIComponent(workspace.id)}/bot/${encodeURIComponent(agentId)}`} replace />;
}

export function AgentPublishRoute() {
  const { agentId = "" } = useParams<{ agentId: string }>();
  const workspace = useWorkspaceContext();
  return <Navigate to={`/space/${encodeURIComponent(workspace.id)}/bot/${encodeURIComponent(agentId)}/publish`} replace />;
}

export function AppEditorRoute() {
  const { projectId = "" } = useParams<{ projectId: string }>();
  return <LowcodeStudioRoute projectId={projectId} />;
}

export function AppPublishRoute() {
  const { projectId = "" } = useParams<{ projectId: string }>();
  return <LowcodeStudioRoute projectId={projectId} routeMode="publish" />;
}

export function CanonicalLowcodeStudioRoute() {
  const { id = "" } = useParams<{ id: string }>();
  return <LowcodeStudioRoute projectId={id} />;
}

function LowcodeStudioRoute({ projectId, routeMode }: { projectId: string; routeMode?: "editor" | "publish" }) {
  const { locale } = useAppI18n();
  const navigate = useNavigate();
  const workspace = useWorkspaceContext();
  const lowcodeHost = useMemo(() => createAppWebLowcodeStudioHost(workspace.appKey), [workspace.appKey]);

  return (
    <Suspense fallback={<EditorLoading />}>
      <LowcodeStudioApp
        appId={projectId}
        host={lowcodeHost}
        locale={locale}
        workspaceId={workspace.id}
        workspaceLabel={workspace.name || workspace.appKey}
        routeMode={routeMode}
        onBack={() => navigate(workspaceProjectsPath(workspace.id))}
      />
    </Suspense>
  );
}

/**
 * 工作流编辑器路由 - `/workflow/:workflowId/editor`
 *
 * 复用 @coze-workflow/playground-adapter 的 WorkflowPage。
 * 必须包 WorkflowRuntimeBoundary（注入 cozelib i18n 与 atlas-foundation-bridge）。
 *
 * 顶部右上角浮一个"测试集"按钮，点击打开 TestsetDrawer。
 */
export function WorkflowEditorRoute() {
  const { workflowId = "" } = useParams<{ workflowId: string }>();
  return <WorkflowEditorBase workflowId={workflowId} mode="workflow" />;
}

export function ChatflowEditorRoute() {
  const { chatflowId = "" } = useParams<{ chatflowId: string }>();
  return <WorkflowEditorBase workflowId={chatflowId} mode="chatflow" />;
}

function WorkflowEditorBase({ workflowId, mode }: { workflowId: string; mode: "workflow" | "chatflow" }) {
  const { t } = useAppI18n();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const workspace = useWorkspaceContext();
  const [testsetOpen, setTestsetOpen] = useState(false);
  const returnUrl = searchParams.get("return_url") ?? searchParams.get("returnUrl") ?? undefined;

  return (
    <Suspense fallback={<EditorLoading />}>
      <div className="coze-workflow-editor-frame">
        <div className="coze-workflow-editor-toolbar">
          <Button theme="light" onClick={() => setTestsetOpen(true)} data-testid="coze-workflow-editor-testset-btn">
            {t("cozeTestsetDrawerTitle")}
          </Button>
        </div>
        <WorkflowRuntimeBoundary spaceId={workspace.id}>
          <CozeWorkflowPage
            workflowId={workflowId}
            spaceId={workspace.id}
            returnUrl={returnUrl}
            mode={mode}
            onAtlasBack={() => {
              if (returnUrl && typeof window !== "undefined") {
                window.location.assign(returnUrl);
                return;
              }
              navigate(mode === "chatflow" ? chatflowEditorPath(workflowId) : workflowEditorPath(workflowId));
            }}
          />
        </WorkflowRuntimeBoundary>
      </div>
      <TestsetDrawer
        visible={testsetOpen}
        workspaceId={workspace.id}
        workflowId={workflowId}
        onClose={() => setTestsetOpen(false)}
      />
    </Suspense>
  );
}
