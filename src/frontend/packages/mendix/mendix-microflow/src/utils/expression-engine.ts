import type { MicroflowSchema, MicroflowMetadataCatalog, MicroflowVariableIndex } from "../schema/types";
import type { ExpressionDiagnostic } from "../expressions/expression-types";
import { validateExpression } from "../expressions/expression-validator";
import { parseExpressionReferences } from "../expressions/expression-reference-parser";
import { tokenizeExpression } from "../expressions/expression-tokenizer";
import { buildVariableIndex, resolveVariableReferenceFromIndex } from "../variables";
import { EMPTY_MICROFLOW_METADATA_CATALOG } from "../metadata/metadata-catalog";
import { MENDIX_FUNCTIONS, type MicroflowVariable, type MendixDataType, toMendixDataType } from "../types/mendix-types";

export interface ExpressionParseSuggestion {
  label: string;
  type: "variable" | "attribute" | "function";
  detail: string;
  insertText: string;
}

export interface ExpressionParseResult {
  valid: boolean;
  type: MendixDataType;
  errorMessage?: string;
  usedVariables: string[];
  suggestions: ExpressionParseSuggestion[];
  diagnostics: ExpressionDiagnostic[];
}

export interface ExpressionParseContext {
  schema?: MicroflowSchema;
  metadata?: MicroflowMetadataCatalog;
  variableIndex?: MicroflowVariableIndex;
  objectId?: string;
  actionId?: string;
  fieldPath?: string;
}

export const tokenize = tokenizeExpression;

function normalizeVariableName(name: string): string {
  return name.startsWith("$") ? name.slice(1) : name;
}

function normalizeAvailableVariables(variables: MicroflowVariable[]): Map<string, MicroflowVariable> {
  const map = new Map<string, MicroflowVariable>();
  for (const variable of variables) {
    map.set(normalizeVariableName(variable.name), variable);
    map.set(`$${normalizeVariableName(variable.name)}`, variable);
  }
  return map;
}

function normalizeReferenceName(variableName: string): string {
  return normalizeVariableName(variableName.split("/")[0] ?? variableName);
}

function toInvalidDivisionDiagnostic(diagnostic: ExpressionDiagnostic): ExpressionDiagnostic {
  return {
    ...diagnostic,
    id: "MF_EXPR_INVALID_DIVISION",
    code: "MF_EXPR_INVALID_DIVISION",
    message: "除法请使用 div 而不是 /。",
    variableName: diagnostic.variableName,
  };
}

export function getAutoCompleteSuggestions(
  expr: string,
  cursorPos: number,
  availableVars: MicroflowVariable[],
): ExpressionParseSuggestion[] {
  const prefix = expr.slice(0, cursorPos);
  const byName = normalizeAvailableVariables(availableVars);

  const variableMatch = prefix.match(/\$([A-Za-z_][\w]*)?$/);
  if (variableMatch && (variableMatch[1] ?? "").length >= 0) {
    if (prefix.endsWith("/")) {
      const varName = normalizeVariableName(prefix.slice(0, -1).split("$").pop() ?? "");
      const variable = byName.get(varName) ?? byName.get(`$${varName}`);
      if (variable?.attributes?.length) {
        return variable.attributes.map(attribute => ({
          label: attribute,
          type: "attribute",
          detail: "attribute",
          insertText: attribute,
        }));
      }
    }
    return availableVars
      .filter(v => variableMatch[1] ? normalizeVariableName(v.name).toLowerCase().includes((variableMatch[1] ?? "").toLowerCase()) : true)
      .map(variable => ({
        label: normalizeVariableName(variable.name),
        type: "variable",
        detail: variable.type,
        insertText: normalizeVariableName(variable.name),
      }));
  }

  const objectMatch = prefix.match(/\$([A-Za-z_][\w]*)\/([A-Za-z0-9_]*)$/);
  if (objectMatch) {
    const variableName = normalizeVariableName(objectMatch[1]);
    const partial = objectMatch[2] ?? "";
    const variable = byName.get(variableName) ?? byName.get(`$${variableName}`);
    return (variable?.attributes ?? [])
      .filter(attribute => attribute.toLowerCase().startsWith(partial.toLowerCase()))
      .map(attribute => ({
        label: attribute,
        type: "attribute" as const,
        detail: "attribute",
        insertText: attribute,
      }));
  }

  const funcMatch = prefix.match(/([A-Za-z_][\w]*)$/);
  if (funcMatch) {
    return MENDIX_FUNCTIONS
      .filter(fn => fn.name.toLowerCase().startsWith(funcMatch[1].toLowerCase()))
      .map(fn => ({
        label: fn.name,
        type: "function",
        detail: fn.signature,
        insertText: fn.name,
      }));
  }

  return [];
}

