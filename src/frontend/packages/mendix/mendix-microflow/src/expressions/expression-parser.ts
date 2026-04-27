import type { MicroflowExpression } from "../schema/types";
import type {
  MicroflowExpressionAstNode,
  MicroflowExpressionBinaryNode,
  MicroflowExpressionUnknownNode,
} from "./expression-ast";
import { tokenizeExpression, type MicroflowExpressionToken } from "./expression-tokenizer";
import { expressionDiagnostic, type ExpressionDiagnostic, type ExpressionReference, type TextRange } from "./expression-types";

export interface MicroflowExpressionParseResult {
  raw: string;
  ast: MicroflowExpressionAstNode;
  tokens: MicroflowExpressionToken[];
  references: ExpressionReference[];
  diagnostics: ExpressionDiagnostic[];
}

function rawExpression(expression: string | MicroflowExpression | undefined): string {
  return typeof expression === "string" ? expression : expression?.raw ?? expression?.text ?? "";
}

function rangeFrom(left: TextRange, right: TextRange): TextRange {
  return { start: left.start, end: right.end };
}

function unknown(raw: string, range: TextRange, reason: string, children?: MicroflowExpressionAstNode[]): MicroflowExpressionUnknownNode {
  return { kind: "unknown", raw: raw.slice(range.start, range.end), range, reason, children };
}

class Parser {
  private cursor = 0;
  private readonly diagnostics: ExpressionDiagnostic[];

  constructor(
    private readonly raw: string,
    private readonly tokens: MicroflowExpressionToken[],
    diagnostics: ExpressionDiagnostic[],
  ) {
    this.diagnostics = [...diagnostics];
  }

  parse(): { ast: MicroflowExpressionAstNode; diagnostics: ExpressionDiagnostic[] } {
    if (this.peek().kind === "eof") {
      return {
        ast: unknown(this.raw, { start: 0, end: 0 }, "empty expression"),
        diagnostics: this.diagnostics,
      };
    }
    const ast = this.parseIf();
    if (this.peek().kind !== "eof") {
      this.diagnostics.push(expressionDiagnostic({
        code: "MF_EXPR_PARSE_ERROR",
        message: `Unexpected token "${this.peek().value}".`,
        range: this.peek().range,
      }));
    }
    return { ast, diagnostics: this.diagnostics };
  }

  private peek(offset = 0): MicroflowExpressionToken {
    return this.tokens[this.cursor + offset] ?? this.tokens[this.tokens.length - 1];
  }

  private advance(): MicroflowExpressionToken {
    const current = this.peek();
    this.cursor += 1;
    return current;
  }

  private matchValue(value: string): MicroflowExpressionToken | undefined {
    if (this.peek().value === value) {
      return this.advance();
    }
    return undefined;
  }

