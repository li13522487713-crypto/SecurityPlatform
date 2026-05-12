import { describe, expect, it } from "vitest";

import { normalizeDesignVariables } from "./design-schema-variables";

describe("normalizeDesignVariables", () => {
  it("preserves distinct error-context variables from variable indexes", () => {
    const variables = normalizeDesignVariables({
      parameters: {},
      localVariables: {},
      objectOutputs: {},
      listOutputs: {},
      loopVariables: {},
      errorVariables: {
        $latestError: {
          name: "$latestError",
          dataType: { kind: "object", entityQualifiedName: "System.Error" },
          source: { kind: "errorContext", flowId: "flow-error", errorVariable: "$latestError" },
          scope: { collectionId: "root", errorHandlerFlowId: "flow-error", startObjectId: "handle-error" },
          readonly: true,
        },
        $latestHttpResponse: {
          name: "$latestHttpResponse",
          dataType: { kind: "object", entityQualifiedName: "System.HttpResponse" },
          source: { kind: "errorContext", flowId: "flow-error", errorVariable: "$latestHttpResponse" },
          scope: { collectionId: "root", errorHandlerFlowId: "flow-error", startObjectId: "handle-error" },
          readonly: true,
        },
        $latestSoapFault: {
          name: "$latestSoapFault",
          dataType: { kind: "object", entityQualifiedName: "System.SoapFault" },
          source: { kind: "errorContext", flowId: "flow-error", errorVariable: "$latestSoapFault" },
          scope: { collectionId: "root", errorHandlerFlowId: "flow-error", startObjectId: "handle-error" },
          readonly: true,
        },
      },
      systemVariables: {},
    });

    expect(variables.map(item => item.name)).toEqual(expect.arrayContaining([
      "$latestError",
      "$latestHttpResponse",
      "$latestSoapFault",
    ]));
    expect(variables.filter(item => item.scope === "errorContext")).toHaveLength(3);
  });
});
