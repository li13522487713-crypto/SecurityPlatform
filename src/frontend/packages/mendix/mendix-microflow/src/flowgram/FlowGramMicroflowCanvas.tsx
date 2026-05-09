import {
  FlowGramMicroflowNativeCanvas,
  type FlowGramMicroflowNativeCanvasProps,
} from "./FlowGramMicroflowNativeCanvas";
import type { MicroflowSchema } from "../schema/types";
import type { FlowGramMicroflowSelection } from "./FlowGramMicroflowTypes";

/**
 * Compatibility export shim:
 * editor/index.tsx still imports `FlowGramMicroflowCanvas`.
 * Keep this alias so existing call sites continue to work while the native canvas remains the single implementation.
 */
export type FlowGramMicroflowCanvasProps =
  Omit<FlowGramMicroflowNativeCanvasProps, "schema" | "onSchemaChange" | "onSelectionChange"> & {
    schema: MicroflowSchema;
    onSchemaChange: (nextSchema: MicroflowSchema, reason: string) => void;
    onSelectionChange: (selection: FlowGramMicroflowSelection) => void;
  };

export function FlowGramMicroflowCanvas(props: FlowGramMicroflowCanvasProps) {
  return <FlowGramMicroflowNativeCanvas {...(props as unknown as FlowGramMicroflowNativeCanvasProps)} />;
}
