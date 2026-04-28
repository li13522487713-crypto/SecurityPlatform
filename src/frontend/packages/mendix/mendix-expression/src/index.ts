import type { DataTypeSchema, ExpressionNode, ExpressionSchema, Ref } from "@atlas/mendix-schema";

export interface ExpressionScope {
  variables: Record<string, DataTypeSchema>;
  enumerations: Record<string, string[]>;
}

export interface RuntimeContext {
  variables: Record<string, unknown>;
  currentUser?: Record<string, unknown>;
}

function inferLiteralType(value: unknown): DataTypeSchema {
  if (typeof value === "boolean") {
    return { kind: "Boolean" };
  }
  if (typeof value === "number") {
    return Number.isInteger(value) ? { kind: "Integer" } : { kind: "Decimal" };
  }
  return { kind: "String" };
}

function parseSimpleExpression(expressionText: string): ExpressionNode {
  const text = expressionText.trim();
  if (text === "empty") {
    return { type: "function", functionName: "empty", args: [] };
  }
  if (text.startsWith("if ") && text.includes(" then ") && text.includes(" else ")) {
    const [conditionPart, remainder] = text.slice(3).split(" then ");
    const [thenPart, elsePart] = remainder.split(" else ");
    return {
      type: "if",
      condition: parseSimpleExpression(conditionPart),
      thenNode: parseSimpleExpression(thenPart),
      elseNode: parseSimpleExpression(elsePart)
    };
  }
  if (text.startsWith("contains(") && text.endsWith(")")) {
    const inner = text.slice("contains(".length, -1);
    const [a, b] = inner.split(",").map(part => part.trim());
    return {
      type: "function",
      functionName: "contains",
      args: [parseSimpleExpression(a), parseSimpleExpression(b)]
    };
  }
  if (text === "$currentUser") {
    return { type: "variable", name: "$currentUser" };
  }
  if (/^\$[A-Za-z_][A-Za-z0-9_]*(\/[A-Za-z_][A-Za-z0-9_]*)+$/.test(text)) {
    const [root, ...segments] = text.replace("$", "").split("/");
    return { type: "path", root, segments };
  }
  if (/^\$[A-Za-z_][A-Za-z0-9_]*$/.test(text)) {
    return { type: "variable", name: text };
  }
  if (/^(true|false)$/i.test(text)) {
    return { type: "literal", value: text.toLowerCase() === "true" };
  }
  if (/^-?\d+(\.\d+)?$/.test(text)) {
    return { type: "literal", value: Number(text) };
  }
  const enumMatch = /^([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)$/.exec(text);
  if (enumMatch) {
    return { type: "enum", enumerationName: enumMatch[1], value: enumMatch[2] };
  }
  for (const op of [" and ", " or ", ">=", "<=", "!=", "=", ">", "<"]) {
    const index = text.indexOf(op);
    if (index > 0) {
      const left = text.slice(0, index);
      const right = text.slice(index + op.length);
      const operator = op.trim() as "and" | "or" | "=" | "!=" | ">" | "<" | ">=" | "<=";
      return {
        type: "binary",
        operator,
        left: parseSimpleExpression(left),
        right: parseSimpleExpression(right)
      };
    }
  }
  return { type: "literal", value: text.replace(/^['"]|['"]$/g, "") };
}

function inferNodeType(node: ExpressionNode, scope: ExpressionScope): DataTypeSchema {
  switch (node.type) {
    case "literal":
      return inferLiteralType(node.value);
    case "variable":
      return scope.variables[node.name] ?? { kind: "String" };
    case "path":
      return scope.variables[`$${node.root}`] ?? { kind: "String" };
    case "enum":
      return { kind: "Enumeration", enumerationRef: { kind: "enumeration", id: node.enumerationName } };
    case "binary":
      if (node.operator === "and" || node.operator === "or") {
        return { kind: "Boolean" };
      }
      return { kind: "Boolean" };
    case "function":
      if (node.functionName === "empty") {
        return { kind: "Boolean" };
      }
      return { kind: "Boolean" };
    case "if":
      return inferNodeType(node.thenNode, scope);
    default:
      return { kind: "String" };
  }
}

function collectDepsFromNode(node: ExpressionNode, deps: Array<Ref<"attribute" | "variable" | "entity" | "enumeration">>) {
  if (node.type === "variable") {
    deps.push({ kind: "variable", id: node.name });
  } else if (node.type === "path") {
    deps.push({ kind: "variable", id: `$${node.root}` });
    if (node.segments.length > 0) {
      deps.push({ kind: "attribute", id: node.segments.join(".") });
    }
  } else if (node.type === "enum") {
    deps.push({ kind: "enumeration", id: node.enumerationName });
  } else if (node.type === "binary") {
    collectDepsFromNode(node.left, deps);
    collectDepsFromNode(node.right, deps);
  } else if (node.type === "if") {
    collectDepsFromNode(node.condition, deps);
    collectDepsFromNode(node.thenNode, deps);
    collectDepsFromNode(node.elseNode, deps);
  } else if (node.type === "function") {
    node.args.forEach(arg => collectDepsFromNode(arg, deps));
  }
}

function evaluateNode(node: ExpressionNode, runtimeContext: RuntimeContext): unknown {
  switch (node.type) {
    case "literal":
      return node.value;
    case "variable":
      if (node.name === "$currentUser") {
        return runtimeContext.currentUser ?? null;
      }
      return runtimeContext.variables[node.name];
    case "path": {
      const root = runtimeContext.variables[`$${node.root}`];
      return node.segments.reduce<unknown>((acc, key) => {
        if (acc && typeof acc === "object" && key in (acc as Record<string, unknown>)) {
          return (acc as Record<string, unknown>)[key];
        }
        return undefined;
      }, root);
    }
    case "enum":
      return `${node.enumerationName}.${node.value}`;
    case "binary": {
      const left = evaluateNode(node.left, runtimeContext);
      const right = evaluateNode(node.right, runtimeContext);
      switch (node.operator) {
        case "and":
          return Boolean(left) && Boolean(right);
        case "or":
          return Boolean(left) || Boolean(right);
        case "=":
          return left === right;
        case "!=":
          return left !== right;
        case ">":
          return Number(left) > Number(right);
        case "<":
          return Number(left) < Number(right);
        case ">=":
          return Number(left) >= Number(right);
        case "<=":
          return Number(left) <= Number(right);
      }
      return false;
    }
    case "function":
      if (node.functionName === "empty") {
        return true;
      }
      if (node.functionName === "contains") {
        const left = String(evaluateNode(node.args[0], runtimeContext) ?? "");
        const right = String(evaluateNode(node.args[1], runtimeContext) ?? "");
        return left.includes(right);
      }
      return null;
    case "if":
      return evaluateNode(node.condition, runtimeContext)
        ? evaluateNode(node.thenNode, runtimeContext)
        : evaluateNode(node.elseNode, runtimeContext);
    default:
      return null;
  }
}

export function parseExpression(expressionText: string): ExpressionSchema {
  const ast = parseSimpleExpression(expressionText);
  return {
    source: expressionText,
    ast,
    dependencies: [],
    validation: []
  };
}

export function inferExpressionType(ast: ExpressionNode, scope: ExpressionScope): DataTypeSchema {
  return inferNodeType(ast, scope);
}

export function collectExpressionDependencies(ast: ExpressionNode): Array<Ref<"attribute" | "variable" | "entity" | "enumeration">> {
  const deps: Array<Ref<"attribute" | "variable" | "entity" | "enumeration">> = [];
  collectDepsFromNode(ast, deps);
  return deps;
}

export function validateExpression(
  expression: ExpressionSchema,
  expectedType: DataTypeSchema,
  scope: ExpressionScope
) {
  const inferred = inferExpressionType(expression.ast, scope);
  const issues: Array<{ code: string; message: string }> = [];
  if (inferred.kind !== expectedType.kind) {
    issues.push({
      code: "EXPRESSION_TYPE_MISMATCH",
      message: `表达式返回类型为 ${inferred.kind}，期望 ${expectedType.kind}`
    });
  }
  return {
    valid: issues.length === 0,
    issues
  };
}

export function evaluateExpression(expression: ExpressionSchema, runtimeContext: RuntimeContext): unknown {
  return evaluateNode(expression.ast, runtimeContext);
}
