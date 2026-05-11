import { describe, expect, it } from "vitest";

import type { MicroflowVariable } from "../types/mendix-types";
import type { MicroflowMetadataCatalog } from "../metadata/metadata-catalog";
import { getAutoCompleteSuggestions, parseExpression } from "./expression-engine";

describe("expression-engine", () => {
  const vars: MicroflowVariable[] = [
    { name: "$order", type: "Object", attributes: ["Amount", "Customer", "Total" ] },
    { name: "$count", type: "Integer" },
    { name: "$price", type: "Decimal" },
  ];

  it("rejects arithmetic division using '/' and suggests div", () => {
    const result = parseExpression("$price / $count", vars);

    expect(result.valid).toBe(false);
    expect(result.diagnostics.some(item => item.code === "MF_EXPR_INVALID_DIVISION")).toBe(true);
    expect(result.errorMessage).toBe("除法请使用 div 而不是 /。");
  });

  it("allows slash in attribute access", () => {
    const result = parseExpression("$order/Amount", vars);

    expect(result.valid).toBe(true);
    expect(result.usedVariables).toContain("order");
  });

  it("treats $currentUser and $currentSession as always-available system variables", () => {
    const result = parseExpression("if $currentUser = empty then $currentSession else $currentUser", []);

    expect(result.valid).toBe(true);
    expect(result.usedVariables.sort()).toEqual(["currentSession", "currentUser"]);
    expect(result.diagnostics.some(item => item.code === "MF_EXPR_UNKNOWN_VARIABLE")).toBe(false);
  });

  it("suggests association members from metadata on nested object path", () => {
    const metadata: MicroflowMetadataCatalog = {
      modules: [],
      entities: [
        {
          id: "1",
          name: "Order",
          qualifiedName: "Sales.Order",
          moduleName: "Sales",
          isPersistable: true,
          specializations: [],
          attributes: [
            { id: "o1", name: "Number", qualifiedName: "Sales.Order/Number", type: { kind: "string" }, required: false },
          ],
          associations: [],
        },
        {
          id: "2",
          name: "Customer",
          moduleName: "CRM",
          qualifiedName: "CRM.Customer",
          isPersistable: true,
          specializations: [],
          attributes: [
            { id: "c1", name: "Name", qualifiedName: "CRM.Customer/Name", type: { kind: "string" }, required: false },
            { id: "c2", name: "Email", qualifiedName: "CRM.Customer/Email", type: { kind: "string" }, required: false },
          ],
          associations: [],
        },
      ],
      associations: [
        {
          id: "a1",
          name: "Order_Customer",
          qualifiedName: "Sales.Order.Customer",
          sourceEntityQualifiedName: "Sales.Order",
          targetEntityQualifiedName: "CRM.Customer",
          multiplicity: "manyToOne",
          direction: "sourceToTarget",
        },
      ],
      enumerations: [],
      microflows: [],
      pages: [],
      workflows: [],
    };

    const suggestions = getAutoCompleteSuggestions(
      "$order/Customer/",
      "$order/Customer/".length,
      [{ name: "$order", type: "Object", entityType: "Sales.Order" }],
      metadata,
    );

    expect(suggestions.map(item => item.label)).toContain("Name");
    expect(suggestions.map(item => item.label)).toContain("Email");
  });

  it("supports multi-hop association member suggestions", () => {
    const metadata: MicroflowMetadataCatalog = {
      modules: [],
      entities: [
        {
          id: "e-order",
          name: "Order",
          qualifiedName: "Sales.Order",
          moduleName: "Sales",
          isPersistable: true,
          specializations: [],
          attributes: [],
          associations: [],
        },
        {
          id: "e-customer",
          name: "Customer",
          moduleName: "CRM",
          qualifiedName: "CRM.Customer",
          isPersistable: true,
          specializations: [],
          attributes: [],
          associations: [],
        },
        {
          id: "e-region",
          name: "Region",
          moduleName: "CRM",
          qualifiedName: "CRM.Region",
          isPersistable: true,
          specializations: [],
          attributes: [
            { id: "r1", name: "Code", qualifiedName: "CRM.Region/Code", type: { kind: "string" }, required: false },
            { id: "r2", name: "Name", qualifiedName: "CRM.Region/Name", type: { kind: "string" }, required: false },
          ],
          associations: [],
        },
      ],
      associations: [
        {
          id: "a-order-customer",
          name: "Customer",
          qualifiedName: "Sales.Order.Customer",
          sourceEntityQualifiedName: "Sales.Order",
          targetEntityQualifiedName: "CRM.Customer",
          multiplicity: "manyToOne",
          direction: "sourceToTarget",
        },
        {
          id: "a-customer-region",
          name: "Region",
          qualifiedName: "CRM.Customer.Region",
          sourceEntityQualifiedName: "CRM.Customer",
          targetEntityQualifiedName: "CRM.Region",
          multiplicity: "manyToOne",
          direction: "sourceToTarget",
        },
      ],
      enumerations: [],
      microflows: [],
      pages: [],
      workflows: [],
    };

    const suggestions = getAutoCompleteSuggestions(
      "$order/Customer/Region/",
      "$order/Customer/Region/".length,
      [{ name: "$order", type: "Object", entityType: "Sales.Order" }],
      metadata,
    );

    expect(suggestions.map(item => item.label)).toContain("Code");
    expect(suggestions.map(item => item.label)).toContain("Name");
  });

  it("suggests variables when typing '$' prefix", () => {
    const suggestions = getAutoCompleteSuggestions("$", 1, vars);

    expect(suggestions.map(item => item.label)).toEqual(expect.arrayContaining(["order", "count", "price"]));
    expect(suggestions.map(item => item.type).every(type => type === "variable")).toBe(true);
  });

  it("suggests object attributes after '$object/'", () => {
    const suggestions = getAutoCompleteSuggestions("$order/", "$order/".length, vars);

    expect(suggestions.map(item => item.label)).toEqual(expect.arrayContaining(["Amount", "Customer", "Total"]));
    expect(suggestions.map(item => item.type).every(type => type === "attribute")).toBe(true);
  });

  it("includes built-in system variables in autocomplete", () => {
    const suggestions = getAutoCompleteSuggestions("$curr", "$curr".length, []);

    expect(suggestions.map(item => item.label)).toEqual(expect.arrayContaining(["currentUser", "currentSession"]));
  });

  it("accepts empty-check expressions for object variables", () => {
    const result = parseExpression("$order = empty", vars);

    expect(result.valid).toBe(true);
    expect(result.usedVariables).toContain("order");
  });

  it("accepts inline if-then-else expressions", () => {
    const result = parseExpression("if $count > 0 then 'ok' else 'empty'", vars);

    expect(result.valid).toBe(true);
    expect(result.usedVariables).toContain("count");
  });
});
