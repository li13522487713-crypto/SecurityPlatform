import { describe, expect, it } from "vitest";
import { buildQueryFromBinding } from "./runtime-query-builder";
import type { ListBinding } from "./binding-types";

describe("runtime-core query builder", () => {
  it("根据 ListBinding 生成 URL 与分页参数", () => {
    const binding: ListBinding = {
      kind: "list",
      key: "users",
      entityKey: "user",
      apiUrl: "/api/users",
      pageSize: 50
    };

    const resolved = buildQueryFromBinding(binding);
    expect(resolved.url).toBe("/api/users");
    expect(resolved.params).toEqual({ pageSize: "50" });
  });

  it("pageSize 未配置时不输出分页参数", () => {
    const binding: ListBinding = {
      kind: "list",
      key: "users",
      entityKey: "user",
      apiUrl: "/api/users"
    };

    const resolved = buildQueryFromBinding(binding);
    expect(resolved.params).toEqual({});
  });
});
