import { useCallback, useEffect, useState, type ReactNode } from "react";
import { Banner, Button, Empty, Select, Space, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import { IconRefresh, IconSave } from "@douyinfe/semi-icons";
import { formatDate } from "../assistant/agent-ide-helpers";
import type { AppBuilderConfig, StudioApplicationSummary, StudioPageProps, WorkbenchTrace, WorkflowListItem } from "../types";
import { AppPreviewPanel } from "./app-preview-panel";
import { defaultAppBuilderConfig, validateAppBuilderConfig } from "./app-builder-helpers";
import { InputComponentPanel } from "./input-component-panel";
import { OutputComponentPanel } from "./output-component-panel";
import { WorkflowBindCard } from "./workflow-bind-card";

const LAYOUT_OPTIONS: Array<{ label: string; value: AppBuilderConfig["layoutMode"] }> = [
  { label: "表单", value: "form" },
  { label: "对话", value: "chat" },
  { label: "混合", value: "hybrid" }
];

function SurfaceAppBuilder({
  title,
  subtitle,
  testId,
  toolbar,
  children
}: {
  title: string;
  subtitle: string;
  testId: string;
  toolbar?: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className="module-studio__page" data-testid={testId}>
      <div className="module-studio__header">
        <div>
          <Typography.Title heading={4} style={{ margin: 0 }}>
            {title}
          </Typography.Title>
          <Typography.Text type="tertiary">{subtitle}</Typography.Text>
        </div>
        {toolbar ? <div className="module-studio__toolbar">{toolbar}</div> : null}
      </div>
      <div className="module-studio__surface">{children}</div>
    </section>
  );
}

export function AppBuilderPage({
  api,
  appId,
  onOpenWorkflow,
  onOpenPublish
}: StudioPageProps & {
  appId: string;
  onOpenWorkflow?: (workflowId: string) => void;
  onOpenPublish?: () => void;
}) {
  const [detail, setDetail] = useState<StudioApplicationSummary | null>(null);
  const [config, setConfig] = useState<AppBuilderConfig>(() => defaultAppBuilderConfig());
  const [workflows, setWorkflows] = useState<WorkflowListItem[]>([]);
  const [workflowsLoading, setWorkflowsLoading] = useState(false);
  const [pageLoading, setPageLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [previewValues, setPreviewValues] = useState<Record<string, unknown>>({});
  const [runResult, setRunResult] = useState<{ outputs: Record<string, unknown>; trace?: WorkbenchTrace } | null>(null);
  const [running, setRunning] = useState(false);

  const load = useCallback(async () => {
    setPageLoading(true);
    setWorkflowsLoading(true);
    try {
      const [nextDetail, nextConfig, publishedWorkflows] = await Promise.all([
        api.getApplication(appId),
        api.getAppBuilderConfig(appId),
        api.listWorkflows({ status: "published" })
      ]);
      setDetail(nextDetail);
      setConfig({
        ...defaultAppBuilderConfig(),
        ...nextConfig,
        inputs: nextConfig.inputs ?? [],
        outputs: nextConfig.outputs ?? [],
        layoutMode: nextConfig.layoutMode ?? "form",
        boundWorkflowId: nextConfig.boundWorkflowId ?? nextDetail.workflowId ?? undefined
      });
      setPreviewValues({});
      setRunResult(null);
      setWorkflows(publishedWorkflows);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "加载应用构建器失败。");
      setDetail(null);
    } finally {
      setPageLoading(false);
      setWorkflowsLoading(false);
    }
  }, [api, appId]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    setPreviewValues(prev => {
      const next: Record<string, unknown> = {};
      const validKeys = new Set(config.inputs.map(i => i.variableKey.trim()).filter(Boolean));
      for (const [k, v] of Object.entries(prev)) {
        if (validKeys.has(k)) {
          next[k] = v;
        }
      }
      for (const row of config.inputs) {
        const k = row.variableKey.trim();
        if (!k) {
          continue;
        }
        if (!(k in next) && row.defaultValue !== undefined && row.defaultValue !== "") {
          if (row.type === "number") {
            const n = Number(row.defaultValue);
            next[k] = Number.isFinite(n) ? n : undefined;
          } else {
            next[k] = row.defaultValue;
          }
        }
      }
      return next;
    });
  }, [config.inputs]);

  async function handleSave() {
    const err = validateAppBuilderConfig(config);
    if (err) {
      Toast.warning(err);
      return;
    }
    setSaving(true);
    try {
      await api.updateAppBuilderConfig(appId, config);
      Toast.success("应用构建配置已保存。");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "保存失败。");
    } finally {
      setSaving(false);
    }
  }

  async function handleRunPreview() {
    const err = validateAppBuilderConfig(config);
    if (err) {
      Toast.warning(err);
      return;
    }
    const missing = config.inputs.filter(row => {
      const k = row.variableKey.trim();
      if (!row.required || !k) {
        return false;
      }
      const v = previewValues[k];
      if (v === undefined || v === null) {
        return true;
      }
      if (typeof v === "string" && v.trim() === "") {
        return true;
      }
      return false;
    });
    if (missing.length > 0) {
      Toast.warning("请填写所有必填输入项后再运行预览。");
      return;
    }

    const payload: Record<string, unknown> = {};
    for (const row of config.inputs) {
      const k = row.variableKey.trim();
      if (!k) {
        continue;
      }
      if (k in previewValues) {
        payload[k] = previewValues[k];
      } else if (row.defaultValue !== undefined && row.defaultValue !== "") {
        if (row.type === "number") {
          const n = Number(row.defaultValue);
          payload[k] = Number.isFinite(n) ? n : undefined;
        } else {
          payload[k] = row.defaultValue;
        }
      }
    }

    setRunning(true);
    setRunResult(null);
    try {
      const result = await api.runAppPreview(appId, payload);
      setRunResult(result);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : "运行预览失败。");
    } finally {
      setRunning(false);
    }
  }

  const disabled = pageLoading || saving;
  const workflowToOpen = config.boundWorkflowId ?? detail?.workflowId;

  return (
    <SurfaceAppBuilder
      title="应用构建器"
      subtitle={detail ? `${detail.name} · 左侧配置入参与工作流，中间配置输出与布局，右侧预览运行结果。` : "加载应用信息…"}
      testId="app-studio-app-detail-page"
      toolbar={
        <Space wrap>
          <Button icon={<IconRefresh />} disabled={pageLoading || saving} onClick={() => void load()}>
            重新加载
          </Button>
          <Button
            theme="solid"
            type="primary"
            icon={<IconSave />}
            loading={saving}
            disabled={pageLoading}
            onClick={() => void handleSave()}
          >
            保存配置
          </Button>
          {workflowToOpen && onOpenWorkflow ? (
            <Button
              onClick={() => {
                if (workflowToOpen) {
                  onOpenWorkflow(workflowToOpen);
                }
              }}
            >
              打开工作流
            </Button>
          ) : null}
          {onOpenPublish ? (
            <Button disabled={pageLoading} onClick={() => onOpenPublish()}>
              进入发布页
            </Button>
          ) : null}
        </Space>
      }
    >
      {pageLoading ? (
        <Banner type="info" bordered={false} fullMode={false} title="正在加载" description="正在同步应用信息与构建配置。" />
      ) : null}
      {!pageLoading && !detail ? (
        <Empty title="未找到应用" description="请从应用列表重新进入。" image={null} />
      ) : null}
      {detail ? (
        <>
          <div className="module-studio__app-builder-shell">
            <div className="module-studio__app-builder-meta">
              <Typography.Text type="tertiary" size="small">
                ID {detail.id} · 更新 {formatDate(detail.updatedAt || detail.lastEditedAt)} · 发布 v{detail.publishVersion ?? 0}
              </Typography.Text>
              <Tag color={detail.status?.toLowerCase() === "published" ? "green" : "blue"}>{detail.status || "Draft"}</Tag>
            </div>

            <div className="module-studio__app-builder-layout">
              <div className="module-studio__app-builder-col">
                <InputComponentPanel value={config.inputs} disabled={disabled} onChange={inputs => setConfig(c => ({ ...c, inputs }))} />
              </div>
              <div className="module-studio__app-builder-col">
                <WorkflowBindCard
                  boundWorkflowId={config.boundWorkflowId}
                  workflows={workflows}
                  loading={workflowsLoading}
                  disabled={disabled}
                  onChange={workflowId => setConfig(c => ({ ...c, boundWorkflowId: workflowId }))}
                  onOpenWorkflow={onOpenWorkflow}
                />
                <OutputComponentPanel value={config.outputs} disabled={disabled} onChange={outputs => setConfig(c => ({ ...c, outputs }))} />
                <div className="module-studio__coze-inspector-card module-studio__app-builder-panel">
                  <div className="module-studio__card-head">
                    <span>布局模式</span>
                  </div>
                  <Typography.Text type="tertiary" size="small" style={{ display: "block", marginTop: 0 }}>
                    决定应用端展示形态（预览区标签仅作提示）。
                  </Typography.Text>
                  <Select
                    style={{ width: "100%" }}
                    value={config.layoutMode}
                    disabled={disabled}
                    optionList={LAYOUT_OPTIONS}
                    onChange={v => setConfig(c => ({ ...c, layoutMode: v as AppBuilderConfig["layoutMode"] }))}
                  />
                </div>
              </div>
              <div className="module-studio__app-builder-col module-studio__app-builder-col--preview">
                <AppPreviewPanel
                  layoutMode={config.layoutMode}
                  inputs={config.inputs}
                  outputs={config.outputs}
                  previewValues={previewValues}
                  onPreviewValuesChange={setPreviewValues}
                  runResult={runResult}
                  running={running}
                  onRun={() => void handleRunPreview()}
                  disabled={disabled}
                />
              </div>
            </div>
          </div>
        </>
      ) : null}
    </SurfaceAppBuilder>
  );
}
