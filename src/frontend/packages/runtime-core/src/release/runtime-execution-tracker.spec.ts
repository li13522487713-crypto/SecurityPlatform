import { describe, expect, it } from "vitest";
import {
  completeExecution,
  createExecution,
  getExecution,
  removeExecution
} from "./runtime-execution-tracker";

describe("runtime execution tracker", () => {
  it("创建并完成执行记录", () => {
    const executionId = "ut-exec-1";
    createExecution({
      executionId,
      appKey: "app-a",
      pageKey: "home"
    });

    expect(getExecution(executionId)?.status).toBe("running");

    completeExecution(executionId, "success");
    expect(getExecution(executionId)?.status).toBe("success");
    expect(getExecution(executionId)?.finishedAt).toBeTruthy();

    removeExecution(executionId);
    expect(getExecution(executionId)).toBeUndefined();
  });
});
