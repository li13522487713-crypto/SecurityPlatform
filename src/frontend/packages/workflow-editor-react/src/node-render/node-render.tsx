import { WorkflowNodeRenderer, useNodeRender } from "@flowgram.ai/free-layout-editor";
import { NodeRenderHeader } from "./header";
import { NodeContentMap } from "./content";
import { NodeRenderWrapper } from "./wrapper";
import { ExecuteStatusBar } from "./execute-status-bar";

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" && !Array.isArray(value) ? (value as Record<string, unknown>) : {};
}

export function WorkflowNodeRender() {
  const render = useNodeRender();
  const data = asRecord(render.data);
  const title = typeof data.title === "string" && data.title ? data.title : String(render.type);
  const status = (data.executionState as "idle" | "running" | "success" | "failed" | "skipped" | undefined) ?? "idle";

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
