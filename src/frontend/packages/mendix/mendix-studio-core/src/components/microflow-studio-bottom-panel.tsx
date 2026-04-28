import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Button, Empty, List, Space, Spin, Table, Tag, Tabs, Toast, Typography } from "@douyinfe/semi-ui";
import type { MicroflowReference } from "@atlas/microflow";

import type { MicroflowAdapterBundle } from "../microflow/adapter/microflow-adapter-factory";
import type { StudioMicroflowDefinitionView } from "../microflow/studio/studio-microflow-types";
import { getMicroflowErrorUserMessage } from "../microflow/adapter/http/microflow-api-error";
import { useMendixStudioStore } from "../store";

const { Text, Title } = Typography;

export interface MicroflowStudioBottomPanelProps {
  microflowId?: string;
  resource?: StudioMicroflowDefinitionView;
  adapterBundle?: MicroflowAdapterBundle;
}

type BottomPanelTabKey = "validation" | "configuration" | "references" | "info";

const formatDate = (input?: string | Date | null): string => {
  if (!input) {
    return "-";
  }
  try {
    const date = typeof input === "string" ? new Date(input) : input;
    return date.toLocaleString();
  } catch {
    return String(input);
  }
};

/**
 * Mendix Studio Workbench 底部面板，对齐用户清单 §2.1：
 *   验证结果 / 配置检查 / 引用检查 / 微流信息 四个 Tab。
 *
 * 数据来源：
 * - 验证结果：store.validationByMicroflowId / validationSummaryByMicroflowId
 *   （由 MicroflowEditor.handleValidate 填充）。
 * - 配置检查：从最近一次 validation 中按 source/severity 分组聚合。
 * - 引用检查：调 adapter.getMicroflowReferences；首次激活该 tab 才触发请求。
 * - 微流信息：直接读取 StudioMicroflowDefinitionView 元数据。
 */
export function MicroflowStudioBottomPanel({
  microflowId,
  resource,
  adapterBundle
}: MicroflowStudioBottomPanelProps) {
  const validation = useMendixStudioStore(state =>
    microflowId ? state.validationByMicroflowId[microflowId] : undefined
  );
  const validationSummary = useMendixStudioStore(state =>
    microflowId ? state.validationSummaryByMicroflowId[microflowId] : undefined
  );

  const [activeKey, setActiveKey] = useState<BottomPanelTabKey>("validation");
  const [references, setReferences] = useState<MicroflowReference[] | null>(null);
  const [referencesLoading, setReferencesLoading] = useState(false);
  const [referencesError, setReferencesError] = useState<string | null>(null);
  const requestSeqRef = useRef(0);

  const issues = validation?.issues ?? [];
  const errorIssues = useMemo(() => issues.filter(issue => issue.severity === "error"), [issues]);
  const warningIssues = useMemo(() => issues.filter(issue => issue.severity === "warning"), [issues]);
  const infoIssues = useMemo(() => issues.filter(issue => issue.severity === "info"), [issues]);

  const loadReferences = useCallback(async () => {
    const adapter = adapterBundle?.resourceAdapter;
    if (!microflowId || !adapter?.getMicroflowReferences) {
      setReferences([]);
      setReferencesError(null);
      return;
    }
    const seq = (requestSeqRef.current += 1);
    setReferencesLoading(true);
    setReferencesError(null);
    try {
      const result = await adapter.getMicroflowReferences(microflowId);
      if (seq !== requestSeqRef.current) {
        return;
      }
      setReferences(result);
    } catch (error) {
      if (seq !== requestSeqRef.current) {
        return;
      }
      const message = getMicroflowErrorUserMessage(error);
      setReferencesError(message);
      Toast.error(message);
    } finally {
      if (seq === requestSeqRef.current) {
        setReferencesLoading(false);
      }
    }
  }, [adapterBundle, microflowId]);

  useEffect(() => {
    setReferences(null);
    setReferencesError(null);
  }, [microflowId]);

  useEffect(() => {
    if (activeKey === "references" && references === null && !referencesLoading) {
      void loadReferences();
    }
  }, [activeKey, references, referencesLoading, loadReferences]);

  if (!microflowId) {
    return (
      <div style={{ padding: 16 }}>
        <Empty title="尚未选择微流" description="请从 App Explorer 打开任一微流后查看底部面板。" />
      </div>
    );
  }

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        minHeight: 200,
        maxHeight: 320,
        background: "var(--semi-color-bg-1, #fafafa)",
        borderTop: "1px solid var(--semi-color-border, #e5e6eb)"
      }}
    >
      <Tabs
        type="line"
        size="small"
        activeKey={activeKey}
        onChange={key => setActiveKey(key as BottomPanelTabKey)}
        tabList={[
          {
            tab: (
              <Space>
                <span>验证结果</span>
                {validationSummary && (validationSummary.errorCount > 0 || validationSummary.warningCount > 0) ? (
                  <Tag size="small" color={validationSummary.errorCount > 0 ? "red" : "amber"}>
                    {validationSummary.errorCount > 0
                      ? `E${validationSummary.errorCount}`
                      : `W${validationSummary.warningCount}`}
                  </Tag>
                ) : null}
              </Space>
            ),
            itemKey: "validation"
          },
          {
            tab: (
              <Space>
                <span>配置检查</span>
                <Tag size="small" color={errorIssues.length > 0 ? "red" : "green"}>{errorIssues.length} 错误</Tag>
              </Space>
            ),
            itemKey: "configuration"
          },
          {
            tab: (
              <Space>
                <span>引用检查</span>
                {references ? <Tag size="small">{references.length}</Tag> : null}
              </Space>
            ),
            itemKey: "references"
          },
          {
            tab: <span>微流信息</span>,
            itemKey: "info"
          }
        ]}
      />
      <div style={{ flex: 1, overflow: "auto", padding: 12 }}>
        {activeKey === "validation" ? (
          <ValidationTabContent
            errorIssues={errorIssues}
            warningIssues={warningIssues}
            infoIssues={infoIssues}
            lastValidatedAt={validation?.lastValidatedAt}
            status={validation?.status}
          />
        ) : null}
        {activeKey === "configuration" ? (
          <ConfigurationTabContent issues={issues} />
        ) : null}
        {activeKey === "references" ? (
          <ReferencesTabContent
            loading={referencesLoading}
            error={referencesError}
            references={references}
            onReload={() => void loadReferences()}
          />
        ) : null}
        {activeKey === "info" ? (
          <InfoTabContent resource={resource} />
        ) : null}
      </div>
    </div>
  );
}

