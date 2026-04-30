import { describe, expect, it } from "vitest";

describe("microflow production gate collectors", () => {
  async function loadGateLib() {
    const moduleUrl = new URL("../../../../../../../scripts/microflow-production-gate-lib.ts", import.meta.url).href;
    return await import(moduleUrl) as {
      detectLegacyAliasesInText: (text: string) => string[];
      parseBackendDescriptorsFromSource: (source: string) => Array<{
        actionKind: string;
        producesVariables: boolean;
        runtimeCategory: string;
        connectorCapability?: string;
        errorCode?: string;
      }>;
      parseMarkdownMatrixActionKinds: (markdown: string) => Set<string>;
    };
  }

  it("parses backend descriptor factories and connector gates", async () => {
    const { parseBackendDescriptorsFromSource } = await loadGateLib();
    const source = `
public static IReadOnlyList<MicroflowActionExecutorDescriptor> BuiltInDescriptors()
    =>
    [
        Server("retrieve", "RetrieveAction", "object", "RetrieveActionExecutor", producesVariables: true, producesTransaction: false),
        Connector("webServiceCall", "WebServiceCallAction", "integration", "ConnectorBackedActionExecutor:webServiceCall", MicroflowRuntimeConnectorCapability.SoapWebService, "SOAP required."),
        Unsupported("callNanoflow", "CallNanoflowAction", "call", MicroflowActionSupportLevel.NanoflowOnly, "Nanoflow only.")
    ];
`;
    const descriptors = parseBackendDescriptorsFromSource(source);
    expect(descriptors.map(item => item.actionKind)).toEqual(["retrieve", "webServiceCall", "callNanoflow"]);
    expect(descriptors[0].producesVariables).toBe(true);
    expect(descriptors[1].runtimeCategory).toBe("ConnectorBacked");
    expect(descriptors[1].connectorCapability).toBe("SoapWebService");
    expect(descriptors[1].errorCode).toBe("RUNTIME_CONNECTOR_REQUIRED");
    expect(descriptors[2].errorCode).toBe("RUNTIME_UNSUPPORTED_ACTION");
  });

  it("detects legacy aliases in schema-like text", async () => {
    const { detectLegacyAliasesInText } = await loadGateLib();
    expect(detectLegacyAliasesInText('{ "kind": "rollbackObject" }')).toContain("rollbackObject");
    expect(detectLegacyAliasesInText('{ "kind": "rollback" }')).toEqual([]);
  });

  it("parses matrix actionKind rows", async () => {
    const { parseMarkdownMatrixActionKinds } = await loadGateLib();
    const markdown = `
| actionKind | category |
|---|---|
| retrieve | object |
| webServiceCall | integration |
`;
    expect([...parseMarkdownMatrixActionKinds(markdown)].sort()).toEqual(["retrieve", "webServiceCall"]);
  });
});
