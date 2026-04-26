import type { MicroflowSchema, MicroflowValidationIssue, MicroflowValidator } from "../schema/types";
import { validateActions } from "./validate-actions";
import { validateDecisions } from "./validate-decisions";
import { validateErrorHandling } from "./validate-error-handling";
import { validateEvents } from "./validate-events";
import { validateExpressions } from "./validate-expressions";
import { validateFlows } from "./validate-flows";
import { validateLoop } from "./validate-loop";
import { validateMetadataReferences } from "./validate-metadata-references";
import { validateObjectCollection } from "./validate-object-collection";
import { validateReachability } from "./validate-reachability";
import { validateRoot } from "./validate-root";
import { validateVariables } from "./validate-variables";

export function validateMicroflowSchema(schema: MicroflowSchema): MicroflowValidationIssue[] {
  const validators: MicroflowValidator[] = [
    { validate: validateRoot },
    { validate: validateObjectCollection },
    { validate: validateFlows },
    { validate: validateEvents },
    { validate: validateDecisions },
    { validate: validateLoop },
    { validate: validateActions },
    { validate: validateMetadataReferences },
    { validate: validateVariables },
    { validate: validateExpressions },
    { validate: validateErrorHandling },
    { validate: validateReachability }
  ];
  return validators.flatMap(validator => validator.validate(schema));
}
export type { MicroflowValidationIssue, MicroflowValidator } from "./validator-types";
