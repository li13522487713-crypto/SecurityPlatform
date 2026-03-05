import { describe, it, expect } from "vitest";
import { ref } from "vue";
import type { Ref } from "vue";
import { useWorkflowSerializer } from "@/composables/useWorkflowSerializer";
import type { StepTypeMetadata } from "@/types/api";
import type { Graph } from "@antv/x6";

// 测试骨架：仅测试纯函数部分，图实例依赖通过 undefined 模拟
describe("useWorkflowSerializer", () => {
  const graphRef = ref(undefined) as Ref<Graph | undefined>;
  const stepTypes = ref<StepTypeMetadata[]>([]);
  const workflowId = ref<string>("");

  it("should be constructable without throwing", () => {
    expect(() => {
      useWorkflowSerializer(graphRef, stepTypes, workflowId);
    }).not.toThrow();
  });

  it("getLinearPathOrThrow should throw when graph has no start edge", () => {
    const { getLinearPathOrThrow } = useWorkflowSerializer(graphRef, stepTypes, workflowId);
    const mockGraph = {
      getNodes: () => [],
      getEdges: () => []
    } as unknown as Graph;
    expect(() => getLinearPathOrThrow(mockGraph)).toThrow();
  });

  it("buildTestDataTemplate should return an object", () => {
    const { buildTestDataTemplate } = useWorkflowSerializer(graphRef, stepTypes, workflowId);
    const result = buildTestDataTemplate();
    expect(typeof result).toBe("object");
    expect(result).not.toBeNull();
    expect(Array.isArray(result)).toBe(false);
  });
});
