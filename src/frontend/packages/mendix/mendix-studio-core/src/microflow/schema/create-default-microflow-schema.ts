import {
  createMicroflowDesignSchema,
  type MicroflowDesignSchema,
} from "@atlas/microflow";

import type { MicroflowCreateInput } from "../resource/resource-types";

export interface CreateDefaultMicroflowSchemaInput extends MicroflowCreateInput {
  id?: string;
  ownerName?: string;
}

function makeId(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

export function createDefaultMicroflowSchema(input: CreateDefaultMicroflowSchemaInput): MicroflowDesignSchema {
  const id = input.id || makeId("mf");
  return createMicroflowDesignSchema({
    id,
    name: input.name,
    displayName: input.displayName,
    description: input.description,
    moduleId: input.moduleId,
    moduleName: input.moduleName,
    parameters: input.parameters,
    returnType: input.returnType,
    returnVariableName: input.returnVariableName,
    ownerName: input.ownerName,
  });
}
