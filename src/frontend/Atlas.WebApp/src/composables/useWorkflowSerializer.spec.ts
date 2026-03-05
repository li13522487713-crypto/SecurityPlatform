import { describe, it, expect } from "vitest";
import { ref } from "vue";
import { useWorkflowSerializer } from "@/composables/useWorkflowSerializer";
import type { StepTypeMetadata } from "@/types/api";

// 测试骨架：仅测试纯函数部分，图实例依赖通过 null 模拟
describe("useWorkflowSerializer", () => {
  const graphRef = ref(null);
  const stepTypes = ref<StepTypeMetadata[]>([]);
  const workflowId = ref<string | undefined>(undefined);

  it("should be constructable without throwing", () => {
    expect(() => {
      useWorkflowSerializer(graphRef, stepTypes, workflowId);
    }).not.toThrow();
  });

  it("getLinearPathOrThrow should throw when graph is null", () => {
    const { getLinearPathOrThrow } = useWorkflowSerializer(graphRef, stepTypes, workflowId);
    expect(() => getLinearPathOrThrow()).toThrow();
  });

  it("buildTestDataTemplate should return an object", () => {
    const { buildTestDataTemplate } = useWorkflowSerializer(graphRef, stepTypes, workflowId);
    const result = buildTestDataTemplate();
    expect(typeof result).toBe("object");
    expect(result).not.toBeNull();
    expect(Array.isArray(result)).toBe(false);
  });
});
