const baseUrl = process.env.MICROFLOW_API_BASE_URL ?? "http://localhost:5002";
const workspaceId = process.env.MICROFLOW_WORKSPACE_ID ?? "demo-workspace";
const tenantId = process.env.MICROFLOW_TENANT_ID ?? "demo-tenant";
const userId = process.env.MICROFLOW_USER_ID ?? "metadata-verify-user";

type Json = Record<string, unknown>;

const headers = {
  Accept: "application/json",
  "Content-Type": "application/json",
  "X-Workspace-Id": workspaceId,
  "X-Tenant-Id": tenantId,
  "X-User-Id": userId,
};

function makeName(): string {
  return `VerifyMetadata${Date.now()}${Math.random().toString(36).slice(2, 7)}`;
}

async function api(method: string, path: string, body?: unknown): Promise<Json> {
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  const envelope = text ? JSON.parse(text) : undefined;
  if (!response.ok || !envelope?.success) {
    throw new Error(`${method} ${path} failed: HTTP ${response.status} ${text}`);
  }
  return envelope.data as Json;
}

function assert(condition: unknown, message: string): void {
  if (!condition) {
    throw new Error(message);
  }
}

function first<T extends Json>(items: unknown, predicate: (item: T) => boolean, message: string): T {
  const item = (items as T[]).find(predicate);
  assert(item, message);
  return item!;
}

async function createMicroflowTarget(): Promise<{ id: string; qualifiedName: string }> {
  const name = makeName();
  const created = await api("POST", "/api/microflows", {
    workspaceId,
    input: {
      name,
      displayName: name,
      description: "Metadata resolver verification target.",
      moduleId: "sales",
      moduleName: "Sales",
      tags: ["verify", "metadata"],
      parameters: [{ name: "order", type: { kind: "object", entityQualifiedName: "Sales.Order" }, required: false }],
      returnType: { kind: "void" },
    },
  });
  const id = String(created.id ?? created.resourceId);
  assert(id && id !== "undefined", "created microflow should return id");
  return { id, qualifiedName: `Sales.${name}` };
}

function resolveBody(extra: Json = {}): Json {
  return {
    refs: [
      { kind: "entity", qualifiedName: "Sales.Order", required: true },
      { kind: "attribute", qualifiedName: "Sales.Order.Status", required: true },
      { kind: "association", qualifiedName: "Sales.Order_OrderLine", required: true },
      { kind: "enumeration", qualifiedName: "Sales.OrderStatus", required: true },
      { kind: "entity", qualifiedName: "Sales.DoesNotExist", required: false },
    ],
    entities: ["Sales.Order", "Sales.DoesNotExist"],
    attributes: [
      { qualifiedName: "Sales.Order.Status" },
      { qualifiedName: "Status", entityQualifiedName: "Sales.Order" },
      { qualifiedName: "DoesNotExist", entityQualifiedName: "Sales.Order" },
    ],
    associations: [
      { qualifiedName: "Sales.Order_OrderLine" },
      { qualifiedName: "Operator", entityQualifiedName: "Sales.Order" },
      { qualifiedName: "DoesNotExist", entityQualifiedName: "Sales.Order" },
    ],
    enumerations: ["Sales.OrderStatus", "Sales.DoesNotExist"],
    enumerationValues: [
      { enumerationQualifiedName: "Sales.OrderStatus", value: "New" },
      { enumerationQualifiedName: "Sales.OrderStatus", value: "Nope" },
    ],
    dataTypes: [
      { kind: "object", entityQualifiedName: "Sales.Order" },
      { kind: "list", itemType: { kind: "object", entityQualifiedName: "Sales.Order" } },
      { kind: "enumeration", enumerationQualifiedName: "Sales.OrderStatus" },
      { kind: "object" },
    ],
    memberPaths: [
      { rootType: { kind: "object", entityQualifiedName: "Sales.Order" }, memberPath: ["Status"] },
      { rootType: { kind: "object", entityQualifiedName: "Sales.Order" }, memberPath: ["Order_Operator", "Name"] },
      { rootType: { kind: "object", entityQualifiedName: "Sales.Order" }, memberPath: ["OrderLine", "Product", "Name"] },
    ],
    securityContext: {
      userId,
      userName: userId,
      roles: ["SalesReader"],
      workspaceId,
      tenantId,
      applyEntityAccess: true,
      isSystemContext: false,
      traceId: "verify-metadata-resolver",
    },
    ...extra,
  };
}

