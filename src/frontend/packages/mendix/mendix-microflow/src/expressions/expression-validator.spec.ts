import { describe, expect, it } from "vitest";

import { createMetadataCatalog, EMPTY_MICROFLOW_METADATA_CATALOG } from "../metadata";
import { createObjectFromRegistry } from "../adapters";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../node-registry";
import type { MicroflowObject, MicroflowSchema } from "../schema/types";
import { buildVariableIndex } from "../variables";
import { buildMicroflowExpressionCompletionOptions } from "../expression-editor/codemirror-microflow-expression";
import { inferExpressionType } from "./expression-type-inference";
import { parseExpression } from "./expression-parser";
import { validateExpression } from "./expression-validator";

function registry(key: string) {
  const item = defaultMicroflowNodeRegistry.find(entry => getMicroflowNodeRegistryKey(entry) === key || entry.type === key);
  if (!item) {
    throw new Error(`Missing registry item ${key}`);
  }
  return item;
}

function objectFrom(key: string, id: string, x = 0, y = 0): MicroflowObject {
  return createObjectFromRegistry(registry(key), { x, y }, id);
}

function schemaWith(objects: MicroflowObject[]): MicroflowSchema {
  return {
    schemaVersion: "1.0.0",
    mendixProfile: "mx10",
    id: "mf-expr",
    stableId: "mf-expr",
    name: "ExprTest",
    displayName: "ExprTest",
    moduleId: "module-a",
    parameters: [],
    returnType: { kind: "void" },
    objectCollection: { id: "root", officialType: "Microflows$MicroflowObjectCollection", objects, flows: [] },
    flows: [],
    security: { allowedModuleRoleIds: [], allowedRoleNames: [], applyEntityAccess: true },
    concurrency: { allowConcurrentExecution: true },
    exposure: { exportLevel: "module", markAsUsed: false },
    validation: { issues: [] },
    editor: { viewport: { x: 0, y: 0, zoom: 1 }, zoom: 1, selection: {} },
    audit: { version: "1", status: "draft" },
  };
}

