export type TestRunValidationIssueCode =
  | "invalidJson"
  | "invalidPayload"
  | "userInputRequired"
  | "conversationNameRequired"
  | "conversationNameTooLong";

export interface TestRunValidationResult {
  payload: Record<string, unknown>;
  issues: TestRunValidationIssueCode[];
}

export function validateTestRunPayload(inputJson: string, workflowMode?: "workflow" | "chatflow"): TestRunValidationResult {
  if (!inputJson.trim()) {
    return buildModeIssues({}, workflowMode);
  }

  try {
    const parsed = JSON.parse(inputJson) as unknown;
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
      return {
        payload: {},
        issues: ["invalidPayload"]
      };
    }

    return buildModeIssues(parsed as Record<string, unknown>, workflowMode);
  } catch {
    return {
      payload: {},
      issues: ["invalidJson"]
    };
  }
}

function buildModeIssues(payload: Record<string, unknown>, workflowMode?: "workflow" | "chatflow"): TestRunValidationResult {
  const issues: TestRunValidationIssueCode[] = [];
  if (workflowMode === "chatflow") {
    const userInput = typeof payload.USER_INPUT === "string" ? payload.USER_INPUT.trim() : "";
    const conversationName = typeof payload.CONVERSATION_NAME === "string" ? payload.CONVERSATION_NAME.trim() : "";

    if (!userInput) {
      issues.push("userInputRequired");
    }
    if (!conversationName) {
      issues.push("conversationNameRequired");
    } else if (conversationName.length > 100) {
      issues.push("conversationNameTooLong");
    }
  }

  return {
    payload,
    issues
  };
}
