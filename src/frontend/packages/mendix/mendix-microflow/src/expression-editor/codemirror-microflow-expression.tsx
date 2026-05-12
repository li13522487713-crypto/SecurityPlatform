import { autocompletion, type CompletionContext } from "@codemirror/autocomplete";
import { defaultKeymap, history, historyKeymap } from "@codemirror/commands";
import { linter, type Diagnostic as CodeMirrorDiagnostic } from "@codemirror/lint";
import { Compartment, EditorState, RangeSetBuilder } from "@codemirror/state";
import { Decoration, EditorView, ViewPlugin, hoverTooltip, keymap, lineNumbers, type DecorationSet, type ViewUpdate } from "@codemirror/view";
import { useEffect, useMemo, useRef } from "react";

import { expressionRaw, expressionTypeLabel, validateExpression } from "../expressions";
import { getAssociationsForEntity, getEntityAttributes, getEnumerationValues, getTargetEntityByAssociation, type MicroflowMetadataCatalog } from "../metadata";
import type { MicroflowAuthoringSchema, MicroflowDataType, MicroflowExpression, MicroflowVariableIndex } from "../schema";
import { buildVariableUsageMetrics, getVariablesForExpressionFromIndex, variableSourceLabel, type MicroflowExpressionScopeContext } from "../variables";
import { tokenizeExpression, type MicroflowExpressionToken } from "../expressions/expression-tokenizer";
import type { ExpressionDiagnostic } from "../expressions/expression-types";

export interface CodemirrorMicroflowExpressionProps {
  value: string;
  onChange: (next: string) => void;
  readonly?: boolean;
  minRows?: number;
  placeholder?: string;
  schema?: MicroflowAuthoringSchema;
  metadata?: MicroflowMetadataCatalog;
  variableIndex?: MicroflowVariableIndex;
  objectId?: string;
  actionId?: string;
  flowId?: string;
  fieldPath?: string;
  expectedType?: MicroflowDataType;
  required?: boolean;
  completionOptions?: Array<{ label: string; value: string; detail?: string; disabled?: boolean }>;
  diagnostics?: ExpressionDiagnostic[];
}

function tokenClassName(token: MicroflowExpressionToken): string | undefined {
  switch (token.kind) {
    case "variable":
      return "cm-mf-variable";
    case "string":
      return "cm-mf-string";
    case "number":
    case "boolean":
    case "null":
      return "cm-mf-literal";
    case "keyword":
      return "cm-mf-keyword";
    case "operator":
    case "slash":
      return "cm-mf-operator";
    case "unknown":
      return "cm-mf-error";
    default:
      return undefined;
  }
}

function buildTokenDecorations(view: EditorView): DecorationSet {
  const builder = new RangeSetBuilder<Decoration>();
  for (const token of tokenizeExpression(view.state.doc.toString()).tokens) {
    const className = tokenClassName(token);
    if (className && token.range.end > token.range.start) {
      builder.add(token.range.start, token.range.end, Decoration.mark({ class: className }));
    }
  }
  return builder.finish();
}

const microflowExpressionHighlight = ViewPlugin.fromClass(class {
  decorations: DecorationSet;

  constructor(view: EditorView) {
    this.decorations = buildTokenDecorations(view);
  }

  update(update: ViewUpdate) {
    if (update.docChanged) {
      this.decorations = buildTokenDecorations(update.view);
    }
  }
}, {
  decorations: plugin => plugin.decorations,
});

function toCodeMirrorDiagnostic(diagnostic: ExpressionDiagnostic): CodeMirrorDiagnostic {
  const start = Math.max(0, diagnostic.range?.start ?? 0);
  const end = Math.max(start, diagnostic.range?.end ?? start + 1);
  return {
    from: start,
    to: end,
    severity: diagnostic.severity === "warning" ? "warning" : "error",
    message: `${diagnostic.code}: ${diagnostic.message}`,
  };
}

function wordAt(doc: string, pos: number): { from: number; to: number; text: string } {
  let from = pos;
  let to = pos;
  while (from > 0 && /[$A-Za-z0-9_./]/.test(doc[from - 1])) {
    from -= 1;
  }
  while (to < doc.length && /[$A-Za-z0-9_./]/.test(doc[to])) {
    to += 1;
  }
  return { from, to, text: doc.slice(from, to) };
}

