import type { MicroflowEditorGraphPatch, MicroflowSchema } from "../schema/types";
import { createBusinessAutoLayoutPatch } from "../layout";

export function createAutoLayoutPatch(schema: MicroflowSchema): MicroflowEditorGraphPatch {
  return createBusinessAutoLayoutPatch({ schema });
}
