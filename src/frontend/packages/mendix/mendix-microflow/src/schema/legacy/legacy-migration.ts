import { buildAuthoringFieldsFromLegacy, isLegacyGraphSchema } from "../../adapters/microflow-adapters";
import type {
  LegacyMicroflowGraphSchema,
  MicroflowAction,
  MicroflowAuthoringSchema,
  MicroflowExpression,
  MicroflowObjectCollection
} from "../types";

/** Current authoring JSON `schemaVersion` written after migration. */
export const CURRENT_AUTHORING_SCHEMA_VERSION = "1.0.0";

export function isLegacyMicroflowSchema(input: unknown): input is LegacyMicroflowGraphSchema {
  return isLegacyGraphSchema(input);
}

export function migrateLegacyMicroflowSchema(input: LegacyMicroflowGraphSchema): MicroflowAuthoringSchema {
  const migrated = buildAuthoringFieldsFromLegacy(input);
  return {
    ...migrated,
    schemaVersion: CURRENT_AUTHORING_SCHEMA_VERSION
  };
}

function isAuthoringShape(value: object): value is MicroflowAuthoringSchema {
  const o = value as Partial<MicroflowAuthoringSchema>;
  return (
    typeof o.schemaVersion === "string" &&
    o.objectCollection != null &&
    typeof o.objectCollection === "object" &&
    Array.isArray(o.flows) &&
    Array.isArray(o.parameters)
  );
}

function migrateP0ActionFields(action: MicroflowAction): void {
  if (action.kind === "changeVariable") {
    const a = action as unknown as Record<string, unknown>;
    if (typeof a.targetVariableName !== "string" && typeof a.variableName === "string") {
      a.targetVariableName = a.variableName;
      delete a.variableName;
    }
    if (a.newValueExpression == null && a.valueExpression != null) {
      a.newValueExpression = a.valueExpression;
      delete a.valueExpression;
    }
  }
  if (action.kind === "createVariable") {
    const a = action as unknown as Record<string, unknown>;
    if (a.readonly === undefined) {
      a.readonly = false;
    }
  }
  if (action.kind === "restCall") {
    const a = action as { request: { headers: { id?: string; key: string; valueExpression: MicroflowExpression }[]; queryParameters: { id?: string; key: string; valueExpression: MicroflowExpression }[] } };
    a.request.headers = a.request.headers.map((h, i) => (h.id ? h : { ...h, id: `hdr-mig-${i}` }));
    a.request.queryParameters = a.request.queryParameters.map((h, i) => (h.id ? h : { ...h, id: `q-mig-${i}` }));
  }
}

function walkObjectCollectionForP0(collection: MicroflowObjectCollection): void {
  for (const object of collection.objects) {
    if (object.kind === "actionActivity") {
      migrateP0ActionFields(object.action);
    }
    if (object.kind === "loopedActivity") {
      walkObjectCollectionForP0(object.objectCollection);
    }
  }
}

/**
 * Accepts {@link MicroflowAuthoringSchema} or {@link LegacyMicroflowGraphSchema} and returns authoring.
 * Unknown JSON falls back to a minimal blank authoring graph (with warning).
 */
export function normalizeMicroflowSchema(input: unknown): MicroflowAuthoringSchema {
  if (input == null || typeof input !== "object") {
    throw new Error("normalizeMicroflowSchema: expected an object");
  }
  if (isLegacyMicroflowSchema(input)) {
    return migrateLegacyMicroflowSchema(input);
  }
  if (isAuthoringShape(input)) {
    walkObjectCollectionForP0(input.objectCollection);
    return input;
  }
  globalThis.console?.warn?.("normalizeMicroflowSchema: unrecognized shape; using minimal blank authoring schema");
  return migrateLegacyMicroflowSchema({
    id: "blank",
    name: "Blank",
    version: "0.1.0",
    parameters: [],
    variables: [],
    nodes: [],
    edges: []
  });
}

export function assertAuthoringSchema(input: unknown): asserts input is MicroflowAuthoringSchema {
  if (input == null || typeof input !== "object") {
    throw new Error("assertAuthoringSchema: expected an object");
  }
  if (isLegacyMicroflowSchema(input)) {
    throw new Error("assertAuthoringSchema: value is a legacy graph schema; call normalizeMicroflowSchema first");
  }
  if (!isAuthoringShape(input)) {
    throw new Error("assertAuthoringSchema: missing schemaVersion, objectCollection, flows, or parameters");
  }
}
