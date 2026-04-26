import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";

export type { MicroflowValidationIssue };

export interface MicroflowValidator {
  validate(schema: MicroflowSchema): MicroflowValidationIssue[];
}
