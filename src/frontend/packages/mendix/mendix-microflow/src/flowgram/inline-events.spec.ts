import { describe, expect, it, vi } from "vitest";

import { emitInlineNodeToggle, subscribeInlineNodeToggle } from "./inline-events";

describe("inline node event bus", () => {
  it("notifies editor subscribers when a node toggles inline edit mode", () => {
    const listener = vi.fn();
    const unsubscribe = subscribeInlineNodeToggle(listener);

    emitInlineNodeToggle({ nodeId: "node-1", expanded: true });
    unsubscribe();
    emitInlineNodeToggle({ nodeId: "node-1", expanded: false });

    expect(listener).toHaveBeenCalledTimes(1);
    expect(listener).toHaveBeenCalledWith({ nodeId: "node-1", expanded: true });
  });
});
