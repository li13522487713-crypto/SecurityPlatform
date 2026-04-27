/**
 * Development/Test only.
 * Do not import this file from production runtime paths.
 * Production should use HTTP adapters through createMicroflowAdapterBundle.
 */
import type { MetadataAttribute, MetadataEntity, MicroflowMetadataCatalog } from "./metadata-catalog";
import type { MicroflowDataType } from "../schema";

function primitive(kind: Extract<MicroflowDataType["kind"], "boolean" | "integer" | "long" | "decimal" | "string" | "dateTime">): MicroflowDataType {
  return { kind };
}

function attribute(entity: string, name: string, type: MicroflowDataType, required = false): MetadataAttribute {
  return {
    id: `${entity}.${name}`,
    name,
    qualifiedName: `${entity}.${name}`,
    type,
    required,
    enumQualifiedName: type.kind === "enumeration" ? type.enumerationQualifiedName : undefined,
  };
}

function entity(input: Omit<MetadataEntity, "id" | "name" | "moduleName" | "isPersistable"> & { qualifiedName: string; isPersistable?: boolean }): MetadataEntity {
  const [moduleName, name] = input.qualifiedName.split(".");
  return {
    id: input.qualifiedName,
    name: name ?? input.qualifiedName,
    moduleName: moduleName ?? "Default",
    isPersistable: input.isPersistable ?? true,
    ...input,
  };
}

