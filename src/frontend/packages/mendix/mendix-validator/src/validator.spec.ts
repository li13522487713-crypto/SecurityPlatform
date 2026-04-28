import { describe, expect, it } from "vitest";
import { parseExpression } from "@atlas/mendix-expression";
import type { LowCodeAppSchema } from "@atlas/mendix-schema";
import { validateLowCodeAppSchema } from "./index";

function createSchema(): LowCodeAppSchema {
  return {
    appId: "app",
    name: "demo",
    version: "1.0.0",
    modules: [
      {
        moduleId: "mod",
        name: "mod",
        domainModel: {
          entities: [
            {
              entityId: "ent",
              moduleId: "mod",
              name: "Entity",
              entityType: "persistable",
              attributes: [],
              associations: [],
              accessRules: [],
              validationRules: [],
              eventHandlers: [],
              systemMembers: { storeOwner: true, storeCreatedDate: true, storeChangedDate: true }
            }
          ],
          associations: [],
          enumerations: []
        },
        pages: [
          {
            pageId: "page1",
            moduleId: "mod",
            name: "Page1",
            pageType: "responsive",
            parameters: [],
            rootWidget: {
              widgetId: "root",
              widgetType: "container",
              props: {}
            },
            allowedRoles: []
          }
        ],
        microflows: [
          {
            microflowId: "mf1",
            moduleId: "mod",
            name: "MF1",
            parameters: [],
            returnType: { kind: "Boolean" },
            allowedRoles: [],
            applyEntityAccess: true,
            concurrentExecution: { allowConcurrentExecution: true },
            nodes: [
              { nodeId: "start", type: "startEvent", caption: "start", position: { x: 0, y: 0 } },
              {
                nodeId: "decision",
                type: "decision",
                caption: "decision",
                position: { x: 100, y: 0 },
                expression: parseExpression("100")
              },
              {
                nodeId: "end",
                type: "endEvent",
                caption: "end",
                position: { x: 200, y: 0 },
                returnExpression: parseExpression("100")
              }
            ],
            edges: []
          }
        ],
        workflows: [],
        enumerations: []
      }
    ],
    navigation: [{ itemId: "nav", caption: "nav", pageRef: { kind: "page", id: "missing_page" } }],
    security: {
      securityLevel: "prototype",
      userRoles: [],
      moduleRoles: [],
      pageAccessRules: [],
      microflowAccessRules: [],
      nanoflowAccessRules: [],
      entityAccessRules: [
        {
          ruleId: "rule1",
          roleRefs: [],
          memberAccess: []
        }
      ]
    },
    extensions: []
  };
}

describe("mendix-validator", () => {
  it("should report required validation errors", () => {
    const errors = validateLowCodeAppSchema(createSchema());
    expect(errors.some(error => error.code === "PAGE_ACCESS_ROLE_REQUIRED")).toBe(true);
    expect(errors.some(error => error.code === "DECISION_TYPE_INVALID")).toBe(true);
    expect(errors.some(error => error.code === "MICROFLOW_RETURN_TYPE_MISMATCH")).toBe(true);
    expect(errors.some(error => error.code === "ENTITY_ACCESS_MEMBER_REQUIRED")).toBe(true);
    expect(errors.some(error => error.code === "NAVIGATION_PAGE_NOT_FOUND")).toBe(true);
  });
});
