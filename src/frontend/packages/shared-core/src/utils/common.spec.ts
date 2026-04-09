import { describe, expect, it } from "vitest";
import { addDateRange, formatDateTime, handleTree, loadSelectOptions } from "./common";

describe("shared-core common utils", () => {
  it("formatDateTime 在合法时间字符串时输出 yyyy-MM-dd HH:mm:ss", () => {
    const output = formatDateTime("2026-04-09T08:07:06Z");
    expect(output).toMatch(/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$/);
  });

  it("formatDateTime 在非法时间字符串时回退原值", () => {
    expect(formatDateTime("not-a-date")).toBe("not-a-date");
  });

  it("handleTree 可按 parentId 组装树结构", () => {
    const tree = handleTree([
      { id: "1", parentId: "0", name: "root" },
      { id: "2", parentId: "1", name: "child" }
    ]);

    expect(tree).toHaveLength(1);
    expect((tree[0] as { children?: Array<{ id: string }> }).children?.[0]?.id).toBe("2");
  });

  it("addDateRange 默认写入 beginTime/endTime", () => {
    const result = addDateRange({}, ["2026-04-01", "2026-04-09"]);
    expect(result.params?.beginTime).toBe("2026-04-01");
    expect(result.params?.endTime).toBe("2026-04-09");
  });

  it("loadSelectOptions 在 fetch 抛错时返回空数组", async () => {
    const result = await loadSelectOptions({
      fetcher: async () => {
        throw new Error("boom");
      },
      mapItem: () => ({ label: "x", value: "x" })
    });

    expect(result).toEqual([]);
  });
});
