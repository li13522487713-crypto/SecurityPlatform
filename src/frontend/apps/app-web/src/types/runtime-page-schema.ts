/** 应用运行端页面 schema 所需的最小类型（与后端契约对齐）。 */

interface RuntimeManifestRefs {
  lifecycle?: Record<string, JsonValue>;
  actionRegistry?: Record<string, JsonValue>;
  bindingRegistry?: Record<string, JsonValue>;
  initialContextPatch?: Record<string, JsonValue>;
}

type JsonValue = string | number | boolean | null | JsonObject | JsonArray;
type JsonObject = { [key: string]: JsonValue };
type JsonArray = JsonValue[];

export interface RuntimePageSchema {
  pageId: string;
  pageKey: string;
  name: string;
  schemaJson: string;
  version: number;
  mode: string;
  lifecycle?: RuntimeManifestRefs["lifecycle"];
  actionRegistry?: RuntimeManifestRefs["actionRegistry"];
  bindingRegistry?: RuntimeManifestRefs["bindingRegistry"];
  initialContextPatch?: RuntimeManifestRefs["initialContextPatch"];
}
