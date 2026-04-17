import { Suspense, useMemo, useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import { Button, Spin } from "@douyinfe/semi-ui";
import {
  agentPublishPath,
  appPublishPath,
  chatflowEditorPath,
  workflowEditorPath
} from "@atlas/app-shell-shared";
import { lazyNamed } from "../lazy-named";
import { useAppI18n } from "../i18n";
import { useWorkspaceContext } from "../workspace-context";
import { WorkflowRuntimeBoundary } from "../workflow-runtime-boundary";
import { useAppApis } from "../app";
import { TestsetDrawer } from "../components/testset-drawer";

const loadStudioModule = () => import("@atlas/module-studio-react");
const loadCozeWorkflowPlaygroundModule = () => import("@coze-workflow/playground-adapter");

const BotIdePage = lazyNamed(loadStudioModule, "BotIdePage");
const AssistantPublishPage = lazyNamed(loadStudioModule, "AssistantPublishPage");
const AppDetailPage = lazyNamed(loadStudioModule, "AppDetailPage");
const AppPublishPage = lazyNamed(loadStudioModule, "AppPublishPage");
const StudioContextProvider = lazyNamed(loadStudioModule, "StudioContextProvider");
const CozeWorkflowPage = lazyNamed(loadCozeWorkflowPlaygroundModule, "WorkflowPage");

function EditorLoading() {
  return (
    <div className="atlas-loading-page" data-testid="coze-editor-loading">
      <Spin size="large" />
    </div>
  );
}

/**
 * 智能体编辑器路由 - `/agent/:agentId/editor`
 *
 * 复用 module-studio-react 的 BotIdePage（即 AgentWorkbench），
 * 包 StudioContextProvider 注入 workspace summary / model configs。
 */
export function AgentEditorRoute() {
  const { agentId = "" } = useParams<{ agentId: string }>();
  const { locale } = useAppI18n();
  const navigate = useNavigate();
  const workspace = useWorkspaceContext();
  const apis = useAppApis(workspace.appKey);
  const studioApi = apis.studioApi;

  const props = useMemo(() => ({
    api: studioApi,
    locale,
    botId: agentId,
    onOpenPublish: () => navigate(agentPublishPath(agentId))
  }), [agentId, locale, navigate, studioApi]);

  return (
    <Suspense fallback={<EditorLoading />}>
      <StudioContextProvider api={studioApi}>
        <BotIdePage {...props} />
      </StudioContextProvider>
    </Suspense>
  );
}

export function AgentPublishRoute() {
  const { agentId = "" } = useParams<{ agentId: string }>();
  const { locale } = useAppI18n();
  const workspace = useWorkspaceContext();
  const apis = useAppApis(workspace.appKey);
  const studioApi = apis.studioApi;

  return (
    <Suspense fallback={<EditorLoading />}>
      <StudioContextProvider api={studioApi}>
        <AssistantPublishPage api={studioApi} locale={locale} assistantId={agentId} />
      </StudioContextProvider>
    </Suspense>
  );
}

export function AppEditorRoute() {
  const { projectId = "" } = useParams<{ projectId: string }>();
  const { locale } = useAppI18n();
  const navigate = useNavigate();
  const workspace = useWorkspaceContext();
  const apis = useAppApis(workspace.appKey);
  const studioApi = apis.studioApi;

  return (
    <Suspense fallback={<EditorLoading />}>
      <StudioContextProvider api={studioApi}>
        <AppDetailPage
          api={studioApi}
          locale={locale}
          appId={projectId}
          onOpenWorkflow={workflowId => navigate(workflowEditorPath(workflowId))}
          onOpenPublish={() => navigate(appPublishPath(projectId))}
        />
      </StudioContextProvider>
    </Suspense>
  );
}

export function AppPublishRoute() {
  const { projectId = "" } = useParams<{ projectId: string }>();
  const { locale } = useAppI18n();
  const workspace = useWorkspaceContext();
  const apis = useAppApis(workspace.appKey);
  const studioApi = apis.studioApi;

  return (
    <Suspense fallback={<EditorLoading />}>
      <StudioContextProvider api={studioApi}>
        <AppPublishPage api={studioApi} locale={locale} appId={projectId} />
      </StudioContextProvider>
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
        <WorkflowRuntimeBoundary>
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
