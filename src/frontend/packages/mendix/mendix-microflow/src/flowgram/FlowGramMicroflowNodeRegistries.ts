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
  return defaultMicroflowObjectNodeRegistry.map(item => {
    const kind = objectKindFromRegistryItem(item);
    const isEndLike = ["endEvent", "errorEvent", "breakEvent", "continueEvent"].includes(kind);
    return ({
      type: kind,
      meta: {
        isStart: kind === "startEvent",
        isNodeEnd: isEndLike,
        isContainer: kind === "loopedActivity",
        nodeDTOType: kind,
        size: { width: item.render.width, height: item.render.height },
        deleteDisable: kind === "startEvent",
        copyDisable: kind === "startEvent",
        useDynamicPort: true,
        defaultPorts: flowGramPortsForObjectKind(kind),
      },
      formMeta: {
        render: () => null,
      },
    } as unknown as WorkflowNodeRegistry);
  });
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
