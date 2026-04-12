import { WorkflowNodeRenderer, useNodeRender } from "@flowgram.ai/free-layout-editor";
import { NodeRenderHeader } from "./header";
import { NodeContentMap } from "./content";
import { NodeRenderWrapper } from "./wrapper";
import { ExecuteStatusBar } from "./execute-status-bar";
import { useFlowgramSelectionBridge } from "../flowgram/selection-bridge";

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" && !Array.isArray(value) ? (value as Record<string, unknown>) : {};
}

export function WorkflowNodeRender() {
  const render = useNodeRender();
  const { selectNode, reportPortClick } = useFlowgramSelectionBridge();
  const data = asRecord(render.data);
  const title = typeof data.title === "string" && data.title ? data.title : String(render.type);
  const status = (data.executionState as "idle" | "running" | "success" | "failed" | "skipped" | "blocked" | undefined) ?? "idle";
  const nodeKeyCandidate = (render.node as { id?: unknown } | undefined)?.id;
  const nodeKey = typeof nodeKeyCandidate === "string" ? nodeKeyCandidate : "";

  return (
    <WorkflowNodeRenderer
      node={render.node}
      className="wf-node-render-shell"
      onPortClick={(port, event) => {
        if (event && typeof (event as { stopPropagation?: () => void }).stopPropagation === "function") {
          (event as { stopPropagation: () => void }).stopPropagation();
        }
        const portType = port.portType === "input" ? "input" : "output";
        const portKey = String(port.portID ?? (portType === "input" ? "input" : "output"));
        reportPortClick({
          nodeKey: nodeKey || String(render.id),
          portKey,
          portType
        });
      }}
    >
      <NodeRenderWrapper
        selected={render.selected}
        onClick={() => {
          if (nodeKey) {
            selectNode(nodeKey);
          }
        }}
      >
        <NodeRenderHeader title={title} type={String(render.type)} />
        <ExecuteStatusBar status={status} />
        <NodeContentMap type={String(render.type)} data={data} />
      </NodeRenderWrapper>
    </WorkflowNodeRenderer>
  );
}
