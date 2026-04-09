export type JsonPrimitive = string | number | boolean | null;

export type JsonValue = JsonPrimitive | JsonObject | JsonValue[];

export interface JsonObject {
  [key: string]: JsonValue;
}

export type JsonSchemaType =
  | "string"
  | "number"
  | "integer"
  | "boolean"
  | "object"
  | "array"
  | "null";

export interface JsonSchemaNode {
  type?: JsonSchemaType | JsonSchemaType[];
  title?: string;
  description?: string;
  format?: string;
  enum?: JsonPrimitive[];
  default?: JsonValue;
  required?: string[];
  properties?: Record<string, JsonSchemaNode>;
  items?: JsonSchemaNode | JsonSchemaNode[];
  additionalProperties?: boolean | JsonSchemaNode;
}

export interface ProtocolField {
  key: string;
  name: string;
  description?: string;
  required: boolean;
  schema: JsonSchemaNode;
}

export interface ProtocolSchema {
  version: string;
  input: JsonSchemaNode;
  output?: JsonSchemaNode;
  fields?: ProtocolField[];
}

export interface ProtocolManifest {
  protocolKey: string;
  displayName: string;
  category?: string;
  version: string;
  schema: ProtocolSchema;
}
