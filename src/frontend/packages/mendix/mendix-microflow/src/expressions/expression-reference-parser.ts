import type { MicroflowExpression } from "../schema/types";
import { expressionDiagnostic, type ExpressionParseResult, type ExpressionReference } from "./expression-types";

function rawExpression(expression: string | MicroflowExpression | undefined): string {
  return typeof expression === "string" ? expression : expression?.raw ?? expression?.text ?? "";
}

function maskStringLiterals(raw: string): string {
  let result = "";
  let quote: "'" | "\"" | undefined;
  for (let index = 0; index < raw.length; index += 1) {
    const char = raw[index];
    if (quote) {
      result += " ";
      if (char === quote && raw[index - 1] !== "\\") {
        quote = undefined;
      }
      continue;
    }
    if (char === "'" || char === "\"") {
      quote = char;
      result += " ";
      continue;
    }
    result += char;
  }
  return result;
}

export function parseExpressionReferences(expression: string | MicroflowExpression | undefined): ExpressionParseResult {
  const raw = rawExpression(expression);
  const masked = maskStringLiterals(raw);
  const references: ExpressionReference[] = [];
  const diagnostics: ExpressionParseResult["diagnostics"] = [];
  const variableNames = new Set<string>();
  const attributeAccesses: ExpressionParseResult["attributeAccesses"] = [];

  for (const match of masked.matchAll(/\$([A-Za-z_][A-Za-z0-9_]*)(?:\/([A-Za-z_][A-Za-z0-9_]*)(?:\/[A-Za-z_][A-Za-z0-9_]*)*)?/g)) {
    const start = match.index ?? 0;
    const end = start + match[0].length;
    const original = raw.slice(start, end);
    const variableName = match[1];
    variableNames.add(variableName);
    const path = original.split("/").slice(1);
    references.push({ kind: "variable", raw: `$${variableName}`, variableName, range: { start, end: start + variableName.length + 1 } });
    if (path.length > 0) {
      references.push({
        kind: "memberAccess",
        raw: original,
        variableName,
        memberName: path[0],
        path,
        range: { start, end },
      });
      attributeAccesses.push({ variableName, attributeName: path[0] });
    }
  }

  for (const match of masked.matchAll(/\b([A-Za-z_][A-Za-z0-9_]*)\s*\(/g)) {
    const start = match.index ?? 0;
    references.push({
      kind: "functionCall",
      raw: match[0],
      functionName: match[1],
      range: { start, end: start + match[0].length },
    });
  }

  for (const match of masked.matchAll(/\$(?![A-Za-z_])/g)) {
    diagnostics.push(expressionDiagnostic({
      code: "MF_EXPR_PARSE_ERROR",
      message: "Invalid variable reference. Variable names must start with a letter or underscore.",
      range: { start: match.index ?? 0, end: (match.index ?? 0) + 1 },
    }));
  }
  for (const match of masked.matchAll(/\$[A-Za-z_][A-Za-z0-9_]*\/(?![A-Za-z_])|\$[A-Za-z_][A-Za-z0-9_]*(?:\/[A-Za-z_][A-Za-z0-9_]*)*\/\//g)) {
    const start = match.index ?? 0;
    diagnostics.push(expressionDiagnostic({
      code: "MF_EXPR_PARSE_ERROR",
      message: "Invalid member access path.",
      range: { start, end: start + match[0].length },
    }));
  }

  return { raw, references, diagnostics, variables: [...variableNames], attributeAccesses };
}
