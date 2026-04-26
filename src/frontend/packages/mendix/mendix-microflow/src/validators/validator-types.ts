import type { MicroflowAuthoringSchema, MicroflowValidationIssue } from "../schema/types";
import type { MicroflowMetadataCatalog } from "../metadata";
import type { MicroflowVariableIndex } from "../variables";
import type { MicroflowValidationCode } from "./validation-codes";

export type { MicroflowValidationIssue };
export type { MicroflowValidationCode };

export interface MicroflowValidatorContext {
  metadata: MicroflowMetadataCatalog;
  variableIndex: MicroflowVariableIndex;
}

export interface MicroflowValidator {
  validate(schema: MicroflowAuthoringSchema, context: MicroflowValidatorContext): MicroflowValidationIssue[];
}

export interface MicroflowValidationOptions {
  mode?: "edit" | "save" | "publish" | "testRun";
  includeInfo?: boolean;
  includeWarnings?: boolean;
}

export interface MicroflowValidationInput {
  schema: MicroflowAuthoringSchema;
  /**
   * 必须为已加载的目录；`null` / `undefined` 时返回 `MF_METADATA_CATALOG_MISSING`，不回落到 mock。
   */
  metadata: MicroflowMetadataCatalog | null | undefined;
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
