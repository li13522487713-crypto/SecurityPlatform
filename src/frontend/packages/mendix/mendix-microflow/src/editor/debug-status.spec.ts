import { describe, expect, it } from "vitest";
import { getDebugWsStatusTag } from "./debug-status";

describe("getDebugWsStatusTag", () => {
  it("maps connected to green tag", () => {
    expect(getDebugWsStatusTag("connected")).toEqual({ text: "WS connected", color: "green" });
  });

  it("maps connecting to blue tag", () => {
    expect(getDebugWsStatusTag("connecting")).toEqual({ text: "WS connecting", color: "blue" });
  });

  it("maps error to red tag", () => {
    expect(getDebugWsStatusTag("error")).toEqual({ text: "WS error", color: "red" });
  });

  it("maps disconnected to grey tag", () => {
    expect(getDebugWsStatusTag("disconnected")).toEqual({ text: "WS disconnected", color: "grey" });
  });
});
