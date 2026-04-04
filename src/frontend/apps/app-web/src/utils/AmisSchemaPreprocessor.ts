import { parseMustache } from "./ExpressionEngine";

/**
 * AMIS Schema 预处理：对 $vars 与 {{}} 模板求值（与 Atlas.WebApp 行为一致）。
 */
export class AmisSchemaPreprocessor {
  static process(schema: unknown, context: Record<string, unknown> = {}): unknown {
    if (!schema) return schema;

    if (typeof schema === "string") {
      return parseMustache(schema, context);
    }

    if (Array.isArray(schema)) {
      return schema.map((item) => this.process(item, context));
    }

    if (typeof schema === "object") {
      const obj = schema as Record<string, unknown>;
      let currentContext = { ...context };
      if (obj.$vars && typeof obj.$vars === "object" && obj.$vars !== null && !Array.isArray(obj.$vars)) {
        currentContext = { ...currentContext, ...(obj.$vars as Record<string, unknown>) };
        delete obj.$vars;
      }

      const result: Record<string, unknown> = {};
      for (const [key, value] of Object.entries(obj)) {
        result[key] = this.process(value, currentContext);
      }
      return result;
    }

    return schema;
  }
}
