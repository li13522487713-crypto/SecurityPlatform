import { describe, expect, it, beforeEach } from "vitest";
import {
  microflowNodeFormRegistry,
  registerMicroflowNodeForm,
  unregisterMicroflowNodeForm,
  getMicroflowNodeFormForObject,
  getMicroflowNodeFormKey,
} from "./node-form-registry";

describe("microflowNodeFormRegistry (P1-1)", () => {
  beforeEach(() => {
    for (const key of Object.keys(microflowNodeFormRegistry)) {
      delete microflowNodeFormRegistry[key];
    }
  });

  it("getMicroflowNodeFormKey for non-action object uses kind", () => {
    expect(getMicroflowNodeFormKey({ kind: "loopedActivity" } as never)).toBe("loopedActivity");
  });

  it("getMicroflowNodeFormKey for actionActivity uses activity:<actionKind>", () => {
    expect(
      getMicroflowNodeFormKey({
        kind: "actionActivity",
        action: { kind: "callMicroflow" },
      } as never),
    ).toBe("activity:callMicroflow");
  });

  it("registerMicroflowNodeForm rejects empty key", () => {
    expect(() =>
      registerMicroflowNodeForm("", {
        tabs: ["properties"],
        renderProperties: () => null as unknown as JSX.Element,
      }),
    ).toThrow(/key/);
  });

  it("registerMicroflowNodeForm rejects without renderProperties", () => {
    expect(() =>
      registerMicroflowNodeForm("activity:custom", {
        tabs: ["properties"],
        renderProperties: undefined as unknown as () => JSX.Element,
      }),
    ).toThrow(/renderProperties/);
  });

  it("registerMicroflowNodeForm forbids overwriting an existing key by default", () => {
    const item = {
      tabs: ["properties"] as const,
      renderProperties: () => null as unknown as JSX.Element,
    };
    registerMicroflowNodeForm("activity:custom", { ...item });
    expect(() => registerMicroflowNodeForm("activity:custom", { ...item })).toThrow(/already registered/);
  });

  it("allowOverride=true replaces an existing item; unregister removes it", () => {
    const first = { tabs: ["properties"] as const, renderProperties: () => null as unknown as JSX.Element };
    const second = { tabs: ["properties"] as const, renderProperties: () => null as unknown as JSX.Element };
    registerMicroflowNodeForm("activity:custom", { ...first });
    registerMicroflowNodeForm("activity:custom", { ...second }, { allowOverride: true });
    expect(microflowNodeFormRegistry["activity:custom"]).toBe(microflowNodeFormRegistry["activity:custom"]);
    unregisterMicroflowNodeForm("activity:custom");
    expect(microflowNodeFormRegistry["activity:custom"]).toBeUndefined();
  });

  it("getMicroflowNodeFormForObject resolves by kind/actionKind", () => {
    const item = {
      tabs: ["properties"] as const,
      renderProperties: () => null as unknown as JSX.Element,
    };
    registerMicroflowNodeForm("loopedActivity", { ...item });
    expect(getMicroflowNodeFormForObject({ kind: "loopedActivity" } as never)).toBeDefined();

    registerMicroflowNodeForm("activity:callMicroflow", { ...item });
    expect(
      getMicroflowNodeFormForObject({ kind: "actionActivity", action: { kind: "callMicroflow" } } as never),
    ).toBeDefined();

    expect(
      getMicroflowNodeFormForObject({ kind: "actionActivity", action: { kind: "logMessage" } } as never),
    ).toBeUndefined();
  });
});
