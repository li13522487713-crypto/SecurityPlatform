import "@flowgram.ai/free-layout-editor/index.css";
import "reflect-metadata";
import { PlaygroundReact, type PlaygroundRef } from "@flowgram.ai/playground-react";
import { useRef } from "react";
import type { CanvasSchema, NodeTypeMetadata } from "../types";
import { FlowgramBackgroundLayer } from "./layers/background-layer";
import { FlowgramHoverLayer } from "./layers/hover-layer";
import { WorkflowLoader } from "./workflow-loader";

interface WorkflowRenderProviderProps {
  canvas: CanvasSchema;
  nodeTypesMeta: NodeTypeMetadata[];
  readonly?: boolean;
  onCanvasChange: (next: CanvasSchema) => void;
}

export function WorkflowRenderProvider(props: WorkflowRenderProviderProps) {
  const playgroundRef = useRef<PlaygroundRef | null>(null);

  return (
    <div className="wf-flowgram-root">
      <FlowgramBackgroundLayer />
      <PlaygroundReact ref={playgroundRef} playground={{ autoFocus: true, autoResize: true }}>
        <WorkflowLoader canvas={props.canvas} readonly={props.readonly} nodeTypesMeta={props.nodeTypesMeta} onCanvasChange={props.onCanvasChange} />
      </PlaygroundReact>
      <FlowgramHoverLayer />
    </div>
  );
}