  private parseIf(): MicroflowExpressionAstNode {
    if (this.peek().kind === "keyword" && this.peek().value === "if") {
      const start = this.advance();
      const condition = this.parseOr();
      if (!this.matchValue("then")) {
        this.diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_PARSE_ERROR",
          message: "If expression requires then.",
          range: this.peek().range,
        }));
        return unknown(this.raw, rangeFrom(start.range, condition.range), "if missing then", [condition]);
      }
      const thenBranch = this.parseOr();
      if (!this.matchValue("else")) {
        this.diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_PARSE_ERROR",
          message: "If expression requires else.",
          range: this.peek().range,
        }));
        return unknown(this.raw, rangeFrom(start.range, thenBranch.range), "if missing else", [condition, thenBranch]);
      }
      const elseBranch = this.parseOr();
      const range = rangeFrom(start.range, elseBranch.range);
      return { kind: "if", raw: this.raw.slice(range.start, range.end), range, condition, thenBranch, elseBranch };
    }
    return this.parseOr();
  }

  private parseOr(): MicroflowExpressionAstNode {
    return this.parseBinary(() => this.parseAnd(), ["or"]);
  }

  private parseAnd(): MicroflowExpressionAstNode {
    return this.parseBinary(() => this.parseComparison(), ["and"]);
  }

  private parseComparison(): MicroflowExpressionAstNode {
    return this.parseBinary(() => this.parseAdditive(), ["=", "!=", ">", "<", ">=", "<="]);
  }

  private parseAdditive(): MicroflowExpressionAstNode {
    return this.parseBinary(() => this.parseMultiplicative(), ["+", "-"]);
  }

  private parseMultiplicative(): MicroflowExpressionAstNode {
    return this.parseBinary(() => this.parseUnary(), ["*", "/"]);
  }

  private parseBinary(parseOperand: () => MicroflowExpressionAstNode, operators: string[]): MicroflowExpressionAstNode {
    let left = parseOperand();
    while (operators.includes(this.peek().value)) {
      const operator = this.advance();
      const right = parseOperand();
      const range = rangeFrom(left.range, right.range);
      left = {
        kind: "binary",
        raw: this.raw.slice(range.start, range.end),
        range,
        operator: operator.value as MicroflowExpressionBinaryNode["operator"],
        left,
        right,
      };
    }
    return left;
  }

  private parseUnary(): MicroflowExpressionAstNode {
    if (this.peek().value === "not" || this.peek().value === "-") {
      const operator = this.advance();
      const argument = this.parseUnary();
      const range = rangeFrom(operator.range, argument.range);
      return {
        kind: "unary",
        raw: this.raw.slice(range.start, range.end),
        range,
        operator: operator.value as "not" | "-",
        argument,
      };
    }
    return this.parsePrimary();
  }

  private parsePrimary(): MicroflowExpressionAstNode {
    const current = this.advance();
    if (current.kind === "boolean") {
      return { kind: "literal", literalKind: "boolean", value: current.value === "true", raw: current.value, range: current.range };
    }
    if (current.kind === "null") {
      return { kind: "literal", literalKind: "null", value: null, raw: current.value, range: current.range };
    }
    if (current.kind === "string") {
      return { kind: "literal", literalKind: "string", value: current.value.slice(1, -1), raw: current.value, range: current.range };
    }
    if (current.kind === "number") {
      const decimal = current.value.includes(".");
      return { kind: "literal", literalKind: decimal ? "decimal" : "integer", value: Number(current.value), raw: current.value, range: current.range };
    }
    if (current.kind === "variable") {
      return this.variableNode(current);
    }
    if (current.kind === "identifier") {
      if (this.peek().kind === "parenOpen") {
        return this.functionNode(current);
      }
      const parts = current.value.split(".");
      if (parts.length >= 3) {
        return { kind: "enumValue", qualifiedName: current.value, valueName: parts[parts.length - 1], raw: current.value, range: current.range };
      }
      return { kind: "enumValue", qualifiedName: current.value, valueName: current.value, raw: current.value, range: current.range };
    }
    if (current.kind === "parenOpen") {
      const expression = this.parseIf();
      if (this.peek().kind !== "parenClose") {
        this.diagnostics.push(expressionDiagnostic({
          code: "MF_EXPR_PARSE_ERROR",
          message: "Closing parenthesis is required.",
          range: this.peek().range,
        }));
        return unknown(this.raw, rangeFrom(current.range, expression.range), "missing closing parenthesis", [expression]);
      }
      const close = this.advance();
      return { ...expression, raw: this.raw.slice(current.range.start, close.range.end), range: rangeFrom(current.range, close.range) };
    }
    this.diagnostics.push(expressionDiagnostic({
      code: "MF_EXPR_PARSE_ERROR",
      message: `Unexpected token "${current.value}".`,
      range: current.range,
    }));
    return unknown(this.raw, current.range, "unexpected token");
  }

  private variableNode(current: MicroflowExpressionToken): MicroflowExpressionAstNode {
    const raw = current.value;
    const body = raw.slice(1);
    if (body.endsWith("/") || body.includes("//")) {
      this.diagnostics.push(expressionDiagnostic({
        code: "MF_EXPR_PARSE_ERROR",
        message: "Invalid member access path.",
        range: current.range,
      }));
      return unknown(this.raw, current.range, "invalid member access path");
    }
    const [variableName, ...path] = body.split("/");
    if (!variableName) {
      return unknown(this.raw, current.range, "invalid variable reference");
    }
    if (!path.length) {
      return { kind: "variable", variableName, raw, range: current.range };
    }
    return { kind: "memberAccess", variableName, path, raw, range: current.range };
  }

  private functionNode(identifier: MicroflowExpressionToken): MicroflowExpressionAstNode {
    const open = this.advance();
    const args: MicroflowExpressionAstNode[] = [];
    while (this.peek().kind !== "parenClose" && this.peek().kind !== "eof") {
      args.push(this.parseIf());
      if (this.peek().kind === "comma") {
        this.advance();
      } else if (this.peek().kind !== "parenClose") {
        break;
      }
    }
    if (this.peek().kind !== "parenClose") {
      this.diagnostics.push(expressionDiagnostic({
        code: "MF_EXPR_PARSE_ERROR",
        message: `Function "${identifier.value}" requires closing parenthesis.`,
        range: open.range,
      }));
      return unknown(this.raw, rangeFrom(identifier.range, args[args.length - 1]?.range ?? open.range), "function missing closing parenthesis", args);
    }
    const close = this.advance();
    const range = rangeFrom(identifier.range, close.range);
    return { kind: "functionCall", functionName: identifier.value, args, raw: this.raw.slice(range.start, range.end), range };
  }
}

function collectReferences(ast: MicroflowExpressionAstNode, references: ExpressionReference[]): void {
  switch (ast.kind) {
    case "variable":
      references.push({ kind: "variable", raw: ast.raw, variableName: ast.variableName, range: ast.range });
      break;
    case "memberAccess":
      references.push({ kind: "variable", raw: `$${ast.variableName}`, variableName: ast.variableName, range: { start: ast.range.start, end: ast.range.start + ast.variableName.length + 1 } });
      references.push({ kind: "memberAccess", raw: ast.raw, variableName: ast.variableName, memberName: ast.path[0], path: ast.path, range: ast.range });
      break;
    case "binary":
      collectReferences(ast.left, references);
      collectReferences(ast.right, references);
      break;
    case "unary":
      collectReferences(ast.argument, references);
      break;
    case "functionCall":
      references.push({ kind: "functionCall", raw: ast.raw, functionName: ast.functionName, range: ast.range });
      ast.args.forEach(arg => collectReferences(arg, references));
      break;
    case "if":
      collectReferences(ast.condition, references);
      collectReferences(ast.thenBranch, references);
      collectReferences(ast.elseBranch, references);
      break;
    case "unknown":
      ast.children?.forEach(child => collectReferences(child, references));
      break;
    default:
      break;
  }
}

export function parseExpression(expression: string | MicroflowExpression | undefined): MicroflowExpressionParseResult {
  const raw = rawExpression(expression);
  const tokenize = tokenizeExpression(raw);
  const parser = new Parser(raw, tokenize.tokens, tokenize.diagnostics);
  const { ast, diagnostics } = parser.parse();
  const references: ExpressionReference[] = [];
  collectReferences(ast, references);
  return { raw, ast, tokens: tokenize.tokens, references, diagnostics };
}
