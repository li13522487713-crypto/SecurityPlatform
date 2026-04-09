import { describe, expect, it } from "vitest";
import { getActionHandler, hasActionHandler, registerActionHandler, unregisterActionHandler } from "./action-registry";
import type { RuntimeAction } from "./action-types";

describe("runtime-core action registry", () => {
  it("支持注册、查询、注销 handler", async () => {
    const type = "unit-test-action";
    const handler = async () => ({ success: true, message: "ok" });

    registerActionHandler(type, handler);
    expect(hasActionHandler(type)).toBe(true);
    expect(getActionHandler(type)).toBe(handler);

    const action: RuntimeAction = { type: "refresh", input: { target: "list" } };
    const result = await getActionHandler(type)?.(action, {}, { getContext: () => ({}) });
    expect(result?.success).toBe(true);

    unregisterActionHandler(type);
    expect(hasActionHandler(type)).toBe(false);
    expect(getActionHandler(type)).toBeUndefined();
  });
});
