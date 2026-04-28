import { describe, expect, it } from "vitest";

import {
  defaultMicroflowActionRegistry,
  microflowActionRegistryByActivityType
} from "./action-registry";
import { createDefaultActionConfig } from "./default-node-config";
import {
  defaultMicroflowNodeRegistry,
  microflowObjectNodeRegistries,
  microflowActionNodePanelRegistries
} from "./registry";

/**
 * 这些断言用来防止 toolbox 默认值再次回退到 mock。
 *
 * - 不能再出现 `Sales.*` / `Sales.Customer` / `Sales.Order` 等 mock 实体名。
 * - 不能再出现 `MF_ValidateOrder` / `MF_CreateInvoice` 等示例微流名。
 * - 每个画布对象节点和动作节点必须带 engineSupport。
 * - 7 个新节点（parallelGateway / inclusiveGateway / tryCatch / errorHandler /
 *   throwException / filterList / sortList）必须存在并具备明确的 engineSupport
 *   等级，以保证 toolbox 与后端 runtime 一致。
 */
describe("microflow toolbox cleanliness", () => {
  // 这两条断言的目标是禁止把示例数据（如 Sales.Order / MF_ValidateOrder）
  // 写进 toolbox 默认值。`MF_THROWN_EXCEPTION` 是 ThrowException 节点合法的错误
  // 码常量，与 mock 不冲突，所以白名单允许下划线后接 SNAKE_CASE 全大写常量。
  const MOCK_MICROFLOW_NAME_REGEX = /\bMF_[A-Z][a-zA-Z0-9]+\b/;

  it("never ships Sales.* or MF_<Name> mock identifiers in any registered node", () => {
    const allDefaults = [
      ...microflowObjectNodeRegistries.map(entry => entry.defaultConfig),
      ...microflowActionNodePanelRegistries.map(entry => entry.defaultConfig),
      ...defaultMicroflowActionRegistry.map(entry => entry.defaultConfig)
    ];
    const serialized = JSON.stringify(allDefaults);
    expect(serialized).not.toMatch(/Sales\./);
    expect(serialized).not.toMatch(MOCK_MICROFLOW_NAME_REGEX);
  });

  it("createDefaultActionConfig never returns Sales.* / mock MF_<Name> placeholders", () => {
    const actionKinds = [
      "retrieve",
      "createObject",
      "changeMembers",
      "commit",
      "delete",
      "rollback",
      "createList",
      "changeList",
      "aggregateList",
      "filterList",
      "sortList",
      "createVariable",
      "changeVariable",
      "callMicroflow",
      "restCall",
      "logMessage",
      "throwException"
    ] as const;
    for (const kind of actionKinds) {
      const config = JSON.stringify(createDefaultActionConfig(kind));
      expect(config, `${kind} default leaked Sales.*`).not.toMatch(/Sales\./);
      expect(config, `${kind} default leaked mock MF_<Name>`).not.toMatch(MOCK_MICROFLOW_NAME_REGEX);
    }
  });

  it("every node registry entry exposes an engineSupport descriptor", () => {
    for (const entry of defaultMicroflowNodeRegistry) {
      expect(entry.engineSupport, `${entry.title} missing engineSupport`).toBeTruthy();
      expect(["supported", "partial", "unsupported"]).toContain(entry.engineSupport.level);
    }
  });

  it("ships parallel/inclusive gateway and tryCatch / errorHandler nodes as modeling-only", () => {
    const expected: Array<{ key: string; level: "partial" | "unsupported" }> = [
      { key: "parallelGateway", level: "unsupported" },
      { key: "inclusiveGateway", level: "unsupported" },
      { key: "tryCatch", level: "unsupported" },
      { key: "errorHandler", level: "partial" }
    ];
    for (const expectation of expected) {
      const entry = microflowObjectNodeRegistries.find(item => item.type === expectation.key);
      expect(entry, `${expectation.key} should be registered as object node`).toBeTruthy();
      expect(entry?.engineSupport.level).toBe(expectation.level);
    }
  });

  it("registers throwException / filterList / sortList as supported actions with executor mapping", () => {
    for (const kind of ["throwException", "filterList", "sortList"] as const) {
      const action = defaultMicroflowActionRegistry.find(item => item.kind === kind);
      expect(action, `${kind} action should be registered`).toBeTruthy();
      expect(action?.runtimeSupportLevel).toBe("supported");
    }
    // The legacy activity type bridge is what lets the canvas roundtrip these actions.
    expect(microflowActionRegistryByActivityType.get("throwException")?.kind).toBe("throwException");
    expect(microflowActionRegistryByActivityType.get("listFilter")?.kind).toBe("filterList");
    expect(microflowActionRegistryByActivityType.get("listSort")?.kind).toBe("sortList");
  });

  it("registers all 30+ user-spec toolbox entries in defaultMicroflowNodeRegistry", () => {
    // Object-shaped (gateway / event / decision / annotation / etc.) keys equal
    // their `type`; action panel entries use `activity:<legacyActivityType>`.
    const requiredKeys = [
      "startEvent",
      "endEvent",
      "errorEvent",
      "breakEvent",
      "continueEvent",
      "decision",
      "objectTypeDecision",
      "merge",
      "loop",
      "parameter",
      "annotation",
      "parallelGateway",
      "inclusiveGateway",
      "tryCatch",
      "errorHandler",
      "activity:objectRetrieve",
      "activity:objectCreate",
      "activity:objectChange",
      "activity:objectCommit",
      "activity:objectDelete",
      "activity:objectRollback",
      "activity:listCreate",
      "activity:listChange",
      "activity:listAggregate",
      "activity:listFilter",
      "activity:listSort",
      "activity:variableCreate",
      "activity:variableChange",
      "activity:callMicroflow",
      "activity:callRest",
      "activity:logMessage",
      "activity:throwException"
    ];
    const presentKeys = new Set(defaultMicroflowNodeRegistry.map(item => item.key ?? item.id ?? item.type));
    const missing = requiredKeys.filter(key => !presentKeys.has(key));
    expect(missing, `Missing toolbox entries: ${missing.join(", ")}`).toEqual([]);
  });
});
