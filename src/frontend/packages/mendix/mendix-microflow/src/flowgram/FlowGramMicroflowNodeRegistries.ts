import { injectable } from "inversify";
import {
  createNodeEntityDatas,
  type EntityManager,
  EntityManagerContribution,
  FlowDocumentContribution,
  FlowNodeFormData,
  FormModelV2,
  type WorkflowDocument,
  type WorkflowNodeEntity,
  type WorkflowNodeRegistry,
} from "@flowgram-adapter/free-layout-editor";

import { defaultMicroflowObjectNodeRegistry, objectKindFromRegistryItem } from "../node-registry";
import { flowGramPortsForObjectKind } from "./adapters/flowgram-port-factory";
import { getMendixMicroflowNodeSize } from "./flowgram-node-geometry";

export function createFlowGramMicroflowNodeRegistries(): WorkflowNodeRegistry[] {
  const registries = defaultMicroflowObjectNodeRegistry.map(item => {
    const kind = objectKindFromRegistryItem(item);
    const isEndLike = ["endEvent", "errorEvent", "breakEvent", "continueEvent"].includes(kind);
    const isStartLike = kind === "startEvent";
    const size = getMendixMicroflowNodeSize(kind);
    return ({
      type: kind,
      meta: {
        // StartEvent is the unique flow entry and should stay pinned.
        isStart: isStartLike,
        isNodeEnd: isEndLike,
        isContainer: kind === "loopedActivity",
        nodeDTOType: kind,
        size,
        deleteDisable: false,
        copyDisable: false,
        useDynamicPort: true,
        defaultPorts: flowGramPortsForObjectKind(kind),
      },
      formMeta: {
        render: () => null,
      },
    } as unknown as WorkflowNodeRegistry);
  });
  if (!registries.some(registry => registry.type === "actionActivity")) {
    registries.push({
      type: "actionActivity",
      meta: {
        isStart: false,
        isNodeEnd: false,
        isContainer: false,
        nodeDTOType: "actionActivity",
        size: getMendixMicroflowNodeSize("actionActivity"),
        deleteDisable: false,
        copyDisable: false,
        useDynamicPort: true,
        defaultPorts: flowGramPortsForObjectKind("actionActivity"),
      },
      formMeta: {
        render: () => null,
      },
    } as unknown as WorkflowNodeRegistry);
  }
  return registries;
}

@injectable()
export class FlowGramMicroflowNodeRegistryContribution
  implements EntityManagerContribution, FlowDocumentContribution<WorkflowDocument>
{
  registerDocument(document: WorkflowDocument): void {
    document.registerNodeDatas(...createNodeEntityDatas());
    for (const registry of createFlowGramMicroflowNodeRegistries()) {
      document.registerFlowNodes(registry);
    }
  }

  registerEntityManager(entityManager: EntityManager): void {
    entityManager.registerEntityData(
      FlowNodeFormData,
      (() => ({
        formModelFactory: (entity: WorkflowNodeEntity) => new FormModelV2(entity),
      })) as unknown as Parameters<EntityManager["registerEntityData"]>[1],
    );
  }
}
