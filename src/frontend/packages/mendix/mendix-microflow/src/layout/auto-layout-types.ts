import type { MicroflowEditorGraphPatch, MicroflowSchema } from "../schema/types";

export interface MicroflowAutoLayoutOptions {
  direction?: "LR" | "TB";
  layerGap?: number;
  nodeGap?: number;
  includeAnnotations?: boolean;
  fitViewAfterLayout?: boolean;
}

export interface MicroflowAutoLayoutInput {
  schema: MicroflowSchema;
  collectionId?: string;
  options?: MicroflowAutoLayoutOptions;
}

export interface MicroflowLayoutBounds {
  minX: number;
  minY: number;
  maxX: number;
  maxY: number;
  width: number;
  height: number;
}

export interface MicroflowAutoLayoutResult {
  nextSchema: MicroflowSchema;
  patch: MicroflowEditorGraphPatch;
  changedObjectIds: string[];
  bounds: MicroflowLayoutBounds;
}
