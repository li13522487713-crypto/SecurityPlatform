import { WorkflowNodeRenderer, useNodeRender } from "@flowgram.ai/free-layout-editor";
import { useEffect } from "react";
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
  const { reportNodeSelection } = useFlowgramSelectionBridge();
  const data = asRecord(render.data);
  const title = typeof data.title === "string" && data.title ? data.title : String(render.type);
  const status = (data.executionState as "idle" | "running" | "success" | "failed" | "skipped" | "blocked" | undefined) ?? "idle";
  const nodeKeyCandidate = (render.node as { id?: unknown } | undefined)?.id;
  const nodeKey = typeof nodeKeyCandidate === "string" ? nodeKeyCandidate : "";

  useEffect(() => {
    if (!nodeKey) {
      return;
    }
    reportNodeSelection(nodeKey, render.selected);
    return () => {
      reportNodeSelection(nodeKey, false);
    };
  }, [nodeKey, render.selected, reportNodeSelection]);

  return (
    <WorkflowNodeRenderer node={render.node} className="wf-node-render-shell">
      <NodeRenderWrapper selected={render.selected}>
        <NodeRenderHeader title={title} type={String(render.type)} />
        <ExecuteStatusBar status={status} />
        <NodeContentMap type={String(render.type)} data={data} />
      </NodeRenderWrapper>
    </WorkflowNodeRenderer>
  );
}
