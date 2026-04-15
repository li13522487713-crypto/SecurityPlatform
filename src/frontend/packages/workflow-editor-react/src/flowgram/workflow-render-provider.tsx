import "@flowgram.ai/free-layout-editor/index.css";
import "reflect-metadata";
import type { onDragLineEndParams } from "@flowgram.ai/free-layout-editor";
import type { CanvasSchema, NodeTypeMetadata } from "../types";
import { FlowgramBackgroundLayer } from "./layers/background-layer";
import { FlowgramHoverLayer } from "./layers/hover-layer";
import { WorkflowLoader } from "./workflow-loader";

interface WorkflowRenderProviderProps {
  canvas: CanvasSchema;
  nodeTypesMeta: NodeTypeMetadata[];
  readonly?: boolean;
  edgeStateByKey?: Record<string, "idle" | "running" | "incomplete" | "success" | "failed" | "skipped">;
  onCanvasChange: (next: CanvasSchema) => void;
  onSelectionChange?: (nodeKeys: string[]) => void;
  onPortClick?: (params: { nodeKey: string; portKey: string; portType: "input" | "output" }) => void;
  onDragLineEnd?: (params: onDragLineEndParams) => void;
}

export function WorkflowRenderProvider(props: WorkflowRenderProviderProps) {
  return (
    <div className="wf-flowgram-root">
      <FlowgramBackgroundLayer />
      <WorkflowLoader
        canvas={props.canvas}
        readonly={props.readonly}
        nodeTypesMeta={props.nodeTypesMeta}
        edgeStateByKey={props.edgeStateByKey}
        onCanvasChange={props.onCanvasChange}
        onSelectionChange={props.onSelectionChange}
        onPortClick={props.onPortClick}
        onDragLineEnd={props.onDragLineEnd}
      />
      <FlowgramHoverLayer />
    </div>
  );
}
