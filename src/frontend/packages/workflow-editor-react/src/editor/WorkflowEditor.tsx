import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { FreeLayoutEditor } from "@flowgram.ai/free-layout-editor";
import "@flowgram.ai/free-layout-editor/index.css";
import { createFreeLinesPlugin } from "@flowgram.ai/free-lines-plugin";
import { createFreeHistoryPlugin } from "@flowgram.ai/free-history-plugin";
import { createFreeSnapPlugin } from "@flowgram.ai/free-snap-plugin";
import { createFreeAutoLayoutPlugin } from "@flowgram.ai/free-auto-layout-plugin";
import { createContainerNodePlugin } from "@flowgram.ai/free-container-plugin";
import { createMinimapPlugin } from "@flowgram.ai/minimap-plugin";
import { ensureWorkflowI18n } from "../i18n";
import { WORKFLOW_NODE_CATALOG, type WorkflowNodeCatalogItem } from "../constants/node-catalog";
import { createMetadataBundle, mergeNodeDefaults, NodeRegistry } from "../node-registry";
import type {
  CanvasSchema,
  NodeTemplateMetadata,
  NodeTypeMetadata,
  WorkflowDetailResponse,
  WorkflowNodeTypeKey,
  WorkflowSaveRequest
} from "../types";
import { WorkflowHeader } from "../components/WorkflowHeader";
import { CanvasToolbar } from "../components/CanvasToolbar";
import { NodePanelPopover } from "../components/NodePanelPopover";
import { NodeCard } from "../components/NodeCard";
import { PropertiesPanel } from "../components/PropertiesPanel";
import { TestRunPanel } from "../components/TestRunPanel";
import { buildVariableSuggestions } from "./smoke-utils";
import "./workflow-editor.css";

interface WorkflowApiClient {
  getDetail?: (id: string) => Promise<{ data?: WorkflowDetailResponse }>;
  saveDraft?: (id: string, req: WorkflowSaveRequest) => Promise<unknown>;
  getNodeTypes?: () => Promise<{ data?: NodeTypeMetadata[] }>;
  getNodeTemplates?: () => Promise<{ data?: NodeTemplateMetadata[] }>;
}

export interface WorkflowEditorReactProps {
  workflowId: string;
  locale?: string;
  apiClient: WorkflowApiClient;
  onBack?: () => void;
}

interface CanvasNode {
  key: string;
  type: WorkflowNodeTypeKey;
  title: string;
  x: number;
  y: number;
  configs: Record<string, unknown>;
  inputMappings: Record<string, string>;
}

const nodeRegistry = new NodeRegistry();

const INITIAL_NODES: CanvasNode[] = [
  {
    key: "entry_1",
    type: "Entry",
    title: "开始",
    x: 160,
    y: 120,
    configs: { entry: { variable: "USER_INPUT", autoSaveHistory: true } },
    inputMappings: {}
  },
  {
    key: "llm_1",
    type: "Llm",
    title: "大模型",
    x: 620,
    y: 120,
    configs: { llm: { provider: "qwen", model: "qwen-max", userPrompt: "{{entry_1.output}}" } },
    inputMappings: {}
  },
  {
    key: "exit_1",
    type: "Exit",
    title: "结束",
    x: 1080,
    y: 120,
    configs: { exit: { terminateMode: "return", template: "{{llm_1.result}}" } },
    inputMappings: {}
  }
];

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

function parseCanvasNode(node: unknown): CanvasNode | null {
  if (!isRecord(node)) {
    return null;
  }
  const key = typeof node.key === "string" ? node.key : "";
  const type = typeof node.type === "string" ? (node.type as WorkflowNodeTypeKey) : "TextProcessor";
  const layout = isRecord(node.layout) ? node.layout : {};
  const title = typeof node.title === "string" ? node.title : type;
  const configs = isRecord(node.configs) ? node.configs : {};
  const inputMappings = isRecord(node.inputMappings)
    ? Object.fromEntries(Object.entries(node.inputMappings).filter(([, value]) => typeof value === "string")) as Record<string, string>
    : {};
  if (!key) {
    return null;
  }
  return {
    key,
    type,
    title,
    x: typeof layout.x === "number" ? layout.x : 120,
    y: typeof layout.y === "number" ? layout.y : 120,
    configs,
    inputMappings
  };
}