function createCompletions(options: CodemirrorMicroflowExpressionProps["completionOptions"]) {
  return (context: CompletionContext) => {
    const word = context.matchBefore(/[$A-Za-z0-9_./]*/);
    if (!word || (word.from === word.to && !context.explicit)) {
      return null;
    }
    const query = word.text.toLowerCase();
    return {
      from: word.from,
      options: (options ?? [])
        .filter(option => !option.disabled && (!query || option.value.toLowerCase().includes(query) || option.label.toLowerCase().includes(query)))
        .slice(0, 50)
        .map(option => ({
          label: option.value,
          detail: option.detail ?? option.label,
          apply: option.value,
          type: option.value.startsWith("$") ? "variable" : option.value.includes("(") ? "function" : "constant",
        })),
    };
  };
}

function createHover(options: CodemirrorMicroflowExpressionProps["completionOptions"]) {
  return hoverTooltip((view, pos) => {
    const doc = view.state.doc.toString();
    const word = wordAt(doc, pos);
    if (!word.text) {
      return null;
    }
    const match = (options ?? []).find(option => option.value === word.text);
    if (!match?.detail) {
      return null;
    }
    return {
      pos: word.from,
      end: word.to,
      create: () => {
        const dom = document.createElement("div");
        dom.className = "cm-mf-hover";
        dom.textContent = match.detail ?? null;
        return { dom };
      },
    };
  });
}

export default function CodemirrorMicroflowExpression({
  value,
  onChange,
  readonly,
  minRows = 2,
  placeholder,
  completionOptions,
  diagnostics,
}: CodemirrorMicroflowExpressionProps) {
  const parentRef = useRef<HTMLDivElement>(null);
  const viewRef = useRef<EditorView | null>(null);
  const readOnlyConf = useRef(new Compartment());
  const completionConf = useRef(new Compartment());
  const lintConf = useRef(new Compartment());
  const onChangeRef = useRef(onChange);
  const completionSource = useMemo(() => createCompletions(completionOptions), [completionOptions]);
  const hover = useMemo(() => createHover(completionOptions), [completionOptions]);
  const currentDiagnostics = useRef<ExpressionDiagnostic[]>(diagnostics ?? []);
  onChangeRef.current = onChange;
  currentDiagnostics.current = diagnostics ?? [];

  useEffect(() => {
    const parent = parentRef.current;
    if (!parent) {
      return;
    }

    const state = EditorState.create({
      doc: value,
      extensions: [
        lineNumbers(),
        history(),
        keymap.of([...defaultKeymap, ...historyKeymap]),
        EditorView.lineWrapping,
        microflowExpressionHighlight,
        hover,
        completionConf.current.of(autocompletion({ override: [completionSource] })),
        lintConf.current.of(linter(() => currentDiagnostics.current.map(toCodeMirrorDiagnostic))),
        readOnlyConf.current.of(EditorState.readOnly.of(!!readonly)),
        EditorView.contentAttributes.of({
          "aria-label": placeholder ?? "Microflow expression",
        }),
        EditorView.updateListener.of(update => {
          if (update.docChanged) {
            onChangeRef.current(update.state.doc.toString());
          }
        }),
        EditorView.theme({
          "&": { minHeight: `${Math.max(2, minRows) * 22}px` },
          ".cm-scroller": { fontFamily: "inherit", fontSize: "13px" },
          ".cm-content": { paddingBlock: "6px" },
          ".cm-mf-variable": { color: "var(--semi-color-primary)" },
          ".cm-mf-keyword": { color: "var(--semi-color-warning)", fontWeight: "600" },
          ".cm-mf-string": { color: "var(--semi-color-success)" },
          ".cm-mf-literal": { color: "var(--semi-color-secondary)" },
          ".cm-mf-operator": { color: "var(--semi-color-tertiary)" },
          ".cm-mf-error": { color: "var(--semi-color-danger)", textDecoration: "underline wavy var(--semi-color-danger)" },
          ".cm-mf-hover": { padding: "4px 6px", fontSize: "12px" },
        }),
      ],
    });

    const view = new EditorView({ state, parent });
    viewRef.current = view;
    return () => {
      view.destroy();
      viewRef.current = null;
    };
     
  }, [completionSource, hover, minRows, placeholder]);

  useEffect(() => {
    const view = viewRef.current;
    if (!view) {
      return;
    }
    const cur = view.state.doc.toString();
    if (cur !== value) {
      view.dispatch({ changes: { from: 0, to: cur.length, insert: value } });
    }
  }, [value]);

  useEffect(() => {
    const view = viewRef.current;
    if (!view) {
      return;
    }
    view.dispatch({
      effects: readOnlyConf.current.reconfigure(EditorState.readOnly.of(!!readonly)),
    });
  }, [readonly]);

  useEffect(() => {
    const view = viewRef.current;
    if (!view) {
      return;
    }
    view.dispatch({
      effects: [
        completionConf.current.reconfigure(autocompletion({ override: [completionSource] })),
        lintConf.current.reconfigure(linter(() => currentDiagnostics.current.map(toCodeMirrorDiagnostic))),
      ],
    });
  }, [completionSource, diagnostics]);

  return <div ref={parentRef} className="microflow-expression-codemirror-host" />;
}

