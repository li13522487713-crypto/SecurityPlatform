interface JsonSchemaObject {
  type?: string | string[];
  required?: string[];
  properties?: Record<string, JsonSchemaObject>;
  enum?: unknown[];
  minimum?: number;
  maximum?: number;
  minLength?: number;
  maxLength?: number;
  pattern?: string;
  items?: JsonSchemaObject;
  default?: unknown;
}

export interface SchemaValidationIssue {
  path: string;
  message: string;
}

export interface SchemaValidationResult {
  issues: SchemaValidationIssue[];
  parsedSchema?: JsonSchemaObject;
}

export function parseConfigSchema(schemaJson: string | undefined): JsonSchemaObject | undefined {
  if (!schemaJson) {
    return undefined;
  }
  try {
    const parsed = JSON.parse(schemaJson) as JsonSchemaObject;
    if (!parsed || typeof parsed !== "object") {
      return undefined;
    }
    if (parsed.properties && typeof parsed.properties === "object") {
      return parsed;
    }
    if (parsed.type === "object") {
      return parsed;
    }
    return parsed;
  } catch {
    return undefined;
  }
}

function isEmptyValue(value: unknown): boolean {
  return value === undefined || value === null || (typeof value === "string" && value.trim().length === 0);
}

function isTypeMatched(value: unknown, type: string): boolean {
  if (type === "string") {
    return typeof value === "string";
  }
  if (type === "number" || type === "integer") {
    return typeof value === "number" && !Number.isNaN(value);
  }
  if (type === "boolean") {
    return typeof value === "boolean";
  }
  if (type === "array") {
    return Array.isArray(value);
  }
  if (type === "object") {
    return Boolean(value) && typeof value === "object" && !Array.isArray(value);
  }
  if (type === "null") {
    return value === null;
  }
  return true;
}

function normalizeSchemaType(type: string | string[] | undefined): string[] {
  if (!type) {
    return [];
  }
  if (Array.isArray(type)) {
    return type;
  }
  return [type];
}

function validateWithSchema(
  value: unknown,
  schema: JsonSchemaObject,
  path: string,
  issues: SchemaValidationIssue[]
) {
  const expectedTypes = normalizeSchemaType(schema.type);
  if (expectedTypes.length > 0 && !expectedTypes.some((type) => isTypeMatched(value, type))) {
    issues.push({ path, message: `字段类型应为 ${expectedTypes.join(" | ")}。` });
    return;
  }

  if (schema.enum && schema.enum.length > 0 && value !== undefined && !schema.enum.some((enumValue) => enumValue === value)) {
    issues.push({ path, message: "字段值不在允许范围内。" });
  }

  if (typeof value === "string") {
    if (typeof schema.minLength === "number" && value.length < schema.minLength) {
      issues.push({ path, message: `长度不能小于 ${schema.minLength}。` });
    }
    if (typeof schema.maxLength === "number" && value.length > schema.maxLength) {
      issues.push({ path, message: `长度不能大于 ${schema.maxLength}。` });
    }
    if (schema.pattern) {
      try {
        const pattern = new RegExp(schema.pattern);
        if (!pattern.test(value)) {
          issues.push({ path, message: "字段格式不正确。" });
        }
      } catch {
        // ignore invalid schema pattern
      }
    }
  }

  if (typeof value === "number") {
    if (typeof schema.minimum === "number" && value < schema.minimum) {
      issues.push({ path, message: `值不能小于 ${schema.minimum}。` });
    }
    if (typeof schema.maximum === "number" && value > schema.maximum) {
      issues.push({ path, message: `值不能大于 ${schema.maximum}。` });
    }
    if (schema.type === "integer" && !Number.isInteger(value)) {
      issues.push({ path, message: "字段必须为整数。" });
    }
  }

  if (Array.isArray(value) && schema.items) {
    value.forEach((item, index) => validateWithSchema(item, schema.items as JsonSchemaObject, `${path}[${index}]`, issues));
  }

  if (value && typeof value === "object" && !Array.isArray(value) && schema.properties) {
    const record = value as Record<string, unknown>;
    if (schema.required) {
      for (const requiredKey of schema.required) {
        if (isEmptyValue(record[requiredKey])) {
          issues.push({ path: path ? `${path}.${requiredKey}` : requiredKey, message: "必填字段不能为空。" });
        }
      }
    }
    for (const [key, subSchema] of Object.entries(schema.properties)) {
      if (record[key] === undefined) {
        continue;
      }
      validateWithSchema(record[key], subSchema, path ? `${path}.${key}` : key, issues);
    }
  }
}

export function validateConfigBySchema(config: Record<string, unknown>, schemaJson: string | undefined): SchemaValidationResult {
  const schema = parseConfigSchema(schemaJson);
  if (!schema) {
    return { issues: [] };
  }
  const issues: SchemaValidationIssue[] = [];
  validateWithSchema(config, schema, "", issues);
  return { issues, parsedSchema: schema };
}