function ValidationTabContent({
  errorIssues,
  warningIssues,
  infoIssues,
  lastValidatedAt,
  status
}: {
  errorIssues: ReturnType<typeof Array.prototype.slice> extends never ? never : Array<{ id?: string; code: string; message: string; severity: string; nodeId?: string; flowId?: string; fieldPath?: string }>;
  warningIssues: ReturnType<typeof Array.prototype.slice> extends never ? never : Array<{ id?: string; code: string; message: string; severity: string; nodeId?: string; flowId?: string; fieldPath?: string }>;
  infoIssues: ReturnType<typeof Array.prototype.slice> extends never ? never : Array<{ id?: string; code: string; message: string; severity: string; nodeId?: string; flowId?: string; fieldPath?: string }>;
  lastValidatedAt?: string;
  status?: string;
}) {
  if (!errorIssues.length && !warningIssues.length && !infoIssues.length) {
    return (
      <Empty title="尚无验证记录" description={`点击工具栏 校验 触发后端校验后这里会显示结果。${status ? `当前状态：${status}。` : ""}`} />
    );
  }
  const renderGroup = (title: string, items: typeof errorIssues, color: "red" | "amber" | "blue") =>
    items.length ? (
      <div style={{ marginBottom: 12 }}>
        <Text strong>
          <Tag size="small" color={color} style={{ marginRight: 6 }}>{items.length}</Tag>
          {title}
        </Text>
        <List
          size="small"
          dataSource={items}
          renderItem={issue => (
            <List.Item key={issue.id ?? `${issue.code}:${issue.message}`}>
              <Space vertical align="start" spacing={2} style={{ width: "100%" }}>
                <Space>
                  <Tag size="small" color={color}>{issue.code}</Tag>
                  {issue.nodeId ? <Tag size="small">node:{issue.nodeId}</Tag> : null}
                  {issue.flowId ? <Tag size="small">flow:{issue.flowId}</Tag> : null}
                  {issue.fieldPath ? <Tag size="small" color="grey">{issue.fieldPath}</Tag> : null}
                </Space>
                <Text size="small">{issue.message}</Text>
              </Space>
            </List.Item>
          )}
        />
      </div>
    ) : null;
  return (
    <div>
      <Space style={{ marginBottom: 8 }}>
        <Tag color="grey" size="small">{status ?? "idle"}</Tag>
        <Text size="small" type="tertiary">最近校验：{formatDate(lastValidatedAt)}</Text>
      </Space>
      {renderGroup("错误", errorIssues, "red")}
      {renderGroup("警告", warningIssues, "amber")}
      {renderGroup("提示", infoIssues, "blue")}
    </div>
  );
}

