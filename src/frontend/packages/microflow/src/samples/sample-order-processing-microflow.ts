import type { MicroflowAuthoringSchema } from "../schema/types";
import { sampleMicroflowSchema } from "../schema/sample";

export const sampleOrderProcessingMicroflow: MicroflowAuthoringSchema = {
  schemaVersion: sampleMicroflowSchema.schemaVersion,
  mendixProfile: sampleMicroflowSchema.mendixProfile,
  id: sampleMicroflowSchema.id,
  stableId: sampleMicroflowSchema.stableId,
  name: sampleMicroflowSchema.name,
  displayName: sampleMicroflowSchema.displayName,
  description: sampleMicroflowSchema.description,
  documentation: sampleMicroflowSchema.documentation,
  moduleId: sampleMicroflowSchema.moduleId,
  moduleName: sampleMicroflowSchema.moduleName,
  parameters: sampleMicroflowSchema.parameters,
  returnType: sampleMicroflowSchema.returnType,
  returnVariableName: sampleMicroflowSchema.returnVariableName,
  objectCollection: sampleMicroflowSchema.objectCollection,
  flows: sampleMicroflowSchema.flows,
  security: sampleMicroflowSchema.security,
  concurrency: sampleMicroflowSchema.concurrency,
  exposure: sampleMicroflowSchema.exposure,
  variables: sampleMicroflowSchema.variableIndex,
  validation: sampleMicroflowSchema.validation,
  editor: sampleMicroflowSchema.editor,
  audit: sampleMicroflowSchema.audit
};
