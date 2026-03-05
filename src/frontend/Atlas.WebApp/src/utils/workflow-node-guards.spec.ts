import { describe, it, expect } from "vitest";
import {
  isApproveNode,
  isCopyNode,
  isConditionNode,
  isDynamicConditionNode,
  isParallelConditionNode,
  isParallelNode,
  isInclusiveNode,
  isRouteNode,
  isCallProcessNode,
  isTimerNode,
  isTriggerNode,
  isStartNode,
  isEndNode
} from "@/utils/workflow-node-guards";
import type { TreeNode } from "@/types/approval-tree";

const makeNode = (nodeType: string): TreeNode => ({
  id: "test-id",
  nodeType: nodeType as TreeNode["nodeType"],
  nodeName: "测试节点"
} as TreeNode);

describe("workflow-node-guards", () => {
  it("isApproveNode identifies approve nodes", () => {
    const node = makeNode("approve");
    expect(isApproveNode(node)).toBe(true);
    expect(isCopyNode(node)).toBe(false);
  });

  it("isCopyNode identifies copy nodes", () => {
    const node = makeNode("copy");
    expect(isCopyNode(node)).toBe(true);
    expect(isApproveNode(node)).toBe(false);
  });

  it("isConditionNode identifies condition nodes", () => {
    const node = makeNode("condition");
    expect(isConditionNode(node)).toBe(true);
  });

  it("isDynamicConditionNode identifies dynamicCondition nodes", () => {
    const node = makeNode("dynamicCondition");
    expect(isDynamicConditionNode(node)).toBe(true);
  });

  it("isParallelConditionNode identifies parallelCondition nodes", () => {
    const node = makeNode("parallelCondition");
    expect(isParallelConditionNode(node)).toBe(true);
  });

  it("isParallelNode identifies parallel nodes", () => {
    const node = makeNode("parallel");
    expect(isParallelNode(node)).toBe(true);
  });

  it("isInclusiveNode identifies inclusive nodes", () => {
    const node = makeNode("inclusive");
    expect(isInclusiveNode(node)).toBe(true);
  });

  it("isRouteNode identifies route nodes", () => {
    const node = makeNode("route");
    expect(isRouteNode(node)).toBe(true);
  });

  it("isCallProcessNode identifies callProcess nodes", () => {
    const node = makeNode("callProcess");
    expect(isCallProcessNode(node)).toBe(true);
  });

  it("isTimerNode identifies timer nodes", () => {
    const node = makeNode("timer");
    expect(isTimerNode(node)).toBe(true);
  });

  it("isTriggerNode identifies trigger nodes", () => {
    const node = makeNode("trigger");
    expect(isTriggerNode(node)).toBe(true);
  });

  it("isStartNode identifies start nodes", () => {
    const node = makeNode("start");
    expect(isStartNode(node)).toBe(true);
  });

  it("isEndNode identifies end nodes", () => {
    const node = makeNode("end");
    expect(isEndNode(node)).toBe(true);
  });

  it("each guard returns false for non-matching types", () => {
    const approveNode = makeNode("approve");
    expect(isConditionNode(approveNode)).toBe(false);
    expect(isTimerNode(approveNode)).toBe(false);
    expect(isEndNode(approveNode)).toBe(false);
  });
});