export function parseExpression(
  expression: string,
  availableVars: MicroflowVariable[],
  context?: ExpressionParseContext,
): ExpressionParseResult {
  const raw = String(expression ?? "").trim();
  const parsed = parseExpressionReferences(raw);
  const availableByName = normalizeAvailableVariables(availableVars);
  const used = new Set<string>();
  const unknown = new Set<string>();
  const diagnostics: ExpressionDiagnostic[] = [
    ...parsed.diagnostics.filter(item => item.code !== "MF_EXPR_USE_DIV_OPERATOR").map(item => ({ ...item })),
    ...parsed.diagnostics
      .filter(item => item.code === "MF_EXPR_USE_DIV_OPERATOR")
      .map(toInvalidDivisionDiagnostic),
  ];

  for (const reference of parsed.references) {
    if (reference.kind === "variable" || reference.kind === "memberAccess") {
      const name = normalizeReferenceName(reference.variableName);
      used.add(name);
      const exists = availableByName.has(name) || availableByName.has(`$${name}`);
      if (!exists) {
        diagnostics.push({
          id: `MF_EXPR_UNKNOWN_VARIABLE:${name}`,
          code: "MF_EXPR_UNKNOWN_VARIABLE",
          message: `Variable "$${name}" is not available in this context.`,
          severity: "error",
          range: reference.range,
          variableName: name,
        });
        unknown.add(name);
      }
    }
  }

  let inferredType = "Empty" as MendixDataType;
  let suggestions = getAutoCompleteSuggestions(raw, raw.length, availableVars);

  if (context?.schema && (context.metadata ?? EMPTY_MICROFLOW_METADATA_CATALOG) && (context.objectId || context.actionId)) {
    const variableIndex = context.variableIndex
      ?? buildVariableIndex(context.schema, context.metadata ?? EMPTY_MICROFLOW_METADATA_CATALOG);
    const validation = validateExpression({
      expression: raw,
      schema: context.schema,
      metadata: context.metadata ?? EMPTY_MICROFLOW_METADATA_CATALOG,
      variableIndex,
      context: {
        objectId: context.objectId,
        actionId: context.actionId,
        fieldPath: context.fieldPath,
      },
    });
    const referenceByContext = validation.references
      .map(reference => (reference.kind === "variable" || reference.kind === "memberAccess") ? reference.variableName : undefined)
      .filter((name): name is string => Boolean(name))
      .map(name => normalizeReferenceName(name));
    for (const name of new Set(referenceByContext)) {
      used.add(name);
      const symbol = resolveVariableReferenceFromIndex(
        context.schema,
        variableIndex,
        { objectId: context.objectId, actionId: context.actionId, fieldPath: context.fieldPath },
        name,
      );
      if (symbol) {
        inferredType = toMendixDataType(symbol.dataType);
      }
    }
    diagnostics.push(...validation.diagnostics);
    if (!validation.diagnostics.some(item => item.severity === "error")) {
      inferredType = toMendixDataType(validation.inferredType);
    }
    if (context.objectId && !parsed.references.some(ref => ref.range.end === raw.length)) {
      // keep default, keep existing behavior
      suggestions = getAutoCompleteSuggestions(raw, raw.length, availableVars);
    }
  }

  const allErrors = diagnostics.filter(item => item.severity === "error");
  const valid = allErrors.length === 0 && unknown.size === 0;
  return {
    valid,
    type: inferredType,
    errorMessage: allErrors[0]?.message,
    usedVariables: [...used],
    suggestions,
    diagnostics,
  };
}
