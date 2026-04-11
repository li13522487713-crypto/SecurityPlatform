import { useMemo, useState } from "react";
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
import { WorkflowHeader } from "../components/WorkflowHeader";
import { CanvasToolbar } from "../components/CanvasToolbar";
import { NodePanelPopover } from "../components/NodePanelPopover";
import { NodeCard } from "../components/NodeCard";
import { PropertiesPanel } from "../components/PropertiesPanel";
import { TestRunPanel } from "../components/TestRunPanel";
import "./workflow-editor.css";

export interface WorkflowEditorReactProps {
  workflowId: string;
  locale?: string;
  apiClient: object;
  onBack?: () => void;
}

interface CanvasNode {
  key: string;
  type: string;
  x: number;
  y: number;
}

const INITIAL_NODES: CanvasNode[] = [
  { key: "entry_1", type: "Entry", x: 160, y: 120 },
  { key: "llm_1", type: "Llm", x: 620, y: 120 },
  { key: "exit_1", type: "Exit", x: 1080, y: 120 }
];

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

  const selectedNode = useMemo(() => {
    const node = canvasNodes.find((item: CanvasNode) => item.key === selectedNodeKey);
    if (!node) {
      return null;
    }
    return WORKFLOW_NODE_CATALOG.find((item) => item.type === node.type) ?? null;
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
    return result;
  }, []);

  const scale = zoom / 100;

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
        onSave={() => {
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
                  title={t(meta.titleKey)}
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
            const key = `${nodeType.toLowerCase()}_${canvasNodes.length + 1}`;
            setCanvasNodes((prev: CanvasNode[]) => [...prev, { key, type: nodeType, x: 320 + prev.length * 140, y: 320 }]);
            setSelectedNodeKey(key);
            setShowNodePanel(false);
            setIsDirty(true);
          }}
        />

        <PropertiesPanel visible={Boolean(selectedNode)} selectedNode={selectedNode} onClose={() => setSelectedNodeKey("")} />

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

