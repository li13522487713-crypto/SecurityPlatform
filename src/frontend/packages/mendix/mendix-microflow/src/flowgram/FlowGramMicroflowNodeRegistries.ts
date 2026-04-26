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

import type { MicroflowObjectKind } from "../schema";

const objectKinds: MicroflowObjectKind[] = [
  "startEvent",
  "endEvent",
  "errorEvent",
  "breakEvent",
  "continueEvent",
  "exclusiveSplit",
  "inheritanceSplit",
  "exclusiveMerge",
  "actionActivity",
  "loopedActivity",
  "parameterObject",
  "annotation",
];

function fallbackPorts(kind: MicroflowObjectKind): WorkflowNodeRegistry["meta"]["defaultPorts"] {
  if (kind === "startEvent") {
    return [{ type: "output", portID: "out" }];
  }
  if (kind === "endEvent" || kind === "errorEvent" || kind === "breakEvent" || kind === "continueEvent") {
    return [{ type: "input", portID: "in" }];
  }
  if (kind === "annotation" || kind === "parameterObject") {
    return [];
  }
  return [{ type: "input", portID: "in" }, { type: "output", portID: "out" }];
}

export function createFlowGramMicroflowNodeRegistries(): WorkflowNodeRegistry[] {
  return objectKinds.map(kind => ({
    type: kind,
    meta: {
      nodeDTOType: kind,
      size: kind === "annotation" ? { width: 240, height: 120 } : { width: 220, height: 92 },
      useDynamicPort: true,
      defaultPorts: fallbackPorts(kind),
    },
    formMeta: {
      render: () => null,
    },
  }));
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
      () => ({
        formModelFactory: (entity: WorkflowNodeEntity) => new FormModelV2(entity),
      }),
    );
  }
}