export const mockMicroflowMetadataCatalog: MicroflowMetadataCatalog = {
  version: "mock-1",
  modules: [
    { id: "Sales", name: "Sales", qualifiedName: "Sales" },
    { id: "Inventory", name: "Inventory", qualifiedName: "Inventory" },
    { id: "System", name: "System", qualifiedName: "System" },
    { id: "Workflow", name: "Workflow", qualifiedName: "Workflow" },
  ],
  entities: [
    entity({
      qualifiedName: "Sales.Order",
      associations: [
        { associationQualifiedName: "Sales.Order_OrderLine", targetEntityQualifiedName: "Sales.OrderLine", direction: "sourceToTarget", multiplicity: "oneToMany" },
        { associationQualifiedName: "Sales.Order_Operator", targetEntityQualifiedName: "System.User", direction: "sourceToTarget", multiplicity: "manyToOne" },
      ],
      specializations: [],
      attributes: [
        attribute("Sales.Order", "Id", primitive("string"), true),
        attribute("Sales.Order", "Status", { kind: "enumeration", enumerationQualifiedName: "Sales.OrderStatus" }, true),
        attribute("Sales.Order", "CreatedDate", primitive("dateTime"), true),
        attribute("Sales.Order", "ProcessedDate", primitive("dateTime")),
        attribute("Sales.Order", "Operator", primitive("string")),
        attribute("Sales.Order", "TotalAmount", primitive("decimal")),
      ],
    }),
    entity({
      qualifiedName: "Sales.OrderLine",
      associations: [
        { associationQualifiedName: "Sales.Order_OrderLine", targetEntityQualifiedName: "Sales.Order", direction: "targetToSource", multiplicity: "manyToOne" },
        { associationQualifiedName: "Sales.OrderLine_Product", targetEntityQualifiedName: "Sales.Product", direction: "sourceToTarget", multiplicity: "manyToOne" },
      ],
      specializations: [],
      attributes: [
        attribute("Sales.OrderLine", "Id", primitive("string"), true),
        attribute("Sales.OrderLine", "Quantity", primitive("integer"), true),
        attribute("Sales.OrderLine", "Price", primitive("decimal"), true),
        attribute("Sales.OrderLine", "IsValid", primitive("boolean")),
      ],
    }),
    entity({
      qualifiedName: "Sales.Product",
      associations: [{ associationQualifiedName: "Sales.OrderLine_Product", targetEntityQualifiedName: "Sales.OrderLine", direction: "targetToSource", multiplicity: "oneToMany" }],
      specializations: [],
      attributes: [
        attribute("Sales.Product", "Id", primitive("string"), true),
        attribute("Sales.Product", "Name", primitive("string"), true),
        attribute("Sales.Product", "Stock", primitive("integer"), true),
        attribute("Sales.Product", "Price", primitive("decimal")),
      ],
    }),
    entity({
      qualifiedName: "Sales.Member",
      associations: [],
      specializations: ["Sales.Professor", "Sales.Student"],
      attributes: [
        attribute("Sales.Member", "Id", primitive("string"), true),
        attribute("Sales.Member", "Name", primitive("string"), true),
        attribute("Sales.Member", "Email", primitive("string")),
      ],
    }),
    entity({
      qualifiedName: "Sales.Professor",
      associations: [],
      generalization: "Sales.Member",
      specializations: [],
      attributes: [
        attribute("Sales.Professor", "Title", primitive("string")),
        attribute("Sales.Professor", "Department", primitive("string")),
      ],
    }),
    entity({
      qualifiedName: "Sales.Student",
      associations: [],
      generalization: "Sales.Member",
      specializations: [],
      attributes: [
        attribute("Sales.Student", "StudentNo", primitive("string")),
        attribute("Sales.Student", "Grade", primitive("string")),
      ],
    }),
    entity({
      qualifiedName: "System.User",
      isSystemEntity: true,
      associations: [],
      specializations: [],
      attributes: [
        attribute("System.User", "Id", primitive("string"), true),
        attribute("System.User", "Name", primitive("string"), true),
        attribute("System.User", "Email", primitive("string")),
      ],
    }),
    entity({
      qualifiedName: "Inventory.StockCheckResult",
      associations: [],
      specializations: [],
      attributes: [
        attribute("Inventory.StockCheckResult", "ProductId", primitive("string"), true),
        attribute("Inventory.StockCheckResult", "Available", primitive("boolean"), true),
        attribute("Inventory.StockCheckResult", "Quantity", primitive("integer")),
      ],
    }),
  ],
  associations: [
    {
      id: "Sales.Order_OrderLine",
      name: "Order_OrderLine",
      qualifiedName: "Sales.Order_OrderLine",
      sourceEntityQualifiedName: "Sales.Order",
      targetEntityQualifiedName: "Sales.OrderLine",
      ownerEntityQualifiedName: "Sales.Order",
      multiplicity: "oneToMany",
      direction: "sourceToTarget",
    },
    {
      id: "Sales.OrderLine_Product",
      name: "OrderLine_Product",
      qualifiedName: "Sales.OrderLine_Product",
      sourceEntityQualifiedName: "Sales.OrderLine",
      targetEntityQualifiedName: "Sales.Product",
      ownerEntityQualifiedName: "Sales.OrderLine",
      multiplicity: "manyToOne",
      direction: "sourceToTarget",
    },
    {
      id: "Sales.Order_Operator",
      name: "Order_Operator",
      qualifiedName: "Sales.Order_Operator",
      sourceEntityQualifiedName: "Sales.Order",
      targetEntityQualifiedName: "System.User",
      ownerEntityQualifiedName: "Sales.Order",
      multiplicity: "manyToOne",
      direction: "sourceToTarget",
    },
  ],
  enumerations: [
    {
      id: "Sales.OrderStatus",
      name: "OrderStatus",
      qualifiedName: "Sales.OrderStatus",
      moduleName: "Sales",
      values: ["New", "Processing", "Paid", "Cancelled", "Failed"].map(key => ({ key, caption: key })),
    },
    {
      id: "Inventory.InventoryResult",
      name: "InventoryResult",
      qualifiedName: "Inventory.InventoryResult",
      moduleName: "Inventory",
      values: ["Enough", "NotEnough", "Unknown"].map(key => ({ key, caption: key })),
    },
    {
      id: "System.MessageType",
      name: "MessageType",
      qualifiedName: "System.MessageType",
      moduleName: "System",
      values: ["Info", "Warning", "Error"].map(key => ({ key, caption: key })),
    },
  ],
  microflows: [
    {
      id: "MF_ProcessOrder",
      name: "ProcessOrder",
      qualifiedName: "Sales.ProcessOrder",
      moduleName: "Sales",
      parameters: [{ name: "orderId", type: primitive("string"), required: true }],
      returnType: primitive("boolean"),
      status: "published",
    },
    {
      id: "MF_CheckInventory",
      name: "CheckInventory",
      qualifiedName: "Sales.CheckInventory",
      moduleName: "Sales",
      parameters: [{ name: "order", type: { kind: "object", entityQualifiedName: "Sales.Order" }, required: true }],
      returnType: { kind: "enumeration", enumerationQualifiedName: "Inventory.InventoryResult" },
      status: "published",
    },
    {
      id: "MF_NotifyUser",
      name: "NotifyUser",
      qualifiedName: "Sales.NotifyUser",
      moduleName: "Sales",
      parameters: [
        { name: "user", type: { kind: "object", entityQualifiedName: "System.User" }, required: true },
        { name: "message", type: primitive("string"), required: true },
      ],
      returnType: { kind: "void" },
      status: "draft",
    },
  ],
  pages: [
    {
      id: "PAGE_OrderDetail",
      name: "OrderDetailPage",
      qualifiedName: "Sales.OrderDetailPage",
      moduleName: "Sales",
      parameters: [{ name: "order", type: { kind: "object", entityQualifiedName: "Sales.Order" }, required: true }],
    },
    { id: "PAGE_OrderList", name: "OrderListPage", qualifiedName: "Sales.OrderListPage", moduleName: "Sales", parameters: [] },
    {
      id: "PAGE_UserTask",
      name: "UserTaskPage",
      qualifiedName: "System.UserTaskPage",
      moduleName: "System",
      parameters: [{ name: "user", type: { kind: "object", entityQualifiedName: "System.User" }, required: true }],
    },
  ],
  workflows: [
    { id: "WF_OrderApproval", name: "OrderApproval", qualifiedName: "Workflow.OrderApproval", moduleName: "Workflow", contextEntityQualifiedName: "Sales.Order" },
    { id: "WF_InventoryReview", name: "InventoryReview", qualifiedName: "Workflow.InventoryReview", moduleName: "Workflow", contextEntityQualifiedName: "Sales.Product" },
  ],
  connectors: [
    { id: "rest", name: "REST", type: "integration", enabled: true, capabilities: ["restCall"] },
  ],
};
