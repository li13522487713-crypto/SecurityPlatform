export type DynamicFieldType =
  | "String"
  | "Text"
  | "Int"
  | "Long"
  | "Decimal"
  | "Boolean"
  | "DateTime"
  | "Date"
  | "Time"
  | "Enum"
  | "File"
  | "Image"
  | "Json"
  | "Guid";

interface AmisFieldSchema {
  type: string;
  name: string;
  label: string;
  [key: string]: unknown;
}

interface AmisColumnSchema {
  type?: string;
  name: string;
  label: string;
  sortable?: boolean;
  [key: string]: unknown;
}

const formFieldMap: Record<DynamicFieldType, (name: string, label: string) => AmisFieldSchema> = {
  String: (name, label) => ({ type: "input-text", name, label }),
  Text: (name, label) => ({ type: "textarea", name, label, minRows: 3 }),
  Int: (name, label) => ({ type: "input-number", name, label, precision: 0 }),
  Long: (name, label) => ({ type: "input-number", name, label, precision: 0 }),
  Decimal: (name, label) => ({ type: "input-number", name, label, precision: 2, step: 0.01 }),
  Boolean: (name, label) => ({ type: "switch", name, label }),
  DateTime: (name, label) => ({ type: "input-datetime", name, label, format: "YYYY-MM-DD HH:mm:ss" }),
  Date: (name, label) => ({ type: "input-date", name, label, format: "YYYY-MM-DD" }),
  Time: (name, label) => ({ type: "input-time", name, label, format: "HH:mm:ss" }),
  Enum: (name, label) => ({ type: "select", name, label, options: [] }),
  File: (name, label) => ({
    type: "input-file",
    name,
    label,
    receiver: "/api/v1/files",
    startChunkApi: "/api/v1/files/upload/init",
    chunkApi: "/api/v1/files/upload/${sessionId}/part/${partNumber}",
    finishChunkApi: "/api/v1/files/upload/${sessionId}/complete"
  }),
  Image: (name, label) => ({
    type: "input-image",
    name,
    label,
    receiver: "/api/v1/files",
    startChunkApi: "/api/v1/files/images/apply",
    chunkApi: "/api/v1/files/upload/${sessionId}/part/${partNumber}",
    finishChunkApi: "/api/v1/files/images/commit"
  }),
  Json: (name, label) => ({ type: "editor", name, label, language: "json" }),
  Guid: (name, label) => ({ type: "input-text", name, label, readOnly: true }),
};

const tableColumnMap: Record<DynamicFieldType, (name: string, label: string) => AmisColumnSchema> = {
  String: (name, label) => ({ name, label, sortable: true }),
  Text: (name, label) => ({ name, label, type: "tpl", tpl: "${" + name + "|truncate:50}" }),
  Int: (name, label) => ({ name, label, sortable: true }),
  Long: (name, label) => ({ name, label, sortable: true }),
  Decimal: (name, label) => ({ name, label, sortable: true }),
  Boolean: (name, label) => ({ name, label, type: "status" }),
  DateTime: (name, label) => ({ name, label, type: "datetime", sortable: true }),
  Date: (name, label) => ({ name, label, type: "date", sortable: true }),
  Time: (name, label) => ({ name, label }),
  Enum: (name, label) => ({ name, label, type: "mapping", map: {} }),
  File: (name, label) => ({ name, label, type: "link", body: "Download" }),
  Image: (name, label) => ({ name, label, type: "image", thumbMode: "cover", thumbRatio: "1:1" }),
  Json: (name, label) => ({ name, label, type: "json" }),
  Guid: (name, label) => ({ name, label }),
};

export interface FieldDefinition {
  name: string;
  displayName: string;
  fieldType: DynamicFieldType;
  isPrimaryKey?: boolean;
}

export function mapFieldToFormSchema(field: FieldDefinition): AmisFieldSchema {
  const mapper = formFieldMap[field.fieldType] ?? formFieldMap.String;
  return mapper(field.name, field.displayName);
}

export function mapFieldToColumnSchema(field: FieldDefinition): AmisColumnSchema {
  const mapper = tableColumnMap[field.fieldType] ?? tableColumnMap.String;
  return mapper(field.name, field.displayName);
}

export function generateFormSchema(
  fields: FieldDefinition[],
  title: string,
  apiBase: string,
): Record<string, unknown> {
  const formFields = fields
    .filter((f) => !f.isPrimaryKey)
    .map((f) => mapFieldToFormSchema(f));

  return {
    type: "page",
    title,
    body: [
      {
        type: "form",
        api: `post:${apiBase}`,
        body: formFields,
      },
    ],
  };
}

export function generateCrudSchema(
  fields: FieldDefinition[],
  title: string,
  apiBase: string,
): Record<string, unknown> {
  const columns = fields.map((f) => mapFieldToColumnSchema(f));

  return {
    type: "page",
    title,
    body: [
      {
        type: "crud",
        api: apiBase,
        columns,
        bulkActions: [],
        itemActions: [
          {
            type: "button",
            label: "Edit",
            actionType: "dialog",
            dialog: {
              title: "Edit",
              body: {
                type: "form",
                api: `put:${apiBase}/\${id}`,
                body: fields
                  .filter((f) => !f.isPrimaryKey)
                  .map((f) => mapFieldToFormSchema(f)),
              },
            },
          },
        ],
      },
    ],
  };
}
