import { autocompletion, type CompletionContext } from "@codemirror/autocomplete";
import { defaultKeymap, history, historyKeymap } from "@codemirror/commands";
import { linter, type Diagnostic as CodeMirrorDiagnostic } from "@codemirror/lint";
import { Compartment, EditorState, RangeSetBuilder } from "@codemirror/state";
import { Decoration, EditorView, ViewPlugin, hoverTooltip, keymap, lineNumbers, type DecorationSet, type ViewUpdate } from "@codemirror/view";
import { useEffect, useMemo, useRef } from "react";

import { expressionRaw, expressionTypeLabel, validateExpression } from "../expressions";
import { getEntityAttributes, getEnumerationValues, type MicroflowMetadataCatalog } from "../metadata";
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
    const detail = [
      expressionTypeLabel(variable.dataType),
      variableSourceLabel(variable),
      variable.visibility === "maybe" ? "maybe" : undefined,
    ].filter(Boolean).join(", ");
    const base = { label: `$${variable.name}`, value: `$${variable.name}`, detail };
    const jsonPathBase = { label: `$.${variable.name}`, value: `$.${variable.name}`, detail };
    if (variable.dataType.kind !== "object") {
      return [base, jsonPathBase];
    }
    return [
      base,
      jsonPathBase,
      ...getEntityAttributes(input.metadata, variable.dataType.entityQualifiedName).map(attribute => ({
        label: `$${variable.name}/${attribute.name}`,
        value: `$${variable.name}/${attribute.name}`,
        detail: expressionTypeLabel(attribute.type),
      })),
      ...getEntityAttributes(input.metadata, variable.dataType.entityQualifiedName).map(attribute => ({
        label: `$.${variable.name}/${attribute.name}`,
        value: `$.${variable.name}/${attribute.name}`,
        detail: expressionTypeLabel(attribute.type),
      })),
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