function ConfigurationTabContent({
  issues
}: {
  issues: Array<{ id?: string; code: string; message: string; severity: string; source?: string; fieldPath?: string }>;
}) {
  if (issues.length === 0) {
    return <Empty title="尚无配置问题" description="校验通过后此处保持为空。" />;
  }
  // Group by source so users can see metadata / parameter / decision groupings.
  const groups = issues.reduce<Record<string, typeof issues>>((acc, issue) => {
    const key = issue.source ?? "schema";
    acc[key] = acc[key] ?? [];
    acc[key].push(issue);
    return acc;
  }, {});
  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      {Object.entries(groups).map(([source, list]) => (
        <div key={source} style={{ width: "100%" }}>
          <Text strong style={{ display: "block", marginBottom: 6 }}>{source} ({list.length})</Text>
          <Table
            size="small"
            pagination={false}
            dataSource={list.map((issue, index) => ({ key: issue.id ?? `${source}-${index}`, ...issue }))}
            columns={[
              { title: "Code", dataIndex: "code", width: 220 },
              { title: "Severity", dataIndex: "severity", width: 100, render: severity => <Tag size="small" color={severity === "error" ? "red" : severity === "warning" ? "amber" : "blue"}>{severity}</Tag> },
              { title: "Field", dataIndex: "fieldPath", width: 220 },
              { title: "Message", dataIndex: "message" }
            ]}
          />
        </div>
      ))}
    </Space>
  );
}

function ReferencesTabContent({
  loading,
  error,
  references,
  onReload
}: {
  loading: boolean;
  error: string | null;
  references: MicroflowReference[] | null;
  onReload: () => void;
}) {
  if (loading && references === null) {
    return (
      <div style={{ padding: 24, textAlign: "center" }}>
        <Spin tip="加载引用..." />
      </div>
    );
  }
  if (error) {
    return (
      <Empty title="引用加载失败" description={error}>
        <Button size="small" onClick={onReload}>重试</Button>
      </Empty>
    );
  }
  if (!references || references.length === 0) {
    return <Empty title="尚无引用" description="该微流没有被其它微流 / 页面引用，删除时不会触发引用阻止。" />;
  }
  return (
    <Table
      size="small"
      pagination={false}
      dataSource={references.map((reference, index) => ({ key: reference.id ?? `${reference.sourceType}-${index}`, ...reference }))}
      columns={[
        { title: "Source Type", dataIndex: "sourceType", width: 140 },
        { title: "Source Name", dataIndex: "sourceName", width: 240 },
        { title: "Source ID", dataIndex: "sourceId", width: 220 },
        { title: "Updated At", dataIndex: "updatedAt", render: (value: string) => formatDate(value) }
      ]}
    />
  );
}

function InfoTabContent({ resource }: { resource?: StudioMicroflowDefinitionView }) {
  if (!resource) {
    return <Empty title="资源元数据未就绪" description="资源加载完成后此处会显示版本、作者等信息。" />;
  }
  return (
    <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
      <Title heading={6} style={{ margin: 0 }}>{resource.qualifiedName ?? resource.name ?? resource.id}</Title>
      <Text type="tertiary" size="small">{resource.description ?? "(无描述)"}</Text>
      <Space wrap spacing={6}>
        <Tag size="small">id:{resource.id}</Tag>
        <Tag size="small">module:{resource.moduleId ?? "-"}</Tag>
        <Tag size="small">version:{resource.version ?? "-"}</Tag>
        {resource.publishStatus ? <Tag size="small" color="blue">{resource.publishStatus}</Tag> : null}
        {resource.archived ? <Tag size="small" color="grey">archived</Tag> : null}
      </Space>
      <Text size="small">最近修改：{formatDate(resource.updatedAt)}</Text>
      <Text size="small">引用计数：{resource.referenceCount}</Text>
    </Space>
  );
}
