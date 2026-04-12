import { useEffect, useMemo, useState } from "react";
import { Banner, Button, Input, Space, Spin, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import type {
  CanvasSchema,
  NodeTypeMetadata,
  WorkflowApiClient,
  WorkflowDetailResponse,
  WorkflowVersionItem
} from "@atlas/workflow-core-react";
import type { WorkflowPageProps, WorkflowResourceMode } from "./types";

interface WorkflowEditorShellProps extends WorkflowPageProps {
  workflowId: string;
  onBack: () => void;
  mode?: WorkflowResourceMode;
}

interface WorkflowProcessSnapshot {
  status?: number;
  startedAt?: string;
  completedAt?: string;
  errorMessage?: string;
  nodeExecutions: Array<{
    nodeKey: string;
    status: number;
    errorMessage?: string;
  }>;
}

function normalizeCanvasSchema(canvas: unknown): CanvasSchema {
  if (!canvas || typeof canvas !== "object") {
    return {
      nodes: [],
      connections: []
    };
  }

  const record = canvas as Partial<CanvasSchema>;
  return {
    nodes: Array.isArray(record.nodes) ? record.nodes : [],
    connections: Array.isArray(record.connections) ? record.connections : [],
    schemaVersion: record.schemaVersion,
    viewport: record.viewport,
    globals: record.globals
  };
}

function safeParseCanvas(canvasJson: string): CanvasSchema {
  try {
    return normalizeCanvasSchema(JSON.parse(canvasJson));
  } catch {
    return {
      nodes: [],
      connections: []
    };
  }
}

function formatDateTime(value?: string) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function resolveExecutionStatus(status?: number) {
  switch (status) {
    case 1:
      return "运行中";
    case 2:
      return "已完成";
    case 3:
      return "失败";
    case 4:
      return "已取消";
    case 5:
      return "已中断";
    case 6:
      return "已跳过";
    case 7:
      return "已阻塞";
    default:
      return "待执行";
  }
}

function findNodeDisplayName(nodeTypes: NodeTypeMetadata[], nodeType: string) {
  return nodeTypes.find(item => item.key === nodeType)?.name ?? nodeType;
}

async function loadWorkflowDetail(apiClient: WorkflowApiClient, workflowId: string) {
  const [detailResponse, versionsResponse, nodeTypesResponse] = await Promise.all([
    apiClient.getDetail?.(workflowId),
    apiClient.getVersions?.(workflowId),
    apiClient.getNodeTypes?.()
  ]);

  return {
    detail: detailResponse?.data,
    versions: versionsResponse?.data ?? [],
    nodeTypes: nodeTypesResponse?.data ?? []
  };
}

export function WorkflowEditorShell({
  api,
  workflowId,
  onBack,
  mode = "workflow"
}: WorkflowEditorShellProps) {
  const apiClient = api.apiClient;
  const [detail, setDetail] = useState<WorkflowDetailResponse | null>(null);
  const [versions, setVersions] = useState<WorkflowVersionItem[]>([]);
  const [nodeTypes, setNodeTypes] = useState<NodeTypeMetadata[]>([]);
  const [canvasJson, setCanvasJson] = useState("{}");
  const [draftName, setDraftName] = useState("");
  const [draftDescription, setDraftDescription] = useState("");
  const [runInputs, setRunInputs] = useState("{\n  \n}");
  const [runProcess, setRunProcess] = useState<WorkflowProcessSnapshot | null>(null);
  const [validationErrors, setValidationErrors] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [running, setRunning] = useState(false);

  useEffect(() => {
    let disposed = false;
    setLoading(true);

    void loadWorkflowDetail(apiClient, workflowId)
      .then(({ detail: nextDetail, versions: nextVersions, nodeTypes: nextNodeTypes }) => {
        if (disposed) {
          return;
        }

        setDetail(nextDetail ?? null);
        setVersions(nextVersions);
        setNodeTypes(nextNodeTypes);
        setCanvasJson(nextDetail?.canvasJson ?? "{\n  \"nodes\": [],\n  \"connections\": []\n}");
        setDraftName(nextDetail?.name ?? "");
        setDraftDescription(nextDetail?.description ?? "");
      })
      .catch(error => {
        if (!disposed) {
          Toast.error(error instanceof Error ? error.message : "加载工作流失败");
        }
      })
      .finally(() => {
        if (!disposed) {
          setLoading(false);
        }
      });

    return () => {
      disposed = true;
    };
  }, [apiClient, workflowId]);

  const parsedCanvas = useMemo(() => safeParseCanvas(canvasJson), [canvasJson]);
  const nodeCount = parsedCanvas.nodes.length;
  const connectionCount = parsedCanvas.connections.length;
  const title = mode === "chatflow" ? "Chatflow Editor" : "Workflow Editor";

  async function refreshExecution(executionId: string) {
    const processResponse = await apiClient.getProcess?.(executionId);
    const process = processResponse?.data;
    setRunProcess(process ? {
      status: process.status,
      startedAt: process.startedAt,
      completedAt: process.completedAt,
      errorMessage: process.errorMessage,
      nodeExecutions: process.nodeExecutions ?? []
    } : null);
  }

  async function handleSave() {
    setSaving(true);
    try {
      if (detail && apiClient.saveDraft) {
        await apiClient.saveDraft(workflowId, {
          canvasJson,
          commitId: detail.commitId
        });
      }
      if (apiClient.updateMeta) {
        await apiClient.updateMeta(workflowId, {
          name: draftName.trim() || detail?.name || "未命名工作流",
          description: draftDescription.trim() || undefined
        });
      }
      Toast.success("草稿已保存");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "保存失败");
    } finally {
      setSaving(false);
    }
  }

  async function handleValidate() {
    if (!apiClient.validate) {
      return;
    }

    try {
      const response = await apiClient.validate(workflowId, { canvasJson });
      const errors = response.data?.errors ?? [];
      setValidationErrors(errors);
      if (response.data?.isValid === false || errors.length > 0) {
        Toast.warning("当前工作流仍有待修复项");
        return;
      }
      Toast.success("当前工作流校验通过");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "校验失败");
    }
  }

  async function handlePublish() {
    if (!apiClient.publish) {
      return;
    }

    setPublishing(true);
    try {
      await handleSave();
      await apiClient.publish(workflowId, {
        changeLog: "Published from React shell"
      });
      Toast.success("已发布最新版本");
      const versionsResponse = await apiClient.getVersions?.(workflowId);
      setVersions(versionsResponse?.data ?? []);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "发布失败");
    } finally {
      setPublishing(false);
    }
  }

  async function handleRun() {
    if (!apiClient.runSync) {
      return;
    }

    setRunning(true);
    try {
      const response = await apiClient.runSync(workflowId, {
        source: "draft",
        inputsJson: runInputs.trim() || "{}"
      });
      const executionId = response.data?.executionId;
      if (!executionId) {
        Toast.warning("未返回执行实例");
        return;
      }

      Toast.success(`已提交测试运行：${executionId}`);
      await refreshExecution(executionId);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "测试运行失败");
    } finally {
      setRunning(false);
    }
  }

  return (
    <section className="module-workflow__shell" data-testid={mode === "chatflow" ? "app-chatflow-editor-shell" : "app-workflow-editor-shell"}>
      <div className="module-workflow__editor-banner">
        <div>
          <Typography.Title heading={4} style={{ margin: 0 }}>
            {title}
          </Typography.Title>
          <Typography.Text type="tertiary">
            先完成 React-only 结构切换，随后把这里替换为 Coze 原生编辑器源码。
          </Typography.Text>
        </div>
        <Space wrap>
          <Button onClick={onBack} data-testid="workflow.detail.title.back">返回列表</Button>
          <Button onClick={() => void handleValidate()}>校验</Button>
          <Button loading={saving} onClick={() => void handleSave()} data-testid="workflow.detail.title.save-draft">
            保存草稿
          </Button>
          <Button
            theme="borderless"
            onClick={() => Toast.info("当前 React 工作流壳层暂不提供复制接口。")}
            data-testid="workflow.detail.title.duplicate"
          >
            复制
          </Button>
          <Button
            theme="solid"
            type="primary"
            loading={publishing}
            onClick={() => void handlePublish()}
            data-testid="workflow-base-publish-button"
          >
            发布
          </Button>
          <Button
            theme="borderless"
            onClick={() => Toast.info("当前 React 工作流壳层暂不提供节点面板。")}
            data-testid="workflow.detail.toolbar.add-node"
          >
            添加节点
          </Button>
          <Button
            theme="borderless"
            onClick={() => Toast.info("当前 React 工作流壳层暂不提供变量面板。")}
            data-testid="workflow.detail.toolbar.variables"
          >
            变量
          </Button>
          <Button
            theme="borderless"
            onClick={() => Toast.info("当前 React 工作流壳层暂不提供 Trace 面板。")}
            data-testid="workflow.detail.toolbar.trace"
          >
            Trace
          </Button>
          <Button
            theme="borderless"
            onClick={() => Toast.info("当前 React 工作流壳层暂不提供节点调试面板。")}
            data-testid="workflow.detail.toolbar.debug"
          >
            调试
          </Button>
          <Button
            theme="solid"
            type="secondary"
            loading={running}
            onClick={() => void handleRun()}
            data-testid="workflow.detail.toolbar.test-run"
          >
            测试运行
          </Button>
        </Space>
      </div>

      {loading ? (
        <div className="module-workflow__editor-loading">
          <Spin size="large" />
        </div>
      ) : (
        <>
          <div className="module-workflow__editor-summary">
            <article>
              <span>节点</span>
              <strong>{nodeCount}</strong>
            </article>
            <article>
              <span>连线</span>
              <strong>{connectionCount}</strong>
            </article>
            <article>
              <span>最近更新</span>
              <strong>{formatDateTime(detail?.updatedAt)}</strong>
            </article>
            <article>
              <span>当前版本</span>
              <strong>v{detail?.latestVersionNumber ?? 0}</strong>
            </article>
          </div>

          <div className="module-workflow__editor-grid">
            <section className="module-workflow__panel">
              <Typography.Title heading={6}>基础信息</Typography.Title>
              <label className="module-workflow__field">
                <span>名称</span>
                <Input value={draftName} onChange={setDraftName} data-testid="workflow.detail.meta.name" />
              </label>
              <label className="module-workflow__field">
                <span>描述</span>
                <Input value={draftDescription} onChange={setDraftDescription} data-testid="workflow.detail.meta.description" />
              </label>
              <Typography.Title heading={6}>节点概览</Typography.Title>
              <div className="module-workflow__node-list">
                {parsedCanvas.nodes.map(node => (
                  <div key={node.key} className="module-workflow__node-card">
                    <div className="module-workflow__node-card-head">
                      <strong>{node.title || node.key}</strong>
                      <Tag color="blue">{findNodeDisplayName(nodeTypes, String(node.type))}</Tag>
                    </div>
                    <span>{node.key}</span>
                  </div>
                ))}
                {parsedCanvas.nodes.length === 0 ? <Typography.Text type="tertiary">当前画布还没有节点。</Typography.Text> : null}
              </div>
            </section>

            <section className="module-workflow__panel module-workflow__panel-wide">
              <Typography.Title heading={6}>画布 JSON</Typography.Title>
              <textarea
                className="module-workflow__json-editor"
                value={canvasJson}
                onChange={event => setCanvasJson(event.target.value)}
                spellCheck={false}
                data-testid="workflow.detail.canvas-json"
              />
              {validationErrors.length > 0 ? (
                <Banner
                  type="warning"
                  bordered={false}
                  fullMode={false}
                  title="校验结果"
                  description={validationErrors.join("；")}
                />
              ) : null}
            </section>

            <section className="module-workflow__panel">
              <Typography.Title heading={6}>测试运行</Typography.Title>
              <textarea
                className="module-workflow__json-editor module-workflow__json-editor--compact"
                value={runInputs}
                onChange={event => setRunInputs(event.target.value)}
                spellCheck={false}
                data-testid="workflow.detail.run-inputs"
              />
              <div className="module-workflow__execution-card" data-testid="workflow.detail.node.testrun.result-panel">
                <strong>最近一次执行</strong>
                <span data-testid="workflow.detail.node.testrun.result-item">状态：{resolveExecutionStatus(runProcess?.status)}</span>
                <span data-testid="workflow.detail.node.testrun.result-item">开始：{formatDateTime(runProcess?.startedAt)}</span>
                <span data-testid="workflow.detail.node.testrun.result-item">结束：{formatDateTime(runProcess?.completedAt)}</span>
                <span data-testid="workflow.detail.node.testrun.result-item">节点执行数：{runProcess?.nodeExecutions.length ?? 0}</span>
                {runProcess?.errorMessage ? <Tag color="red">{runProcess.errorMessage}</Tag> : null}
              </div>
              <Typography.Title heading={6}>版本</Typography.Title>
              <div className="module-workflow__version-list">
                {versions.map(item => (
                  <div key={item.id} className="module-workflow__version-item">
                    <strong>v{item.versionNumber}</strong>
                    <span>{formatDateTime(item.publishedAt)}</span>
                  </div>
                ))}
                {versions.length === 0 ? <Typography.Text type="tertiary">还没有发布版本。</Typography.Text> : null}
              </div>
            </section>
          </div>
        </>
      )}
    </section>
  );
}
