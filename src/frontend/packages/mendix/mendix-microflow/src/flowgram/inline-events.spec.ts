import { afterEach, describe, expect, it, vi } from "vitest";

import { emitInlineNodeToggle, MICROFLOW_INLINE_NODE_TOGGLE_EVENT, subscribeInlineNodeToggle } from "./inline-events";

afterEach(() => {
  vi.unstubAllGlobals();
});

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

  it("also dispatches a DOM event so renderer/editor module instances stay connected", () => {
    const target = new EventTarget();
    class TestCustomEvent<T = unknown> extends Event {
      detail: T;

      constructor(type: string, init?: CustomEventInit<T>) {
        super(type);
        this.detail = init?.detail as T;
      }
    }
    vi.stubGlobal("window", target);
    vi.stubGlobal("CustomEvent", TestCustomEvent);
    const listener = vi.fn();
    window.addEventListener(MICROFLOW_INLINE_NODE_TOGGLE_EVENT, listener);

    emitInlineNodeToggle({ nodeId: "node-1", expanded: true });
    window.removeEventListener(MICROFLOW_INLINE_NODE_TOGGLE_EVENT, listener);

    expect(listener).toHaveBeenCalledTimes(1);
    expect((listener.mock.calls[0]?.[0] as CustomEvent).detail).toEqual({ nodeId: "node-1", expanded: true });
  });
});
