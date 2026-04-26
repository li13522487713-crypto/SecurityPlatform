import { describe, expect, it } from "vitest";
import type { LowCodeAppSchema } from "@atlas/mendix-schema";
import { createRuntimeExecutor } from "./executor";

const APP: LowCodeAppSchema = {
  appId: "app",
  name: "app",
  version: "1.0.0",
  modules: [
    {
      moduleId: "mod",
      name: "mod",
      domainModel: { entities: [], associations: [], enumerations: [] },
      pages: [],
      microflows: [
        {
          microflowId: "mf_submit_purchase_request",
          moduleId: "mod",
          name: "MF_SubmitPurchaseRequest",
          parameters: [],
          returnType: { kind: "Nothing" },
          allowedRoles: [],
          applyEntityAccess: true,
          concurrentExecution: { allowConcurrentExecution: true },
          nodes: [],
          edges: []
        }
      ],
      workflows: [],
      enumerations: []
    }
  ],
  navigation: [],
  security: {
    securityLevel: "off",
    userRoles: [],
    moduleRoles: [],
    pageAccessRules: [],
    microflowAccessRules: [],
    nanoflowAccessRules: [],
    entityAccessRules: []
  },
  extensions: []
};

describe("mendix-runtime executor", () => {
  it("should execute submit purchase request and set status", () => {
    const executor = createRuntimeExecutor();
    const objectState: Record<string, unknown> = {
      Amount: 80000,
      Status: "Draft",
      Reason: "采购服务器"
    };
    const response = executor.executeAction(
      {
        actionType: "callMicroflow",
        microflowRef: { kind: "microflow", id: "mf_submit_purchase_request" },
        arguments: [{ name: "Request", value: objectState }]
      },
      {
        app: APP,
        pageId: "page_purchase_request_edit",
        objectState
      }
    );
    expect(response.success).toBe(true);
    expect(objectState.Status).toBe("NeedFinanceApproval");
    expect(response.traceId).toBeTruthy();
    expect(response.uiCommands.some(command => command.type === "refreshObject")).toBe(true);
  });
});
