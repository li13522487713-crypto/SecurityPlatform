using System.Text.Json;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Services;

public static class MicroflowSeedMetadataCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public const string Version = "seed-v1";

    public static MicroflowMetadataCatalogDto Create(DateTimeOffset? updatedAt = null)
    {
        return new MicroflowMetadataCatalogDto
        {
            Version = Version,
            UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow,
            Modules =
            [
                Module("sales", "Sales", "Sales", "Sales demo module from backend seed catalog."),
                Module("inventory", "Inventory", "Inventory", "Inventory demo module from backend seed catalog."),
                Module("system", "System", "System", "System metadata seed."),
                Module("workflow", "Workflow", "Workflow", "Workflow metadata seed.")
            ],
            Entities =
            [
                Entity("system-user", "User", "System.User", "System", true, null, ["Id", "Name", "Email"]),
                Entity("system-file-document", "FileDocument", "System.FileDocument", "System", true, null,
                    [("Name", Type("string"), null), ("Contents", Type("binary"), null)]),
                Entity("sales-order", "Order", "Sales.Order", "Sales", false, null,
                    [
                        ("Id", Type("string"), null),
                        ("Status", EnumType("Sales.OrderStatus"), "Sales.OrderStatus"),
                        ("CreatedDate", Type("dateTime"), null),
                        ("ProcessedDate", Type("dateTime"), null),
                        ("Operator", Type("string"), null),
                        ("TotalAmount", Type("decimal"), null)
                    ],
                    [
                        AssociationRef("Sales.Order_OrderLine", "Sales.OrderLine", "sourceToTarget", "oneToMany"),
                        AssociationRef("Sales.Order_Operator", "System.User", "sourceToTarget", "manyToOne")
                    ]),
                Entity("sales-order-line", "OrderLine", "Sales.OrderLine", "Sales", false, null,
                    [("Id", Type("string"), null), ("Quantity", Type("integer"), null), ("Price", Type("decimal"), null)],
                    [AssociationRef("Sales.OrderLine_Product", "Sales.Product", "sourceToTarget", "manyToOne")]),
                Entity("sales-product", "Product", "Sales.Product", "Sales", false, null,
                    [("Id", Type("string"), null), ("Name", Type("string"), null), ("Stock", Type("integer"), null), ("Price", Type("decimal"), null)]),
                Entity("sales-member", "Member", "Sales.Member", "Sales", false, null,
                    [("Id", Type("string"), null), ("Name", Type("string"), null), ("Email", Type("string"), null)],
                    specializations: ["Sales.Professor", "Sales.Student"]),
                Entity("sales-professor", "Professor", "Sales.Professor", "Sales", false, "Sales.Member",
                    [("Title", Type("string"), null), ("Department", Type("string"), null)]),
                Entity("sales-student", "Student", "Sales.Student", "Sales", false, "Sales.Member",
                    [("StudentNo", Type("string"), null), ("Grade", Type("string"), null)])
            ],
            Associations =
            [
                Association("sales-order-order-line", "Order_OrderLine", "Sales.Order_OrderLine", "Sales.Order", "Sales.OrderLine", "Sales.Order", "oneToMany"),
                Association("sales-order-line-product", "OrderLine_Product", "Sales.OrderLine_Product", "Sales.OrderLine", "Sales.Product", "Sales.OrderLine", "manyToOne"),
                Association("sales-order-operator", "Order_Operator", "Sales.Order_Operator", "Sales.Order", "System.User", "Sales.Order", "manyToOne")
            ],
            Enumerations =
            [
                Enumeration("sales-order-status", "OrderStatus", "Sales.OrderStatus", "Sales", ["New", "Processing", "Paid", "Cancelled", "Failed"]),
                Enumeration("inventory-result", "InventoryResult", "Inventory.InventoryResult", "Inventory", ["Enough", "NotEnough", "Unknown"]),
                Enumeration("system-message-type", "MessageType", "System.MessageType", "System", ["Info", "Warning", "Error"])
            ],
            Microflows = Array.Empty<MetadataMicroflowRefDto>(),
            Pages = Array.Empty<MetadataPageRefDto>(),
            Workflows = Array.Empty<MetadataWorkflowRefDto>(),
            Connectors =
            [
                new MetadataConnectorDto
                {
                    Id = "http-rest",
                    Name = "REST Connector",
                    Type = "rest",
                    Enabled = true,
                    Capabilities = ["request", "response", "json"]
                }
            ]
        };
    }

    public static JsonElement Type(string kind)
        => JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["kind"] = kind }, JsonOptions);

    public static JsonElement EnumType(string qualifiedName)
        => JsonSerializer.SerializeToElement(
            new Dictionary<string, object?> { ["kind"] = "enumeration", ["enumerationQualifiedName"] = qualifiedName },
            JsonOptions);

    public static JsonElement UnknownType(string reason)
        => JsonSerializer.SerializeToElement(
            new Dictionary<string, object?> { ["kind"] = "unknown", ["reason"] = reason },
            JsonOptions);

    private static MetadataModuleDto Module(string id, string name, string qualifiedName, string description)
        => new() { Id = id, Name = name, QualifiedName = qualifiedName, Description = description };

    private static MetadataEntityDto Entity(
        string id,
        string name,
        string qualifiedName,
        string moduleName,
        bool isSystem,
        string? generalization,
        string[] attributes,
        IReadOnlyList<MetadataAssociationRefDto>? associations = null,
        IReadOnlyList<string>? specializations = null)
        => Entity(
            id,
            name,
            qualifiedName,
            moduleName,
            isSystem,
            generalization,
            attributes.Select(attribute => (attribute, Type("string"), (string?)null)).ToArray(),
            associations,
            specializations);

    private static MetadataEntityDto Entity(
        string id,
        string name,
        string qualifiedName,
        string moduleName,
        bool isSystem,
        string? generalization,
        IReadOnlyList<(string Name, JsonElement Type, string? EnumQualifiedName)> attributes,
        IReadOnlyList<MetadataAssociationRefDto>? associations = null,
        IReadOnlyList<string>? specializations = null)
        => new()
        {
            Id = id,
            Name = name,
            QualifiedName = qualifiedName,
            ModuleName = moduleName,
            Documentation = $"Backend seed metadata for {qualifiedName}.",
            Attributes = attributes.Select(attribute => new MetadataAttributeDto
            {
                Id = $"{qualifiedName}.{attribute.Name}",
                Name = attribute.Name,
                QualifiedName = $"{qualifiedName}.{attribute.Name}",
                Type = attribute.Type,
                Required = false,
                EnumQualifiedName = attribute.EnumQualifiedName
            }).ToArray(),
            Associations = associations ?? Array.Empty<MetadataAssociationRefDto>(),
            Generalization = generalization,
            Specializations = specializations ?? Array.Empty<string>(),
            IsPersistable = true,
            IsSystemEntity = isSystem
        };

    private static MetadataAssociationRefDto AssociationRef(string associationQualifiedName, string targetEntityQualifiedName, string direction, string multiplicity)
        => new()
        {
            AssociationQualifiedName = associationQualifiedName,
            TargetEntityQualifiedName = targetEntityQualifiedName,
            Direction = direction,
            Multiplicity = multiplicity
        };

    private static MetadataAssociationDto Association(
        string id,
        string name,
        string qualifiedName,
        string source,
        string target,
        string owner,
        string multiplicity)
        => new()
        {
            Id = id,
            Name = name,
            QualifiedName = qualifiedName,
            SourceEntityQualifiedName = source,
            TargetEntityQualifiedName = target,
            OwnerEntityQualifiedName = owner,
            Multiplicity = multiplicity,
            Direction = "sourceToTarget"
        };

    private static MetadataEnumerationDto Enumeration(string id, string name, string qualifiedName, string moduleName, string[] values)
        => new()
        {
            Id = id,
            Name = name,
            QualifiedName = qualifiedName,
            ModuleName = moduleName,
            Values = values.Select(value => new MetadataEnumerationValueDto { Key = value, Caption = value }).ToArray()
        };
}
