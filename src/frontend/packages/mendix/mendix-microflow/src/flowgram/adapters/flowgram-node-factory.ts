import type { WorkflowNodeJSON } from "@flowgram-adapter/free-layout-editor";

import type { MicroflowObject } from "../../schema";
import { microflowPortsToFlowGramPorts } from "./flowgram-port-factory";

export function createFlowGramNodeFromObject(
  object: MicroflowObject,
  ports: ReturnType<typeof microflowPortsToFlowGramPorts>,
): WorkflowNodeJSON {
  return {
    id: object.id,
    type: object.kind,
    data: {
      objectId: object.id,
      objectKind: object.kind,
      title: object.caption ?? object.id,
      officialType: object.officialType,
      disabled: Boolean(object.disabled),
      validationState: "valid",
      runtimeState: "idle",
      issueCount: 0,
    },
    meta: {
      position: object.relativeMiddlePoint,
      size: object.size,
      nodeDTOType: object.kind,
      useDynamicPort: true,
      defaultPorts: ports,
    },
  };
}

