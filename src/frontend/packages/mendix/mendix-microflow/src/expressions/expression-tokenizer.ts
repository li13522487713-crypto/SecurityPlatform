import { expressionDiagnostic, type ExpressionDiagnostic, type TextRange } from "./expression-types";

export type MicroflowExpressionTokenKind =
  | "variable"
  | "identifier"
  | "string"
  | "number"
  | "boolean"
  | "null"
  | "operator"
  | "parenOpen"
  | "parenClose"
  | "comma"
  | "slash"
  | "keyword"
  | "unknown"
  | "eof";

export interface MicroflowExpressionToken {
  kind: MicroflowExpressionTokenKind;
  value: string;
  range: TextRange;
}

export interface MicroflowExpressionTokenizeResult {
  tokens: MicroflowExpressionToken[];
  diagnostics: ExpressionDiagnostic[];
}

const keywordSet = new Set(["and", "or", "not", "if", "then", "else"]);

function token(kind: MicroflowExpressionTokenKind, value: string, start: number, end: number): MicroflowExpressionToken {
  return { kind, value, range: { start, end } };
}

function isIdentifierStart(char: string | undefined): boolean {
  return Boolean(char && /[A-Za-z_]/.test(char));
}

function isIdentifierPart(char: string | undefined): boolean {
  return Boolean(char && /[A-Za-z0-9_.]/.test(char));
}

export function tokenizeExpression(raw: string): MicroflowExpressionTokenizeResult {
  const tokens: MicroflowExpressionToken[] = [];
  const diagnostics: ExpressionDiagnostic[] = [];
  let index = 0;
  while (index < raw.length) {
    const char = raw[index];
    if (/\s/.test(char)) {
      index += 1;
      continue;
    }
    if (char === "$") {
      const start = index;
      index += 1;
      if (!isIdentifierStart(raw[index])) {
        diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_PARSE_ERROR",
          message: "Invalid variable reference. Variable names must start with a letter or underscore.",
          range: { start, end: index },
        }));
        tokens.push(token("unknown", "$", start, index));
        continue;
      }
      while (isIdentifierPart(raw[index]) || raw[index] === "/") {
        index += 1;
      }
      tokens.push(token("variable", raw.slice(start, index), start, index));
      continue;
    }
    if (char === "'" || char === "\"") {
      const quote = char;
      const start = index;
      index += 1;
      let closed = false;
      while (index < raw.length) {
        if (raw[index] === quote && raw[index - 1] !== "\\") {
          index += 1;
          closed = true;
          break;
        }
        index += 1;
      }
      if (!closed) {
        diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_PARSE_ERROR",
          message: "String literal is not closed.",
          range: { start, end: index },
        }));
      }
      tokens.push(token("string", raw.slice(start, index), start, index));
      continue;
    }
    if (/\d/.test(char)) {
      const start = index;
      while (/\d/.test(raw[index])) {
        index += 1;
      }
      if (raw[index] === "." && /\d/.test(raw[index + 1])) {
        index += 1;
        while (/\d/.test(raw[index])) {
          index += 1;
        }
      }
      tokens.push(token("number", raw.slice(start, index), start, index));
      continue;
    }
    if (isIdentifierStart(char)) {
      const start = index;
      while (isIdentifierPart(raw[index])) {
        index += 1;
      }
      const value = raw.slice(start, index);
      if (value === "true" || value === "false") {
        tokens.push(token("boolean", value, start, index));
      } else if (value === "null" || value === "empty") {
        tokens.push(token(value === "empty" ? "identifier" : "null", value, start, index));
      } else if (keywordSet.has(value)) {
        tokens.push(token("keyword", value, start, index));
      } else {
        tokens.push(token("identifier", value, start, index));
      }
      continue;
    }
    if (char === "(") {
      tokens.push(token("parenOpen", char, index, index + 1));
      index += 1;
      continue;
    }
    if (char === ")") {
      tokens.push(token("parenClose", char, index, index + 1));
      index += 1;
      continue;
    }
    if (char === ",") {
      tokens.push(token("comma", char, index, index + 1));
      index += 1;
      continue;
    }
    if (char === "/") {
      tokens.push(token("operator", char, index, index + 1));
      index += 1;
      continue;
    }
    const two = raw.slice(index, index + 2);
    if (two === "!=" || two === ">=" || two === "<=") {
      tokens.push(token("operator", two, index, index + 2));
      index += 2;
      continue;
    }
    if ("=<>+-*".includes(char)) {
      tokens.push(token("operator", char, index, index + 1));
      index += 1;
      continue;
    }
    diagnostics.push(expressionDiagnostic({
      code: "MF_EXPR_PARSE_ERROR",
      message: `Unknown token "${char}".`,
      range: { start: index, end: index + 1 },
    }));
    tokens.push(token("unknown", char, index, index + 1));
    index += 1;
  }
  tokens.push(token("eof", "", raw.length, raw.length));
  return { tokens, diagnostics };
}
