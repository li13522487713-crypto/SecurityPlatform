/**
 * 工作流序列化 Composable
 * 包含拓扑校验（getLinearPathOrThrow）、JSON 序列化（getDefinitionJson）、测试数据模板（buildTestDataTemplate）
 */
import type { Ref } from "vue";
import type { Graph } from "@antv/x6";
import type { StepTypeMetadata } from "@/types/api";
import { WORKFLOW_START_NODE_ID, WORKFLOW_END_NODE_ID } from "@/constants/workflow";
import {
  formatLocalIsoSeconds,
  normalizeTimeSpanInput,
  normalizeDateTimeInput
} from "./useTimeNormalize";

export function useWorkflowSerializer(
  graphRef: Ref<Graph | undefined>,
  stepTypes: Ref<StepTypeMetadata[]>,
  workflowId: Ref<string>
) {
  function getLinearPathOrThrow(graph: Graph): string[] {
    const stepNodes = graph
      .getNodes()
      .filter((n) => n.id !== WORKFLOW_START_NODE_ID && n.id !== WORKFLOW_END_NODE_ID);
    const stepTypeMap = new Map<string, string>();
    stepNodes.forEach((n) => {
      const data = n.getData() as Record<string, unknown>;
      stepTypeMap.set(n.id, String(data.stepType ?? ""));
    });
    const stepNodeIds = new Set(stepNodes.map((n) => n.id));

    const edges = graph.getEdges();
    const outgoing = new Map<string, string[]>();
    const incoming = new Map<string, number>();

    for (const edge of edges) {
      const sourceId = edge.getSourceCellId();
      const targetId = edge.getTargetCellId();
      if (!sourceId || !targetId) continue;
      if (!outgoing.has(sourceId)) outgoing.set(sourceId, []);
      outgoing.get(sourceId)!.push(targetId);
      incoming.set(targetId, (incoming.get(targetId) ?? 0) + 1);
    }

    const startOut = outgoing.get(WORKFLOW_START_NODE_ID) ?? [];
    if (startOut.length === 0) {
      throw new Error("请从'开始'节点连线到第一个步骤节点");
    }
    if (startOut.length > 1) {
      throw new Error("当前后端仅支持顺序流程（NextStepId），开始节点只能连出一条线");
    }

    for (const id of stepNodeIds) {
      const inCount = incoming.get(id) ?? 0;
      if (inCount > 1) {
        throw new Error("当前后端仅支持顺序流程（NextStepId），每个步骤节点最多只能有一条入线");
      }
      const outCount = (outgoing.get(id) ?? []).length;
      const stepType = stepTypeMap.get(id);
      if (stepType === "If") {
        if (outCount > 2) {
          throw new Error("If 节点最多只能连出两条线（True/False）");
        }
      } else if (outCount > 1) {
        throw new Error("当前后端仅支持顺序流程（NextStepId），每个步骤节点最多只能连出一条线");
      }
    }

    const endIn = incoming.get(WORKFLOW_END_NODE_ID) ?? 0;
    if (endIn === 0) {
      throw new Error("请将最后一个步骤节点连线到'结束'节点");
    }
    if (endIn > 1) {
      throw new Error("当前后端仅支持顺序流程（NextStepId），'结束'节点只能有一条入线");
    }

    const reachable = new Set<string>();
    const stack = [...startOut];
    while (stack.length > 0) {
      const currentId = stack.pop()!;
      if (reachable.has(currentId)) {
        continue;
      }
      reachable.add(currentId);
      const outs = outgoing.get(currentId) ?? [];
      outs.forEach((nextId) => stack.push(nextId));
    }
    for (const id of stepNodeIds) {
      if (!reachable.has(id)) {
        throw new Error("存在未接入主链路的步骤节点：请确保所有步骤都可从“开始”到达");
      }
    }

    const path: string[] = [];
    const visited = new Set<string>();
    let current = startOut[0];
    while (true) {
      if (current === WORKFLOW_END_NODE_ID) {
        break;
      }
      if (!stepNodeIds.has(current)) {
        throw new Error(
          "检测到无效连线：请确保'开始'只能连接到步骤节点，且步骤节点最终连接到'结束'"
        );
      }
      if (visited.has(current)) {
        throw new Error("检测到循环连线：当前后端仅支持无环的顺序流程");
      }
      visited.add(current);
      path.push(current);
      const outs = outgoing.get(current) ?? [];
      if (outs.length === 0) {
        throw new Error("流程未完整闭合：请将每个步骤节点连线到下一个节点或'结束'");
      }
      current = outs[0];
    }

    return path;
  }

  function getDefinitionJson(): string {
    if (!graphRef.value) return "";

    const graph = graphRef.value;
    const path = getLinearPathOrThrow(graph);
    const stepNodes = graph
      .getNodes()
      .filter((n) => n.id !== WORKFLOW_START_NODE_ID && n.id !== WORKFLOW_END_NODE_ID);
    const unsupportedSteps = stepNodes.filter((node) => {
      const data = node.getData() as Record<string, unknown>;
      const meta = stepTypes.value.find((st) => st.type === data.stepType);
      return meta?.supported === false;
    });
    if (unsupportedSteps.length > 0) {
      const labels = unsupportedSteps.map((node) => {
        const data = node.getData() as Record<string, unknown>;
        return String(data.name ?? data.stepType ?? node.id);
      });
      throw new Error(`存在“规划中”节点，暂不支持发布：${labels.join("、")}`);
    }

    const outgoing = new Map<string, string[]>();
    for (const edge of graph.getEdges()) {
      const sourceId = edge.getSourceCellId();
      const targetId = edge.getTargetCellId();
      if (!sourceId || !targetId) continue;
      if (!outgoing.has(sourceId)) outgoing.set(sourceId, []);
      outgoing.get(sourceId)!.push(targetId);
    }

    const nodes = path.map((nodeId) => {
      const node = graph.getCellById(nodeId);
      if (!node || !node.isNode()) {
        throw new Error("步骤节点不存在，请刷新后重试");
      }
      const data = node.getData() as Record<string, unknown>;
      const stepMeta = stepTypes.value.find((st) => st.type === data.stepType);
      const inputs: Record<string, unknown> = { ...((data.inputs as Record<string, unknown>) || {}) };
      stepMeta?.parameters?.forEach((p) => {
        const raw = inputs[p.name];
        if (p.type === "timespan") {
          inputs[p.name] = normalizeTimeSpanInput(raw);
        } else if (p.type === "datetime") {
          inputs[p.name] = normalizeDateTimeInput(raw);
        }
      });

      const outs = outgoing.get(nodeId) ?? [];
      const nextTargets = outs.filter((target) => target !== WORKFLOW_END_NODE_ID);
      const nextStepId = nextTargets[0] ?? null;
      const falseStepId = data.stepType === "If" ? (nextTargets[1] ?? null) : null;

      return {
        Id: data.id,
        Name: data.name,
        StepType: data.stepType,
        Inputs: inputs,
        NextStepId: nextStepId,
        FalseStepId: falseStepId
      };
    });

    const definition = {
      Id: workflowId.value,
      Version: 1,
      Steps: nodes
    };

    return JSON.stringify(definition);
  }

  function buildTestDataTemplate(): Record<string, unknown> {
    const template: Record<string, unknown> = {
      now: formatLocalIsoSeconds(new Date())
    };

    const nodes =
      graphRef.value
        ?.getNodes()
        .filter((n) => n.id !== WORKFLOW_START_NODE_ID && n.id !== WORKFLOW_END_NODE_ID) ?? [];
    const usedParamTypes = new Map<string, string>();

    for (const node of nodes) {
      const data = node.getData() as Record<string, unknown>;
      const meta = stepTypes.value.find((st) => st.type === data.stepType);
      for (const p of meta?.parameters ?? []) {
        if (!usedParamTypes.has(p.name)) {
          usedParamTypes.set(p.name, p.type);
        }
      }
    }

    for (const [name, type] of usedParamTypes.entries()) {
      if (name in template) continue;
      if (type === "timespan") {
        template[name] = "00:00:05";
      } else if (type === "datetime") {
        template[name] = formatLocalIsoSeconds(new Date());
      } else if (type === "bool") {
        template[name] = false;
      } else if (type === "int") {
        template[name] = 0;
      } else if (type === "array") {
        template[name] = [];
      } else {
        template[name] = "";
      }
    }

    return template;
  }

  return { getLinearPathOrThrow, getDefinitionJson, buildTestDataTemplate };
}


