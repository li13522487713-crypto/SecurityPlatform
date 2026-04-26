import { useEffect, useMemo, useState } from "react";
import { Badge, Button, Dropdown, Empty, Space, Tabs, Tag, Tooltip, Typography } from "@douyinfe/semi-ui";
import { IconClose, IconCopy, IconDelete, IconInfoCircle, IconLock, IconMore, IconUnlock } from "@douyinfe/semi-icons";
import type { MicroflowNodeOutput, MicroflowVariable } from "../schema";
import {
  MicroflowAdvancedSection,
  MicroflowDocumentationSection,
  MicroflowErrorHandlingSection,
  MicroflowOutputSection
} from "./sections";
import {
  getMicroflowNodeFormKey,
  microflowNodeFormRegistry
} from "./node-forms";
import type {
  MicroflowNodeFormProps,
  MicroflowNodePatch,
  MicroflowPropertyPanelProps,
  MicroflowPropertyTabKey
} from "./types";

export * from "./controls";
export * from "./node-forms";
export * from "./sections";
export * from "./types";

const { Text } = Typography;

const tabLabels: Record<MicroflowPropertyTabKey, string> = {
  properties: "属性",
  documentation: "文档",
  errorHandling: "错误处理",
  output: "输出",
  advanced: "高级"
};

function nodeTypeLabel(props: MicroflowPropertyPanelProps): string {
  const node = props.selectedNode;
  if (!node) {
    return "";
  }
  return node.type === "activity" ? node.config.activityType : node.type;
}

function nodeIconLabel(type: string): string {
  return type.slice(0, 1).toUpperCase();
}

function MicroflowPropertyPanelHeader({
  props,
  issueCount,
  dirty,
  onPatch
}: {
  props: MicroflowPropertyPanelProps;
  issueCount: number;
  dirty: boolean;
  onPatch: (patch: MicroflowNodePatch) => void;
}) {
  const node = props.selectedNode;
  if (!node) {
    return null;
  }
  const type = nodeTypeLabel(props);
  const locked = Boolean(props.readonly || node.locked);
  const menu = (
    <Dropdown.Menu>
      <Dropdown.Item icon={<IconCopy />} onClick={() => props.onDuplicateNode?.(node.id)}>复制节点</Dropdown.Item>
      <Dropdown.Item disabled>粘贴配置</Dropdown.Item>
      <Dropdown.Item onClick={() => props.onLocateNode?.(node.id)}>定位到画布</Dropdown.Item>
      <Dropdown.Item disabled>查看执行记录</Dropdown.Item>
      <Dropdown.Item type="danger" icon={<IconDelete />} onClick={() => props.onDeleteNode?.(node.id)}>删除节点</Dropdown.Item>
    </Dropdown.Menu>
  );

  return (
    <div style={{ padding: 14, borderBottom: "1px solid var(--semi-color-border, #e5e6eb)", background: "var(--semi-color-bg-2, #fff)" }}>
      <div style={{ display: "grid", gridTemplateColumns: "36px minmax(0, 1fr) auto", gap: 10, alignItems: "center" }}>
        <span style={{ width: 34, height: 34, borderRadius: 12, display: "inline-flex", alignItems: "center", justifyContent: "center", background: "rgba(22, 93, 255, 0.1)", color: "#165dff", fontWeight: 700 }}>
          {nodeIconLabel(type)}
        </span>
        <div style={{ minWidth: 0 }}>
          <Space spacing={6}>
            <Text strong ellipsis={{ showTooltip: true }} style={{ maxWidth: 168 }}>{node.title}</Text>
            <Tag size="small">v2</Tag>
            {dirty ? <Tag size="small" color="orange">Dirty</Tag> : null}
            {issueCount > 0 ? <Badge count={issueCount} type="danger" /> : null}
          </Space>
          <Text type="tertiary" size="small" ellipsis={{ showTooltip: true }} style={{ display: "block", maxWidth: 230 }}>{node.description || type}</Text>
        </div>
        <Space spacing={2}>
          <Tooltip content={locked ? "解锁节点" : "锁定节点"}>
            <Button size="small" theme="borderless" icon={locked ? <IconLock /> : <IconUnlock />} onClick={() => onPatch({ node: { locked: !node.locked } })} />
          </Tooltip>
          <Tooltip content="复制节点">
            <Button size="small" theme="borderless" icon={<IconCopy />} onClick={() => props.onDuplicateNode?.(node.id)} />
          </Tooltip>
          <Dropdown trigger="click" position="bottomRight" render={menu}>
            <Button size="small" theme="borderless" icon={<IconMore />} />
          </Dropdown>
          <Button size="small" theme="borderless" icon={<IconClose />} onClick={props.onClose} />
        </Space>
      </div>
      {locked ? <Text type="tertiary" size="small" style={{ display: "block", marginTop: 8 }}>节点已锁定，表单进入只读状态。</Text> : null}
    </div>
  );
}

function MicroflowPropertyTabs({
  activeKey,
  tabs,
  onChange
}: {
  activeKey: MicroflowPropertyTabKey;
  tabs: MicroflowPropertyTabKey[];
  onChange: (key: MicroflowPropertyTabKey) => void;
}) {
  return (
    <Tabs type="line" size="small" activeKey={activeKey} onChange={key => onChange(key as MicroflowPropertyTabKey)}>
      {tabs.map(tab => <Tabs.TabPane key={tab} itemKey={tab} tab={tabLabels[tab]} />)}
    </Tabs>
  );
}

