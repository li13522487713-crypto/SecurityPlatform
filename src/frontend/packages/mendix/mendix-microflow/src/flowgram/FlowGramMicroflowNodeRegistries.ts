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

export function createFlowGramMicroflowNodeRegistries(): WorkflowNodeRegistry[] {
  const registries = defaultMicroflowObjectNodeRegistry.map(item => {
    const kind = objectKindFromRegistryItem(item);
    const isEndLike = ["endEvent", "errorEvent", "breakEvent", "continueEvent"].includes(kind);
    return ({
      type: kind,
      meta: {
        // Keep Start as business data only. FlowGram's special start-node flag
        // pins the node in free-layout interactions and blocks single-node drag.
        isStart: false,
        isNodeEnd: isEndLike,
        isContainer: kind === "loopedActivity",
        nodeDTOType: kind,
        size: { width: item.render.width, height: item.render.height },
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
        size: { width: 178, height: 76 },
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
