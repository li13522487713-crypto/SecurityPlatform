import type { MicroflowAuthoringSchema, MicroflowValidationIssue } from "../schema/types";
import type { MicroflowMetadataCatalog } from "../metadata";
import type { MicroflowVariableIndex } from "../variables";
import type { MicroflowValidationCode } from "./validation-codes";

export type { MicroflowValidationIssue };
export type { MicroflowValidationCode };

export interface MicroflowValidator {
  validate(schema: MicroflowAuthoringSchema): MicroflowValidationIssue[];
}

export interface MicroflowValidationOptions {
  mode?: "edit" | "save" | "publish" | "testRun";
  includeInfo?: boolean;
  includeWarnings?: boolean;
}

export interface MicroflowValidationInput {
  schema: MicroflowAuthoringSchema;
  metadata?: MicroflowMetadataCatalog;
  variableIndex?: MicroflowVariableIndex;
  options?: MicroflowValidationOptions;
}

export interface MicroflowValidationSummary {
  errorCount: number;
  warningCount: number;
  infoCount: number;
}

export interface MicroflowValidationResult {
  issues: MicroflowValidationIssue[];
  variableIndex: MicroflowVariableIndex;
  summary: MicroflowValidationSummary;
}