async function run(): Promise<void> {
  await api("GET", "/api/microflows/health");

  const target = await createMicroflowTarget();
  const result = await api("POST", "/api/microflows/runtime/metadata/resolve", resolveBody({
    microflows: [
      { id: target.id },
      { qualifiedName: target.qualifiedName },
      { id: "missing-microflow" },
    ],
  }));

  first(result.entities, item => item.qualifiedName === "Sales.Order" && item.found === true, "Sales.Order should resolve");
  first(result.entities, item => item.qualifiedName === "Sales.DoesNotExist" && item.found === false, "unknown entity should not resolve");
  first(result.attributes, item => item.qualifiedName === "Sales.Order.Status" && item.found === true, "Sales.Order.Status should resolve");
  first(result.attributes, item => item.qualifiedName === "DoesNotExist" && item.found === false, "unknown attribute should not resolve");
  first(result.associations, item => item.qualifiedName === "Sales.Order_OrderLine" && item.found === true, "OrderLine association should resolve");
  first(result.associations, item => item.qualifiedName === "Sales.Order_Operator" && item.found === true, "Operator association should resolve from member name");
  first(result.associations, item => item.qualifiedName === "DoesNotExist" && item.found === false, "unknown association should not resolve");
  first(result.enumerations, item => item.qualifiedName === "Sales.OrderStatus" && item.found === true, "Sales.OrderStatus should resolve");
  first(result.enumerationValues, item => item.value === "New" && item.found === true, "enum value New should resolve");
  first(result.enumerationValues, item => item.value === "Nope" && item.found === false, "unknown enum value should not resolve");
  first(result.dataTypes, item => item.kind === "object" && item.entityQualifiedName === "Sales.Order" && item.found === true, "object dataType should resolve");
  first(result.dataTypes, item => item.kind === "list" && item.found === true, "list<object> dataType should resolve");
  first(result.dataTypes, item => item.kind === "enumeration" && item.enumerationQualifiedName === "Sales.OrderStatus", "enumeration dataType should resolve");
  first(result.dataTypes, item => item.kind === "unknown" && item.found === false, "invalid dataType should report unknown");
  first(result.memberPaths, item => JSON.stringify(item.memberPath) === JSON.stringify(["Status"]) && item.found === true, "$Order/Status should resolve");
  first(result.memberPaths, item => JSON.stringify(item.memberPath) === JSON.stringify(["Order_Operator", "Name"]) && item.found === true, "$Order/Order_Operator/Name should resolve");
  first(result.memberPaths, item => JSON.stringify(item.memberPath) === JSON.stringify(["OrderLine", "Product", "Name"]) && JSON.stringify(item.diagnostics).includes("LIST_TRAVERSAL"), "list traversal should produce diagnostic");
  first(result.microflows, item => item.id === target.id && item.found === true, "microflow ref by id should resolve");
  first(result.microflows, item => item.qualifiedName === target.qualifiedName && item.found === true, "microflow ref by qualifiedName should resolve");
  assert(JSON.stringify(result.resolutionReport).includes("Sales.DoesNotExist"), "optional missing metadata should be reported");
  assert(!JSON.stringify(result).includes("objectCollection"), "metadata resolver response must not leak FlowGram JSON");

  const allowAll = await api("POST", "/api/microflows/runtime/metadata/resolve", resolveBody({ entityAccessMode: "AllowAll", entities: ["Sales.Order"] }));
  first(allowAll.entityAccessDecisions, item => item.operation === "read" && item.allowed === true, "AllowAll read should allow");
  first(allowAll.entityAccessDecisions, item => item.operation === "create" && item.allowed === true, "AllowAll create should allow");

  const denyUnknown = await api("POST", "/api/microflows/runtime/metadata/resolve", resolveBody({ entityAccessMode: "DenyUnknownEntity", entities: ["Sales.DoesNotExist"] }));
  first(denyUnknown.entityAccessDecisions, item => item.allowed === false && item.source === "denyUnknownEntity", "DenyUnknownEntity should deny unknown entity");

  const roleAllow = await api("POST", "/api/microflows/runtime/metadata/resolve", resolveBody({
    entityAccessMode: "RoleBasedStub",
    entities: ["Sales.Order"],
    entityRequiredRoles: { "Sales.Order": ["SalesReader"] },
  }));
  first(roleAllow.entityAccessDecisions, item => item.operation === "read" && item.allowed === true && item.source === "roleBasedStub", "RoleBasedStub should allow matching role");

  const roleDeny = await api("POST", "/api/microflows/runtime/metadata/resolve", resolveBody({
    entityAccessMode: "RoleBasedStub",
    entities: ["Sales.Order"],
    entityRequiredRoles: { "Sales.Order": ["AdminOnly"] },
  }));
  first(roleDeny.entityAccessDecisions, item => item.operation === "read" && item.allowed === false && item.source === "roleBasedStub", "RoleBasedStub should deny missing role");

  const systemContext = await api("POST", "/api/microflows/runtime/metadata/resolve", resolveBody({
    entityAccessMode: "RoleBasedStub",
    entities: ["Sales.Order"],
    entityRequiredRoles: { "Sales.Order": ["AdminOnly"] },
    securityContext: { userId: "system", roles: [], workspaceId, tenantId, applyEntityAccess: true, isSystemContext: true },
  }));
  first(systemContext.entityAccessDecisions, item => item.operation === "read" && item.allowed === true && item.source === "systemContext", "system context should bypass stub");

  const bypass = await api("POST", "/api/microflows/runtime/metadata/resolve", resolveBody({
    entityAccessMode: "RoleBasedStub",
    entities: ["Sales.Order"],
    entityRequiredRoles: { "Sales.Order": ["AdminOnly"] },
    securityContext: { userId, roles: [], workspaceId, tenantId, applyEntityAccess: false, isSystemContext: false },
  }));
  first(bypass.entityAccessDecisions, item => item.operation === "read" && item.allowed === true && item.source === "disabled", "applyEntityAccess=false should bypass stub");

  console.log("verify-microflow-metadata-resolver-entity-access: PASS");
}

run().catch(error => {
  console.error("verify-microflow-metadata-resolver-entity-access: FAIL");
  console.error(error);
  process.exitCode = 1;
});