export function buildMicroflowExpressionCompletionOptions(input: {
  schema: MicroflowAuthoringSchema;
  metadata: MicroflowMetadataCatalog;
  variableIndex: MicroflowVariableIndex;
  objectId?: string;
  actionId?: string;
  fieldPath?: string;
  expectedType?: MicroflowDataType;
}): Array<{ label: string; value: string; detail?: string; disabled?: boolean }> {
  const variableToken = (name: string) => name.startsWith("$") ? name : `$${name}`;
  const jsonVariableToken = (name: string) => name.startsWith("$") ? `$.${name.slice(1)}` : `$.${name}`;
  const context: MicroflowExpressionScopeContext = { objectId: input.objectId ?? "", actionId: input.actionId, fieldPath: input.fieldPath };
  const variableMetrics = buildVariableUsageMetrics({ schema: input.schema, variableIndex: input.variableIndex, objectId: input.objectId });
  const variables = (input.objectId ? getVariablesForExpressionFromIndex(input.schema, input.variableIndex, context) : [])
    .sort((left, right) => {
      const leftCount = variableMetrics[left.name]?.referenceCount ?? 0;
      const rightCount = variableMetrics[right.name]?.referenceCount ?? 0;
      if (rightCount !== leftCount) {
        return rightCount - leftCount;
      }
      if (left.visibility !== right.visibility) {
        return left.visibility === "definite" ? -1 : 1;
      }
      return left.name.localeCompare(right.name);
    });
  const variableOptions = variables.flatMap(variable => {
    const maybeReason = (variable.maybeReason ?? "").trim() || "Variable is not definitely assigned on every normal path to this object.";
    const detail = [
      expressionTypeLabel(variable.dataType),
      variableSourceLabel(variable),
      variable.visibility === "maybe" ? `maybe - ${maybeReason}` : undefined,
    ].filter(Boolean).join(", ");
    const baseToken = variableToken(variable.name);
    const jsonToken = jsonVariableToken(variable.name);
    const base = { label: baseToken, value: baseToken, detail };
    const jsonPathBase = { label: jsonToken, value: jsonToken, detail };
    if (variable.dataType.kind !== "object") {
      return [base, jsonPathBase];
    }
    const variableObjectType = variable.dataType;
    const associationOptions = getAssociationsForEntity(input.metadata, variableObjectType.entityQualifiedName).flatMap(association => {
      const target = getTargetEntityByAssociation(input.metadata, association.qualifiedName, variableObjectType.entityQualifiedName);
      const associationValue = `${baseToken}/${association.qualifiedName}`;
      const associationJsonValue = `${jsonToken}/${association.qualifiedName}`;
      if (!target) {
        return [
          { label: associationValue, value: associationValue, detail: "association" },
          { label: associationJsonValue, value: associationJsonValue, detail: "association" },
        ];
      }
      const nestedAttributes = getEntityAttributes(input.metadata, target.qualifiedName).map(attribute => ({
        label: `${associationValue}/${target.qualifiedName}/${attribute.name}`,
        value: `${associationValue}/${target.qualifiedName}/${attribute.name}`,
        detail: `${target.name}.${attribute.name}: ${expressionTypeLabel(attribute.type)}`,
      }));
      const nestedJsonAttributes = getEntityAttributes(input.metadata, target.qualifiedName).map(attribute => ({
        label: `${associationJsonValue}/${target.qualifiedName}/${attribute.name}`,
        value: `${associationJsonValue}/${target.qualifiedName}/${attribute.name}`,
        detail: `${target.name}.${attribute.name}: ${expressionTypeLabel(attribute.type)}`,
      }));
      return [
        { label: associationValue, value: associationValue, detail: `association -> ${target.qualifiedName}` },
        { label: associationJsonValue, value: associationJsonValue, detail: `association -> ${target.qualifiedName}` },
        ...nestedAttributes,
        ...nestedJsonAttributes,
      ];
    });
    return [
      base,
      jsonPathBase,
      ...getEntityAttributes(input.metadata, variableObjectType.entityQualifiedName).map(attribute => ({
        label: `${baseToken}/${attribute.name}`,
        value: `${baseToken}/${attribute.name}`,
        detail: expressionTypeLabel(attribute.type),
      })),
      ...getEntityAttributes(input.metadata, variableObjectType.entityQualifiedName).map(attribute => ({
        label: `${jsonToken}/${attribute.name}`,
        value: `${jsonToken}/${attribute.name}`,
        detail: expressionTypeLabel(attribute.type),
      })),
      ...associationOptions,
    ];
  });
  const expectedEnumeration = input.expectedType?.kind === "enumeration" ? input.expectedType : undefined;
  const enumOptions = expectedEnumeration
    ? getEnumerationValues(input.metadata, expectedEnumeration.enumerationQualifiedName).map(value => ({
        label: `${expectedEnumeration.enumerationQualifiedName}.${value.key}`,
        value: `${expectedEnumeration.enumerationQualifiedName}.${value.key}`,
        detail: "enum",
      }))
    : [];
  const functionOptions = [
    { label: "empty($variable)", value: "empty()", detail: "function" },
    { label: "not empty($variable)", value: "not empty()", detail: "function" },
    { label: "if condition then value else value", value: "if true then  else ", detail: "function" },
    { label: "toLowerCase($text)", value: "toLowerCase()", detail: "toLowerCase(String) -> String" },
    { label: "toUpperCase($text)", value: "toUpperCase()", detail: "toUpperCase(String) -> String" },
    { label: "substring($text, 0, 1)", value: "substring()", detail: "substring(String, Integer, Integer?) -> String" },
    { label: "contains($text, 'x')", value: "contains()", detail: "contains(String, String) -> Boolean" },
    { label: "startsWith($text, 'x')", value: "startsWith()", detail: "startsWith(String, String) -> Boolean" },
    { label: "endsWith($text, 'x')", value: "endsWith()", detail: "endsWith(String, String) -> Boolean" },
    { label: "trim($text)", value: "trim()", detail: "trim(String) -> String" },
    { label: "replaceAll($text, 'a', 'b')", value: "replaceAll()", detail: "replaceAll(String, String, String) -> String" },
    { label: "replaceFirst($text, 'a', 'b')", value: "replaceFirst()", detail: "replaceFirst(String, String, String) -> String" },
    { label: "isMatch($text, 'regex')", value: "isMatch()", detail: "isMatch(String, String) -> Boolean" },
    { label: "urlEncode($text)", value: "urlEncode()", detail: "urlEncode(String) -> String" },
    { label: "urlDecode($text)", value: "urlDecode()", detail: "urlDecode(String) -> String" },
    { label: "htmlEncode($text)", value: "htmlEncode()", detail: "htmlEncode(String) -> String" },
    { label: "max(1, 2)", value: "max()", detail: "max(Number...) -> Number" },
    { label: "min(1, 2)", value: "min()", detail: "min(Number...) -> Number" },
    { label: "round(1.23, 1)", value: "round()", detail: "round(Decimal, Integer?) -> Decimal" },
    { label: "floor(1.23)", value: "floor()", detail: "floor(Decimal) -> Long" },
    { label: "ceil(1.23)", value: "ceil()", detail: "ceil(Decimal) -> Long" },
    { label: "pow(2, 3)", value: "pow()", detail: "pow(Number, Number) -> Decimal" },
    { label: "abs(-1)", value: "abs()", detail: "abs(Number) -> Number" },
    { label: "sqrt(9)", value: "sqrt()", detail: "sqrt(Number) -> Decimal" },
    { label: "random()", value: "random()", detail: "random() -> Decimal" },
    { label: "isNew($obj)", value: "isNew()", detail: "isNew(Object) -> Boolean" },
    { label: "formatDateTime($dt, 'yyyy-MM-dd')", value: "formatDateTime()", detail: "formatDateTime(DateTime, String) -> String" },
    { label: "toDateTime('2026-01-01', 'yyyy-MM-dd')", value: "toDateTime()", detail: "toDateTime(String, String) -> DateTime" },
    { label: "addDays($dt, 1)", value: "addDays()", detail: "addDays(DateTime, Integer) -> DateTime" },
    { label: "addMonths($dt, 1)", value: "addMonths()", detail: "addMonths(DateTime, Integer) -> DateTime" },
    { label: "addYears($dt, 1)", value: "addYears()", detail: "addYears(DateTime, Integer) -> DateTime" },
    { label: "addHours($dt, 1)", value: "addHours()", detail: "addHours(DateTime, Integer) -> DateTime" },
    { label: "addMinutes($dt, 1)", value: "addMinutes()", detail: "addMinutes(DateTime, Integer) -> DateTime" },
    { label: "addSeconds($dt, 1)", value: "addSeconds()", detail: "addSeconds(DateTime, Integer) -> DateTime" },
    { label: "addWeeks($dt, 1)", value: "addWeeks()", detail: "addWeeks(DateTime, Integer) -> DateTime" },
    { label: "addQuarters($dt, 1)", value: "addQuarters()", detail: "addQuarters(DateTime, Integer) -> DateTime" },
    { label: "getYear($dt)", value: "getYear()", detail: "getYear(DateTime) -> Integer" },
    { label: "getMonth($dt)", value: "getMonth()", detail: "getMonth(DateTime) -> Integer" },
    { label: "getDay($dt)", value: "getDay()", detail: "getDay(DateTime) -> Integer" },
    { label: "getHour($dt)", value: "getHour()", detail: "getHour(DateTime) -> Integer" },
    { label: "getMinute($dt)", value: "getMinute()", detail: "getMinute(DateTime) -> Integer" },
    { label: "getSecond($dt)", value: "getSecond()", detail: "getSecond(DateTime) -> Integer" },
    { label: "getDayOfWeek($dt)", value: "getDayOfWeek()", detail: "getDayOfWeek(DateTime) -> Integer" },
    { label: "dateDiff($start, $end, 'day')", value: "dateDiff()", detail: "dateDiff(DateTime, DateTime, String) -> Integer" },
    { label: "currentDateTime()", value: "currentDateTime()", detail: "currentDateTime() -> DateTime" },
    { label: "parseInteger('1')", value: "parseInteger()", detail: "parseInteger(String) -> Integer" },
    { label: "parseDecimal('1.23')", value: "parseDecimal()", detail: "parseDecimal(String) -> Decimal" },
    { label: "formatDecimal(1.23, '0.00')", value: "formatDecimal()", detail: "formatDecimal(Decimal, String) -> String" },
  ];
  return [...variableOptions, ...enumOptions, ...functionOptions];
}

export function validateMicroflowExpressionForEditor(input: {
  value: string | MicroflowExpression | undefined;
  schema: MicroflowAuthoringSchema;
  metadata: MicroflowMetadataCatalog;
  variableIndex: MicroflowVariableIndex;
  objectId?: string;
  actionId?: string;
  flowId?: string;
  fieldPath?: string;
  expectedType?: MicroflowDataType;
  required?: boolean;
}): ExpressionDiagnostic[] {
  return validateExpression({
    expression: expressionRaw(input.value),
    schema: input.schema,
    metadata: input.metadata,
    variableIndex: input.variableIndex,
    context: {
      objectId: input.objectId,
      actionId: input.actionId,
      flowId: input.flowId,
      fieldPath: input.fieldPath,
      expectedType: input.expectedType,
      required: input.required,
    },
  }).diagnostics;
}