describe("expression validator", () => {
  const metadata = createMetadataCatalog({
    ...EMPTY_MICROFLOW_METADATA_CATALOG,
    entities: [
      { id: "System.User", name: "User", qualifiedName: "System.User", moduleName: "System", attributes: [], associations: [], specializations: [], isPersistable: false, isSystemEntity: true },
      { id: "System.Session", name: "Session", qualifiedName: "System.Session", moduleName: "System", attributes: [], associations: [], specializations: [], isPersistable: false, isSystemEntity: true },
      {
        id: "CRM.Customer",
        name: "Customer",
        qualifiedName: "CRM.Customer",
        moduleName: "CRM",
        attributes: [{ id: "CRM.Customer.Name", name: "Name", qualifiedName: "CRM.Customer.Name", type: { kind: "string" }, required: false }],
        associations: [{ associationQualifiedName: "CRM.Customer_Order", targetEntityQualifiedName: "CRM.Order", direction: "sourceToTarget", multiplicity: "oneToMany" }],
        specializations: [],
        isPersistable: true,
      },
      {
        id: "CRM.Order",
        name: "Order",
        qualifiedName: "CRM.Order",
        moduleName: "CRM",
        attributes: [{ id: "CRM.Order.Number", name: "Number", qualifiedName: "CRM.Order.Number", type: { kind: "string" }, required: false }],
        associations: [],
        specializations: [],
        isPersistable: true,
      },
    ],
    associations: [{
      id: "CRM.Customer_Order",
      name: "Customer_Order",
      qualifiedName: "CRM.Customer_Order",
      sourceEntityQualifiedName: "CRM.Customer",
      targetEntityQualifiedName: "CRM.Order",
      multiplicity: "oneToMany",
      direction: "sourceToTarget",
    }],
  });

  const start = objectFrom("startEvent", "start");
  const end = objectFrom("endEvent", "end", 240, 0);
  const schema = schemaWith([start, end]);
  const variableIndex = buildVariableIndex(schema, metadata);

  it("exposes $currentSession as a system variable", () => {
    expect(variableIndex.byName?.["$currentSession"]?.[0]?.source.kind).toBe("system");
  });

  it("flags slash division and accepts div operator", () => {
    const slashValidation = validateExpression({
      expression: "1 / 2",
      schema,
      metadata,
      variableIndex,
      context: { objectId: start.id, fieldPath: "test" },
    });
    const divInference = inferExpressionType({
      expression: "1 div 2",
      schema,
      metadata,
      variableIndex,
      objectId: start.id,
      fieldPath: "test",
    });

    expect(slashValidation.diagnostics.some(item => item.code === "MF_EXPR_USE_DIV_OPERATOR")).toBe(true);
    expect(divInference.inferredType.kind).toBe("decimal");
  });

  it("treats empty as a literal and validates supported functions", () => {
    const parsed = parseExpression("$currentUser = empty");
    const validation = validateExpression({
      expression: "toLowerCase('ABC')",
      schema,
      metadata,
      variableIndex,
      context: { objectId: start.id, fieldPath: "test", expectedType: { kind: "string" } },
    });

    expect(parsed.diagnostics).toEqual([]);
    expect(validation.diagnostics.some(item => item.code === "MF_EXPR_UNSUPPORTED_FUNCTION")).toBe(false);
  });

  it("supports if-then-else expressions and reports branch type mismatches", () => {
    const valid = validateExpression({
      expression: "if true then 'green' else 'red'",
      schema,
      metadata,
      variableIndex,
      context: { objectId: start.id, fieldPath: "test", expectedType: { kind: "string" } },
    });
    const invalid = validateExpression({
      expression: "if true then 'green' else 1",
      schema,
      metadata,
      variableIndex,
      context: { objectId: start.id, fieldPath: "test", expectedType: { kind: "string" } },
    });

    expect(valid.inferredType.kind).toBe("string");
    expect(valid.diagnostics.some(item => item.code === "MF_EXPR_TYPE_MISMATCH")).toBe(false);
    expect(invalid.diagnostics.some(item => item.code === "MF_EXPR_IF_BRANCH_TYPE_MISMATCH")).toBe(true);
  });

  it("validates multi-level association access paths", () => {
    const customerVarSchema = schemaWith([
      start,
      (() => {
        const base = createObjectFromRegistry(registry("activity:objectCreate"), { x: 120, y: 0 }, "customer-create") as MicroflowObject & { action: Record<string, unknown> };
        return {
          ...base,
          kind: "actionActivity",
          action: {
            ...base.action,
            entityQualifiedName: "CRM.Customer",
            outputVariableName: "customer",
            objectVariableName: "customer",
          },
        } as MicroflowObject;
      })(),
      end,
    ]);
    const customerIndex = buildVariableIndex(customerVarSchema, metadata);
    const validation = validateExpression({
      expression: "$customer/CRM.Customer_Order/CRM.Order/Number",
      schema: customerVarSchema,
      metadata,
      variableIndex: customerIndex,
      context: { objectId: "customer-create", fieldPath: "test" },
    });

    expect(validation.diagnostics.some(item => item.code === "MF_EXPR_MEMBER_NOT_FOUND")).toBe(false);
  });

  it("offers system variables and multi-level association paths in completion options", () => {
    const customerVarSchema = {
      ...schemaWith([start, end]),
      parameters: [
        {
          id: "param-customer",
          name: "customer",
          dataType: { kind: "object", entityQualifiedName: "CRM.Customer" } as const,
          required: true,
        },
      ],
    };
    const customerIndex = buildVariableIndex(customerVarSchema, metadata);
    const options = buildMicroflowExpressionCompletionOptions({
      schema: customerVarSchema,
      metadata,
      variableIndex: customerIndex,
      objectId: end.id,
      fieldPath: "test",
    });

    expect(options.some(option => option.value === "$currentSession")).toBe(true);
    expect(options.some(option => option.value === "$customer/Name")).toBe(true);
    expect(options.some(option => option.value === "$customer/CRM.Customer_Order/CRM.Order/Number")).toBe(true);
  });
});
