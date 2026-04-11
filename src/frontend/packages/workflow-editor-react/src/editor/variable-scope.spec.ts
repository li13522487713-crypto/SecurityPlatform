import { describe, expect, it } from "vitest";
import {
  computeVariableScopes,
  getAvailableVariablePaths,
  type VariableScopeConnection,
  type VariableScopeNode,
} from "./variable-scope";

describe("variable-scope", () => {
  // ─── Test helpers ───────────────────────────────────────────────────────────

  const makeNode = (
    key: string,
    type = "Llm",
    outputTypes?: Record<string, string>
  ): VariableScopeNode => ({ key, type, outputTypes });

  const makeConn = (fromNode: string, toNode: string): VariableScopeConnection => ({
    fromNode,
    toNode,
  });

  // ─── System variables ───────────────────────────────────────────────────────

  it("所有节点都应包含系统变量（inputs/workflow/runtime）", () => {
    const nodes = [makeNode("entry_1", "Entry"), makeNode("llm_1")];
    const scopes = computeVariableScopes(nodes, [makeConn("entry_1", "llm_1")]);

    const scope = scopes.get("llm_1")!;
    const paths = scope.availableVariables.map((v) => v.path);

    expect(paths).toContain("inputs.input");
    expect(paths).toContain("workflow.executionId");
    expect(paths).toContain("runtime.userId");
  });

  // ─── Linear chain ──────────────────────────────────────────────────────────

  it("直线链：下游节点可以看到所有上游节点的输出变量", () => {
    const nodes = [
      makeNode("entry_1", "Entry"),
      makeNode("llm_1", "Llm", { output: "string" }),
      makeNode("text_1", "TextProcessor", { result: "string" }),
    ];
    const connections = [makeConn("entry_1", "llm_1"), makeConn("llm_1", "text_1")];
    const scopes = computeVariableScopes(nodes, connections);

    // text_1 应能看到 llm_1.output
    const textScope = scopes.get("text_1")!;
    const paths = textScope.availableVariables.map((v) => v.path);
    expect(paths).toContain("llm_1.output");
    // 也能看到 entry_1 的变量（没有 outputTypes 则 entry 不产生额外变量）
  });

  it("直线链：上游节点不能看到下游变量", () => {
    const nodes = [
      makeNode("entry_1", "Entry"),
      makeNode("llm_1", "Llm", { output: "string" }),
    ];
    const connections = [makeConn("entry_1", "llm_1")];
    const scopes = computeVariableScopes(nodes, connections);

    // entry_1 不应看到 llm_1.output（它是下游）
    const entryScope = scopes.get("entry_1")!;
    const paths = entryScope.availableVariables.map((v) => v.path);
    expect(paths).not.toContain("llm_1.output");
  });

  // ─── Transitive upstream ───────────────────────────────────────────────────

  it("传递性上游：n3 应看到 n1 和 n2 的输出变量", () => {
    const nodes = [
      makeNode("n1", "Entry", { out1: "string" }),
      makeNode("n2", "Llm", { out2: "number" }),
      makeNode("n3", "Llm"),
    ];
    const connections = [makeConn("n1", "n2"), makeConn("n2", "n3")];
    const scopes = computeVariableScopes(nodes, connections);

    const n3Scope = scopes.get("n3")!;
    const paths = n3Scope.availableVariables.map((v) => v.path);
    expect(paths).toContain("n1.out1");
    expect(paths).toContain("n2.out2");
  });

  // ─── Fork/Join (parallel branches) ────────────────────────────────────────

  it("并行分支汇聚：汇聚节点应能看到所有分支节点的变量", () => {
    const nodes = [
      makeNode("entry_1", "Entry"),
      makeNode("branch_a", "Llm", { outputA: "string" }),
      makeNode("branch_b", "Llm", { outputB: "string" }),
      makeNode("merge_1", "Llm"),
    ];
    const connections = [
      makeConn("entry_1", "branch_a"),
      makeConn("entry_1", "branch_b"),
      makeConn("branch_a", "merge_1"),
      makeConn("branch_b", "merge_1"),
    ];
    const scopes = computeVariableScopes(nodes, connections);

    const mergeScope = scopes.get("merge_1")!;
    const paths = mergeScope.availableVariables.map((v) => v.path);
    expect(paths).toContain("branch_a.outputA");
    expect(paths).toContain("branch_b.outputB");
  });

  // ─── producedVariables ─────────────────────────────────────────────────────

  it("producedVariables 应反映节点自身 outputTypes", () => {
    const nodes = [makeNode("llm_1", "Llm", { output: "string", confidence: "number" })];
    const scopes = computeVariableScopes(nodes, []);

    const scope = scopes.get("llm_1")!;
    expect(scope.producedVariables).toHaveLength(2);
    expect(scope.producedVariables.map((v) => v.path)).toContain("llm_1.output");
    expect(scope.producedVariables.map((v) => v.path)).toContain("llm_1.confidence");
  });

  it("没有 outputTypes 的节点不应产生变量", () => {
    const nodes = [makeNode("entry_1", "Entry")];
    const scopes = computeVariableScopes(nodes, []);

    const scope = scopes.get("entry_1")!;
    expect(scope.producedVariables).toHaveLength(0);
  });

  // ─── Global variables ──────────────────────────────────────────────────────

  it("全局变量应对所有节点可见", () => {
    const nodes = [makeNode("llm_1")];
    const scopes = computeVariableScopes(nodes, [], { apiKey: "string", maxRetry: "number" });

    const scope = scopes.get("llm_1")!;
    const paths = scope.availableVariables.map((v) => v.path);
    expect(paths).toContain("global.apiKey");
    expect(paths).toContain("global.maxRetry");
  });

  // ─── getAvailableVariablePaths ─────────────────────────────────────────────

  it("getAvailableVariablePaths 返回上游变量路径集合", () => {
    const nodes = [
      makeNode("entry_1", "Entry"),
      makeNode("llm_1", "Llm", { output: "string" }),
      makeNode("text_1"),
    ];
    const connections = [makeConn("entry_1", "llm_1"), makeConn("llm_1", "text_1")];

    const paths = getAvailableVariablePaths("text_1", nodes, connections);
    expect(paths.has("llm_1.output")).toBe(true);
    expect(paths.has("inputs.input")).toBe(true);
    // text_1 本身的变量不在 available（只在 produced 中）
    expect(paths.has("text_1.anything")).toBe(false);
  });

  it("getAvailableVariablePaths 对不存在的节点返回空集合", () => {
    const paths = getAvailableVariablePaths("non_existent", [], []);
    expect(paths.size).toBe(0);
  });

  // ─── Isolated node ─────────────────────────────────────────────────────────

  it("孤立节点仍应有系统变量可用", () => {
    const nodes = [makeNode("orphan_1", "Llm")];
    const scopes = computeVariableScopes(nodes, []);

    const scope = scopes.get("orphan_1")!;
    const paths = scope.availableVariables.map((v) => v.path);
    expect(paths).toContain("inputs.input");
    expect(paths).not.toContain("any.upstream");
  });

  // ─── Empty canvas ──────────────────────────────────────────────────────────

  it("空画布返回空 Map", () => {
    const scopes = computeVariableScopes([], []);
    expect(scopes.size).toBe(0);
  });
});
