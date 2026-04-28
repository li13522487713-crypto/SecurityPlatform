import type { MicroflowExpression } from "../schema/types";
import type { ExpressionParseResult } from "./expression-types";
import { parseExpression } from "./expression-parser";

export function parseExpressionReferences(expression: string | MicroflowExpression | undefined): ExpressionParseResult {
  const parsed = parseExpression(expression);
  const raw = parsed.raw;
  const variableNames = new Set<string>();
  const attributeAccesses: ExpressionParseResult["attributeAccesses"] = [];
  for (const reference of parsed.references) {
    if (reference.kind === "variable") {
      variableNames.add(reference.variableName);
    }
    if (reference.kind === "memberAccess") {
      variableNames.add(reference.variableName);
      attributeAccesses.push({ variableName: reference.variableName, attributeName: reference.memberName });
    }
  }
  return {
    raw,
    ast: parsed.ast,
    tokens: parsed.tokens,
    references: parsed.references,
    diagnostics: parsed.diagnostics,
    variables: [...variableNames],
    attributeAccesses,
  };
}
