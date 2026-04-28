import type { MicroflowValidationIssue } from "../../schema";
import { issuesForFlowGramEntity, validationStateFromIssues } from "../adapters/flowgram-validation-sync";

export function useFlowGramValidationSync(issues: MicroflowValidationIssue[]) {
  return {
    stateForEntity(id: string) {
      return validationStateFromIssues(issuesForFlowGramEntity(issues, id));
    },
  };
}

