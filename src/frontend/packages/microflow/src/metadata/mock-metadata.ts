import type { MicroflowMetadataCatalog } from "./metadata-catalog";

export const mockMicroflowMetadataCatalog: MicroflowMetadataCatalog = {
  entities: [
    {
      qualifiedName: "Sales.Order",
      associations: ["Sales.Order_OrderLine"],
      generalization: undefined,
      specializations: [],
      attributes: [
        { name: "Id", qualifiedName: "Sales.Order.Id", type: { kind: "string" }, required: true },
        { name: "OrderId", qualifiedName: "Sales.Order.OrderId", type: { kind: "string" }, required: true },
        { name: "Status", qualifiedName: "Sales.Order.Status", type: { kind: "enumeration", enumerationQualifiedName: "Sales.OrderStatus" }, required: true, enumRef: "Sales.OrderStatus" },
        { name: "CreatedDate", qualifiedName: "Sales.Order.CreatedDate", type: { kind: "dateTime" }, required: true },
        { name: "TotalAmount", qualifiedName: "Sales.Order.TotalAmount", type: { kind: "decimal" }, required: false }
      ]
    },
    {
      qualifiedName: "Sales.OrderLine",
      associations: ["Sales.Order_OrderLine"],
      specializations: [],
      attributes: [
        { name: "Id", qualifiedName: "Sales.OrderLine.Id", type: { kind: "string" }, required: true },
        { name: "Quantity", qualifiedName: "Sales.OrderLine.Quantity", type: { kind: "integer" }, required: true },
        { name: "Price", qualifiedName: "Sales.OrderLine.Price", type: { kind: "decimal" }, required: true }
      ]
    },
    {
      qualifiedName: "System.User",
      associations: [],
      specializations: [],
      attributes: [
        { name: "Name", qualifiedName: "System.User.Name", type: { kind: "string" }, required: true }
      ]
    },
    {
      qualifiedName: "System.HttpResponse",
      associations: [],
      specializations: [],
      attributes: [
        { name: "StatusCode", qualifiedName: "System.HttpResponse.StatusCode", type: { kind: "integer" }, required: true },
        { name: "Body", qualifiedName: "System.HttpResponse.Body", type: { kind: "string" }, required: false }
      ]
    },
    {
      qualifiedName: "Sales.PaymentMethod",
      associations: [],
      specializations: ["Sales.CardPayment", "Sales.BankTransferPayment"],
      attributes: [
        { name: "Id", qualifiedName: "Sales.PaymentMethod.Id", type: { kind: "string" }, required: true }
      ]
    },
    {
      qualifiedName: "Sales.CardPayment",
      associations: [],
      generalization: "Sales.PaymentMethod",
      specializations: [],
      attributes: [
        { name: "CardLast4", qualifiedName: "Sales.CardPayment.CardLast4", type: { kind: "string" }, required: false }
      ]
    },
    {
      qualifiedName: "Sales.BankTransferPayment",
      associations: [],
      generalization: "Sales.PaymentMethod",
      specializations: [],
      attributes: [
        { name: "BankName", qualifiedName: "Sales.BankTransferPayment.BankName", type: { kind: "string" }, required: false }
      ]
    }
  ],
  associations: [
    { qualifiedName: "Sales.Order_OrderLine", sourceEntity: "Sales.Order", targetEntity: "Sales.OrderLine", multiplicity: "many", owner: "source" }
  ],
  enumerations: [
    { qualifiedName: "Sales.OrderStatus", values: ["Pending", "Processing", "Completed", "Cancelled"] }
  ],
  microflows: [
    {
      id: "MF_ValidateOrder",
      qualifiedName: "Sales.ValidateOrder",
      parameters: [{ name: "order", type: { kind: "object", entityQualifiedName: "Sales.Order" }, required: true }],
      returnType: { kind: "boolean" }
    }
  ],
  pages: [
    { id: "Order.Detail", name: "Order.Detail", parameters: [{ name: "order", type: { kind: "object", entityQualifiedName: "Sales.Order" }, required: true }] }
  ],
  workflows: [],
  enabledConnectors: []
};
