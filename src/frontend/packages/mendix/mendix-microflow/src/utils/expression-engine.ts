import type { MicroflowSchema, MicroflowVariableIndex } from "../schema/types";
import type { ExpressionDiagnostic } from "../expressions/expression-types";
import { validateExpression } from "../expressions/expression-validator";
import { parseExpressionReferences } from "../expressions/expression-reference-parser";
import { tokenizeExpression } from "../expressions/expression-tokenizer";
import { buildVariableIndex, resolveVariableReferenceFromIndex } from "../variables";
import { EMPTY_MICROFLOW_METADATA_CATALOG } from "../metadata/metadata-catalog";
import type { MicroflowMetadataCatalog } from "../metadata/metadata-catalog";
import { getAssociationsForEntity, getEntityByQualifiedName, getTargetEntityByAssociation, resolveStoredEntityQualifiedName } from "../metadata";
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
const BUILTIN_SYSTEM_VARIABLES: readonly MicroflowVariable[] = [
  { name: "$currentUser", type: "Object", entityType: "System.User" },
  { name: "$currentSession", type: "Object", entityType: "System.Session" },
];

function normalizeVariableName(name: string): string {
  return name.startsWith("$") ? name.slice(1) : name;
}

function withBuiltinSystemVariables(variables: MicroflowVariable[]): MicroflowVariable[] {
  const names = new Set(variables.map(item => normalizeVariableName(item.name).toLowerCase()));
  const merged = [...variables];
  for (const builtin of BUILTIN_SYSTEM_VARIABLES) {
    const key = normalizeVariableName(builtin.name).toLowerCase();
    if (!names.has(key)) {
      merged.push(builtin);
    }
  }
  return merged;
}

function normalizeAvailableVariables(variables: MicroflowVariable[]): Map<string, MicroflowVariable> {
  const map = new Map<string, MicroflowVariable>();
  for (const variable of withBuiltinSystemVariables(variables)) {
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

function collectObjectMemberSuggestions(
  metadata: MicroflowMetadataCatalog,
  entityQualifiedName: string | undefined,
  path: string,
): string[] {
  const resolvedEntityName = resolveStoredEntityQualifiedName(metadata, entityQualifiedName);
  if (!resolvedEntityName) {
    return [];
  }
  const trimmedPath = path.trim();
  const segments = trimmedPath.split("/").filter(Boolean);
  const withTrailingSlash = trimmedPath.endsWith("/");

  const partial = withTrailingSlash
    ? ""
    : (segments.length ? segments[segments.length - 1] : "");
  const associationChain = withTrailingSlash
    ? segments
    : (segments.length ? segments.slice(0, -1) : []);

  let currentEntityName = resolvedEntityName;
  for (const segment of associationChain) {
    const candidates = getAssociationsForEntity(metadata, currentEntityName);
    const segmentLower = segment.toLowerCase();
    const matched = candidates.find(association =>
      association.qualifiedName === segment ||
      association.qualifiedName.toLowerCase().endsWith(`.${segmentLower}`) ||
      association.qualifiedName.toLowerCase() === segmentLower
    );
    if (!matched) {
      return [];
    }
    const target = getTargetEntityByAssociation(metadata, matched.qualifiedName, currentEntityName);
    if (!target) {
      return [];
    }
    currentEntityName = target.qualifiedName;
  }

  const entity = getEntityByQualifiedName(metadata, currentEntityName);
  if (!entity) {
    return [];
  }
  const tokens = new Set<string>([
    ...entity.attributes.map(attribute => attribute.name),
    ...getAssociationsForEntity(metadata, currentEntityName).map(association => association.name),
  ]);
  const normalizedPartial = partial.toLowerCase();
  return [...tokens]
    .filter(token => token.toLowerCase().startsWith(normalizedPartial))
    .sort((left, right) => left.localeCompare(right));
}

function getMetadataSuggestions(
  rawExpr: string,
  availableVars: MicroflowVariable[],
  metadata?: MicroflowMetadataCatalog,
): ExpressionParseSuggestion[] {
  if (!metadata) {
    return [];
  }
  const byName = normalizeAvailableVariables(availableVars);
  const objectMatch = rawExpr.match(/\$([A-Za-z_][\w]*)(\/[A-Za-z0-9_.]*)*$/);
  if (!objectMatch) {
    return [];
  }

  const raw = objectMatch[0];
  const slashIndex = raw.indexOf("/");
  if (slashIndex < 0) {
    return [];
  }

  const variableName = normalizeVariableName(raw.slice(1, slashIndex));
  const path = raw.slice(slashIndex + 1);
  const variable = byName.get(variableName) ?? byName.get(`$${variableName}`);
  const entityType = variable?.entityType;
  if (!entityType) {
    return [];
  }

  const members = collectObjectMemberSuggestions(metadata, entityType, path);
  if (!members.length) {
    return [];
  }
  return members.map(attribute => ({
    label: attribute,
    type: "attribute",
    detail: "attribute",
    insertText: attribute,
  }));
}

export function getAutoCompleteSuggestions(
  expr: string,
  cursorPos: number,
  availableVars: MicroflowVariable[],
  metadata?: MicroflowMetadataCatalog,
): ExpressionParseSuggestion[] {
  const allVariables = withBuiltinSystemVariables(availableVars);
  const prefix = expr.slice(0, cursorPos);
  const byName = normalizeAvailableVariables(allVariables);
  const metadataSuggestions = getMetadataSuggestions(prefix, allVariables, metadata);
  if (metadataSuggestions.length) {
    return metadataSuggestions;
  }

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
    return allVariables
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
  const allVariables = withBuiltinSystemVariables(availableVars);
  const raw = String(expression ?? "").trim();
  const parsed = parseExpressionReferences(raw);
  const availableByName = normalizeAvailableVariables(allVariables);
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
  let suggestions = getAutoCompleteSuggestions(
    raw,
    raw.length,
    allVariables,
    context?.metadata ?? EMPTY_MICROFLOW_METADATA_CATALOG,
  );

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
      const objectId = context.objectId;
      if (!objectId) {
        continue;
      }
      const symbol = resolveVariableReferenceFromIndex(
        context.schema,
        variableIndex,
        { objectId, actionId: context.actionId, fieldPath: context.fieldPath },
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
      suggestions = getAutoCompleteSuggestions(
        raw,
        raw.length,
        allVariables,
        context?.metadata ?? EMPTY_MICROFLOW_METADATA_CATALOG,
      );
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