function createVariableFromOutput(output: MicroflowNodeOutput): MicroflowVariable {
  return {
    id: output.id,
    name: output.name,
    type: output.type,
    scope: "node"
  };
}

export function buildVariablesForPropertyPanel(props: MicroflowPropertyPanelProps): MicroflowVariable[] {
  const outputVariables = props.schema.nodes.flatMap(node => (node.outputs ?? []).map(createVariableFromOutput));
  const parameterVariables = props.schema.parameters.map(parameter => ({
    id: parameter.id,
    name: parameter.name,
    type: parameter.type,
    scope: "microflow" as const
  }));
  return [
    ...props.schema.variables,
    ...parameterVariables,
    ...outputVariables,
    { id: "latestError", name: "latestError", type: { kind: "unknown", name: "Error" }, scope: "latestError" }
  ];
}

export function MicroflowPropertyPanel(props: MicroflowPropertyPanelProps) {
  const [activeTab, setActiveTab] = useState<MicroflowPropertyTabKey>("properties");
  const [dirtyNodeIds, setDirtyNodeIds] = useState<string[]>([]);
  const selectedNode = props.selectedNode;
  const issues = useMemo(() => selectedNode ? props.validationIssues.filter(issue => issue.nodeId === selectedNode.id) : [], [props.validationIssues, selectedNode]);
  const variables = useMemo(() => buildVariablesForPropertyPanel(props), [props]);
  const trace = selectedNode ? props.traceFrames?.find(frame => frame.nodeId === selectedNode.id) : undefined;
  const formKey = selectedNode ? getMicroflowNodeFormKey(selectedNode) : "";
  const formItem = selectedNode ? microflowNodeFormRegistry[formKey] : undefined;
  const tabs = formItem?.tabs ?? ["properties", "documentation"];
  const readonly = Boolean(props.readonly || selectedNode?.locked);

  useEffect(() => {
    if (!tabs.includes(activeTab)) {
      setActiveTab(tabs[0] ?? "properties");
    }
  }, [activeTab, tabs]);

  if (!selectedNode) {
    return (
      <div style={{ height: "100%", display: "flex", alignItems: "center", justifyContent: "center", padding: 20 }}>
        <Empty
          image={<IconInfoCircle />}
          title="请选择一个节点"
          description="在画布中点击节点后，可在这里配置节点属性。也可以从左侧 Nodes 面板拖拽或双击快速添加。"
        />
      </div>
    );
  }

  const formProps: MicroflowNodeFormProps = {
    node: selectedNode,
    schema: props.schema,
    variables,
    edges: props.schema.edges,
    issues,
    readonly,
    onPatch: patch => handlePatch(patch)
  };

  function handlePatch(patch: MicroflowNodePatch) {
    if (!selectedNode || readonly) {
      return;
    }
    props.onNodeChange(selectedNode.id, patch);
    setDirtyNodeIds(current => current.includes(selectedNode.id) ? current : [...current, selectedNode.id]);
  }

  function renderActiveTab() {
    if (!formItem) {
      return (
        <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
          <Text type="tertiary">暂不支持该节点配置。</Text>
          <pre style={{ width: "100%", maxHeight: 280, overflow: "auto", background: "var(--semi-color-fill-0)", padding: 10, borderRadius: 8 }}>
            {JSON.stringify(selectedNode.config, null, 2)}
          </pre>
        </Space>
      );
    }
    if (activeTab === "documentation") {
      return <MicroflowDocumentationSection props={formProps} />;
    }
    if (activeTab === "errorHandling") {
      return <MicroflowErrorHandlingSection props={formProps} />;
    }
    if (activeTab === "output") {
      return <MicroflowOutputSection props={formProps} />;
    }
    if (activeTab === "advanced") {
      return <MicroflowAdvancedSection props={formProps} />;
    }
    return formItem.renderProperties(formProps);
  }

  return (
    <div style={{ height: "100%", display: "grid", gridTemplateRows: "auto auto minmax(0, 1fr) auto", background: "var(--semi-color-bg-1, #fff)" }}>
      <MicroflowPropertyPanelHeader props={props} issueCount={issues.length} dirty={dirtyNodeIds.includes(selectedNode.id)} onPatch={handlePatch} />
      <div style={{ padding: "0 12px", borderBottom: "1px solid var(--semi-color-border, #e5e6eb)" }}>
        <MicroflowPropertyTabs activeKey={activeTab} tabs={tabs} onChange={setActiveTab} />
      </div>
      <div style={{ minHeight: 0, overflow: "auto", padding: 14 }}>
        <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
          {issues.length > 0 ? (
            <div style={{ width: "100%", padding: 10, borderRadius: 10, background: "rgba(249, 57, 32, 0.06)", border: "1px solid rgba(249, 57, 32, 0.18)" }}>
              {issues.map(issue => <Text key={issue.id} type={issue.severity === "error" ? "danger" : "warning"} size="small" style={{ display: "block" }}>{issue.message}</Text>)}
            </div>
          ) : null}
          {trace ? <Tag color={trace.error ? "red" : "green"}>Last run: {trace.status} · {trace.durationMs}ms</Tag> : null}
          {renderActiveTab()}
        </Space>
      </div>
      <div style={{ padding: "8px 12px", borderTop: "1px solid var(--semi-color-border, #e5e6eb)", background: "rgba(255,255,255,0.9)" }}>
        <Text type="tertiary" size="small">配置变更会实时写入 MicroflowSchema，保存与校验使用最新 schema。</Text>
      </div>
    </div>
  );
}
