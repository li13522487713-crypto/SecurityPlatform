import { describe, expect, it } from "vitest";
import { getDebugLatencyColor, getDebugWsStatusTag } from "./debug-status";

describe("getDebugWsStatusTag", () => {
  it("maps connected to green tag", () => {
    expect(getDebugWsStatusTag("connected")).toEqual({ text: "已连接", color: "green" });
  });

  it("maps connecting to blue tag", () => {
    expect(getDebugWsStatusTag("connecting")).toEqual({ text: "连接中", color: "blue" });
  });

  it("maps reconnecting to orange tag", () => {
    expect(getDebugWsStatusTag("reconnecting")).toEqual({ text: "重连中", color: "orange" });
  });

  it("maps error to red tag", () => {
    expect(getDebugWsStatusTag("error")).toEqual({ text: "连接失败", color: "red" });
  });

  it("maps disconnected to grey tag", () => {
    expect(getDebugWsStatusTag("disconnected")).toEqual({ text: "已断开", color: "grey" });
  });

  it("falls back to disconnected style for unknown values", () => {
    expect(getDebugWsStatusTag("unknown" as never)).toEqual({ text: "已断开", color: "grey" });
  });

  it("maps latency threshold color: normal", () => {
    expect(getDebugLatencyColor(120)).toBe("#6b7280");
    expect(getDebugLatencyColor(200)).toBe("#6b7280");
  });

  it("maps latency threshold color: high", () => {
    expect(getDebugLatencyColor(201)).toBe("#f59e0b");
    expect(getDebugLatencyColor(500)).toBe("#f59e0b");
  });

  it("maps latency threshold color: bad", () => {
    expect(getDebugLatencyColor(501)).toBe("#ef4444");
  });
});