function parseCanvasJson(json: string | undefined): CanvasNode[] {
  if (!json) {
    return INITIAL_NODES;
  }
  try {
    const parsed = JSON.parse(json) as CanvasSchema;
    if (!Array.isArray(parsed.nodes)) {
      return INITIAL_NODES;
    }
    const nextNodes = parsed.nodes.map((item) => parseCanvasNode(item)).filter((item): item is CanvasNode => item !== null);
    return nextNodes.length > 0 ? nextNodes : INITIAL_NODES;
  } catch {
    return INITIAL_NODES;
  }
}

function toCanvasJson(nodes: CanvasNode[]): string {
  const payload: CanvasSchema = {
    nodes: nodes.map((node) => ({
      key: node.key,
      type: node.type,
      title: node.title,
      layout: { x: node.x, y: node.y, width: 360, height: 160 },
      configs: node.configs,
      inputMappings: node.inputMappings
    })),
    connections: []
  };
  return JSON.stringify(payload);
}

export function WorkflowEditorReact(props: WorkflowEditorReactProps) {
  ensureWorkflowI18n(props.locale ?? "zh-CN");
  const { t } = useTranslation();

  const [workflowName, setWorkflowName] = useState(`Workflow_${props.workflowId}`);
  const [isDirty, setIsDirty] = useState(false);
  const [zoom, setZoom] = useState(100);
  const [selectedNodeKey, setSelectedNodeKey] = useState<string>("llm_1");
  const [showNodePanel, setShowNodePanel] = useState(false);
  const [showTestPanel, setShowTestPanel] = useState(false);
  const [logs, setLogs] = useState<string[]>([]);
  const [canvasNodes, setCanvasNodes] = useState<CanvasNode[]>(INITIAL_NODES);
  const [nodeTypesMeta, setNodeTypesMeta] = useState<NodeTypeMetadata[]>([]);
  const [nodeTemplates, setNodeTemplates] = useState<NodeTemplateMetadata[]>([]);

  useEffect(() => {
    let disposed = false;
    const load = async () => {
      if (props.apiClient.getNodeTypes) {
        const response = await props.apiClient.getNodeTypes();
        if (!disposed) {
          setNodeTypesMeta(response.data ?? []);
        }
      }
      if (props.apiClient.getNodeTemplates) {
        const response = await props.apiClient.getNodeTemplates();
        if (!disposed) {
          setNodeTemplates(response.data ?? []);
        }
      }
      if (props.apiClient.getDetail) {
        const response = await props.apiClient.getDetail(props.workflowId);
        if (!disposed && response.data) {
          setWorkflowName(response.data.name || `Workflow_${props.workflowId}`);
          setCanvasNodes(parseCanvasJson(response.data.canvasJson));
        }
      }
    };
    void load();
    return () => {
      disposed = true;
    };
  }, [props.apiClient, props.workflowId]);

  const metadataBundle = useMemo(() => createMetadataBundle(nodeTypesMeta, nodeTemplates), [nodeTemplates, nodeTypesMeta]);

  const selectedNode = useMemo(() => {
    const node = canvasNodes.find((item: CanvasNode) => item.key === selectedNodeKey);
    if (!node) {
      return null;
    }
    return node;
  }, [canvasNodes, selectedNodeKey]);

  const plugins = useMemo(
    () => () => [
      createFreeLinesPlugin({}),
      createMinimapPlugin({}),
      createFreeHistoryPlugin({}),
      createFreeSnapPlugin({}),
      createFreeAutoLayoutPlugin({}),
      createContainerNodePlugin({})
    ],
    []
  );

  const nodeMap = useMemo(() => {
    const result = new Map<string, WorkflowNodeCatalogItem>();
    for (const item of WORKFLOW_NODE_CATALOG) {
      result.set(item.type, item);
    }
    for (const type of nodeRegistry.getAllTypes()) {
      if (!result.has(type)) {
        result.set(type, {
          type,
          titleKey: `wfUi.nodeTypes.${type}`,
          category: "dataProcess",
          color: "#64748B",
          iconText: type.slice(0, 2).toUpperCase()
        });
      }
    }
    return result;
  }, []);

  const scale = zoom / 100;
  const variableSuggestions = useMemo(
    () =>
      buildVariableSuggestions(
        canvasNodes.map((node) => ({ key: node.key, type: node.type, configs: node.configs, x: node.x })),
        selectedNodeKey
      ),
    [canvasNodes, selectedNodeKey]
  );

  return (
    <div className="wf-react-editor-page">
      <WorkflowHeader
        name={workflowName}
        dirty={isDirty}
        onNameChange={(value) => {
          setWorkflowName(value);
          setIsDirty(true);
        }}
        onBack={() => props.onBack?.()}
        onSave={async () => {
          if (props.apiClient.saveDraft) {
            await props.apiClient.saveDraft(props.workflowId, { canvasJson: toCanvasJson(canvasNodes) });
          }
          setIsDirty(false);
          setLogs((prev: string[]) => [...prev, `${new Date().toLocaleTimeString()} save_draft`]);
        }}
        onPublish={() => setLogs((prev: string[]) => [...prev, `${new Date().toLocaleTimeString()} publish`])}
      />
      <div className="wf-react-canvas-shell">
        <div className="wf-react-dot-grid" />
        <div className="wf-react-flowgram-layer">
          <FreeLayoutEditor plugins={plugins} />
        </div>
        <div className="wf-react-node-layer" style={{ transform: `scale(${scale})`, transformOrigin: "0 0" }}>
          {canvasNodes.map((node: CanvasNode) => {
            const meta = nodeMap.get(node.type);
            if (!meta) {
              return null;
            }
            return (
              <div key={node.key} className="wf-react-node-wrap" style={{ left: node.x, top: node.y }}>
                <NodeCard
                  title={node.title || t(meta.titleKey)}
                  color={meta.color}
                  iconText={meta.iconText}
                  selected={selectedNodeKey === node.key}
                  subtitle={node.type}
                  onClick={() => setSelectedNodeKey(node.key)}
                />
              </div>
            );
          })}
        </div>

        <NodePanelPopover
          visible={showNodePanel}
          nodes={WORKFLOW_NODE_CATALOG}
          onSelect={(nodeType) => {
            const definition = nodeRegistry.resolve(nodeType);
            const normalizedType = definition.type;
            const template = metadataBundle.templatesMap.get(normalizedType);
            const key = `${nodeType.toLowerCase()}_${canvasNodes.length + 1}`;
            const nextConfigs = mergeNodeDefaults(definition, template, {});
            const catalog = nodeMap.get(normalizedType);
            setCanvasNodes((prev: CanvasNode[]) => [
              ...prev,
              {
                key,
                type: normalizedType,
                title: catalog ? t(catalog.titleKey) : normalizedType,
                x: 320 + prev.length * 140,
                y: 320,
                configs: nextConfigs,
                inputMappings: {}
              }
            ]);
            setSelectedNodeKey(key);
            setShowNodePanel(false);
            setIsDirty(true);
          }}
        />

        <PropertiesPanel
          visible={Boolean(selectedNode)}
          selectedNode={selectedNode}
          selectedNodeLabel={selectedNode ? selectedNode.title || t(nodeMap.get(selectedNode.type)?.titleKey ?? selectedNode.type) : ""}
          template={selectedNode ? metadataBundle.templatesMap.get(selectedNode.type) : undefined}
          nodeTypeMeta={selectedNode ? metadataBundle.nodeTypesMap.get(selectedNode.type) : undefined}
          variableSuggestions={variableSuggestions}
          onChangeNode={(next) => {
            if (!selectedNode) {
              return;
            }
            setCanvasNodes((prev) =>
              prev.map((node) => (node.key === selectedNode.key ? { ...node, title: next.title, configs: next.configs } : node))
            );
            setIsDirty(true);
          }}
          onClose={() => setSelectedNodeKey("")}
        />

        <TestRunPanel
          visible={showTestPanel}
          logs={logs}
          onClose={() => setShowTestPanel(false)}
          onRun={() => setLogs((prev: string[]) => [...prev, `${new Date().toLocaleTimeString()} execution_start`])}
        />

        <CanvasToolbar
          zoom={zoom}
          onZoomChange={(value: number) => setZoom(value)}
          onToggleNodePanel={() => setShowNodePanel((value: boolean) => !value)}
          onRun={() => setShowTestPanel((value: boolean) => !value)}
        />
      </div>
    </div>
  );
}

