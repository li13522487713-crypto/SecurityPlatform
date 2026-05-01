using System.Data;
using System.Globalization;
using System.Text.Json;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime;
using Atlas.Application.Microflows.Runtime.Objects;
using Atlas.Infrastructure.Services.DatabaseStructure;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Microflows;

public sealed class SqlSugarMicroflowRuntimeObjectStore : IDatabaseBackedMicroflowRuntimeObjectStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IAiDatabaseClientFactory _clientFactory;
    private readonly IDatabaseDialectRegistry _dialects;

    public SqlSugarMicroflowRuntimeObjectStore(
        IAiDatabaseClientFactory clientFactory,
        IDatabaseDialectRegistry dialects)
    {
        _clientFactory = clientFactory;
        _dialects = dialects;
    }

    public async Task<MicroflowRuntimeObjectStoreResult> RetrieveAsync(MicroflowRuntimeObjectQuery query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var entity = ResolveEntity(query.EntityType, query.RuntimeContext);
        if (entity is null)
        {
            return Failed(RuntimeErrorCode.RuntimeMetadataNotFound, $"Entity metadata not found: {query.EntityType}");
        }

        var rangeKind = ReadStringByPath(query.ActionConfig, "retrieveSource", "range", "kind") ?? "all";
        var limit = rangeKind switch
        {
            "first" => 1,
            "custom" => ReadIntByPath(query.ActionConfig, "retrieveSource", "range", "limitExpression") ?? query.Limit,
            _ => query.Limit
        };
        var offset = rangeKind == "custom"
            ? (ReadIntByPath(query.ActionConfig, "retrieveSource", "range", "offsetExpression") ?? 0)
            : 0;
        if (string.Equals(ReadStringByPath(query.ActionConfig, "retrieveSource", "kind"), "association", StringComparison.OrdinalIgnoreCase))
        {
            return await RetrieveByAssociationAsync(query, entity, Math.Clamp(limit, 1, 500), offset, ct);
        }

        var items = await QueryEntityRowsAsync(
            entity,
            null,
            Math.Clamp(limit, 1, 500),
            Math.Max(0, offset),
            ReadSortItems(query.ActionConfig),
            ResolveEnvironment(query.RuntimeContext),
            query.RuntimeContext,
            ct);
        var payload = rangeKind == "first"
            ? items.FirstOrDefault()
            : JsonSerializer.SerializeToElement(items, JsonOptions);
        return new MicroflowRuntimeObjectStoreResult
        {
            Success = true,
            Value = payload,
            Items = items,
            ProducedVariables = BuildProducedVariables(
                ReadString(query.ActionConfig, "outputVariableName"),
                rangeKind == "first" ? ToObjectType(entity) : ToListType(entity),
                payload),
            Message = $"Retrieved {items.Count} row(s) from {entity.TableName}."
        };
    }

    public async Task<MicroflowRuntimeObjectStoreResult> CreateAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var entity = ResolveEntity(mutation.EntityType, mutation.RuntimeContext);
        if (entity is null)
        {
            return Failed(RuntimeErrorCode.RuntimeMetadataNotFound, $"Entity metadata not found: {mutation.EntityType}");
        }

        var row = BuildObjectFromCreateAction(entity, mutation);
        var commitEnabled = ReadBoolByPath(mutation.ActionConfig, "commit", "enabled");
        if (commitEnabled && !mutation.DryRun)
        {
            row = await PersistInsertAsync(entity, row, mutation.RuntimeContext, ct);
        }

        return Success(
            row,
            BuildProducedVariables(
                ReadString(mutation.ActionConfig, "outputVariableName"),
                ToObjectType(entity),
                row),
            commitEnabled && !mutation.DryRun
                ? $"Created and committed object in {entity.TableName}."
                : $"Created staged object for {entity.TableName}.");
    }

    public async Task<MicroflowRuntimeObjectStoreResult> ChangeAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var variableName = ReadString(mutation.ActionConfig, "changeVariableName")
            ?? ReadString(mutation.ActionConfig, "objectVariableName")
            ?? ReadString(mutation.ActionConfig, "objectOrListVariableName");
        if (string.IsNullOrWhiteSpace(variableName) || mutation.RuntimeContext is null || !mutation.RuntimeContext.VariableStore.TryGet(variableName!, out var existing) || existing is null)
        {
            return Failed(RuntimeErrorCode.RuntimeVariableNotFound, "ChangeMembers requires an existing object variable.");
        }

        var entity = ResolveEntity(mutation.EntityType, mutation.RuntimeContext) ?? ResolveEntityFromJson(existing.RawValueJson, mutation.RuntimeContext);
        if (entity is null)
        {
            return Failed(RuntimeErrorCode.RuntimeMetadataNotFound, $"Entity metadata not found: {mutation.EntityType}");
        }

        var currentObject = ToJsonElement(existing.RawValueJson);
        if (!currentObject.HasValue || currentObject.Value.ValueKind != JsonValueKind.Object)
        {
            return Failed(RuntimeErrorCode.RuntimeVariableTypeMismatch, $"Variable '{variableName}' is not an object.");
        }

        var updated = ApplyMemberChanges(entity, currentObject.Value, mutation.ActionConfig);
        var commitEnabled = ReadBoolByPath(mutation.ActionConfig, "commit", "enabled");
        if (commitEnabled && !mutation.DryRun)
        {
            updated = await PersistUpsertAsync(entity, updated, mutation.RuntimeContext, ct);
        }

        return Success(
            updated,
            BuildProducedVariables(variableName, ToObjectType(entity), updated),
            commitEnabled && !mutation.DryRun
                ? $"Updated and committed object in {entity.TableName}."
                : $"Updated staged object in variable '{variableName}'.");
    }

    public async Task<MicroflowRuntimeObjectStoreResult> CommitAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var variableName = ReadString(mutation.ActionConfig, "objectOrListVariableName")
            ?? ReadString(mutation.ActionConfig, "objectVariableName");
        if (string.IsNullOrWhiteSpace(variableName) || mutation.RuntimeContext is null || !mutation.RuntimeContext.VariableStore.TryGet(variableName!, out var existing) || existing is null)
        {
            return Failed(RuntimeErrorCode.RuntimeVariableNotFound, "Commit requires an existing object or list variable.");
        }

        var value = ToJsonElement(existing.RawValueJson);
        if (!value.HasValue)
        {
            return Failed(RuntimeErrorCode.RuntimeVariableTypeMismatch, $"Variable '{variableName}' is empty.");
        }

        if (value.Value.ValueKind == JsonValueKind.Array)
        {
            var committedItems = new List<JsonElement>();
            foreach (var item in value.Value.EnumerateArray())
            {
                var entity = ResolveEntityFromJson(item.GetRawText(), mutation.RuntimeContext);
                if (entity is null)
                {
                    return Failed(RuntimeErrorCode.RuntimeMetadataNotFound, "List item entity metadata not found.");
                }
                committedItems.Add(await PersistUpsertAsync(entity, item.Clone(), mutation.RuntimeContext, ct));
            }

            var committedArray = JsonSerializer.SerializeToElement(committedItems, JsonOptions);
            return Success(
                committedArray,
                BuildProducedVariables(variableName, ToListType(ResolveEntityFromJson(committedItems[0].GetRawText(), mutation.RuntimeContext)!), committedArray),
                $"Committed {committedItems.Count} object(s).");
        }

        var singleEntity = ResolveEntityFromJson(value.Value.GetRawText(), mutation.RuntimeContext) ?? ResolveEntity(mutation.EntityType, mutation.RuntimeContext);
        if (singleEntity is null)
        {
            return Failed(RuntimeErrorCode.RuntimeMetadataNotFound, $"Entity metadata not found: {mutation.EntityType}");
        }

        var committed = await PersistUpsertAsync(singleEntity, value.Value, mutation.RuntimeContext, ct);
        return Success(
            committed,
            BuildProducedVariables(variableName, ToObjectType(singleEntity), committed),
            $"Committed object in {singleEntity.TableName}.");
    }

    public async Task<MicroflowRuntimeObjectStoreResult> DeleteAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var variableName = ReadString(mutation.ActionConfig, "objectOrListVariableName")
            ?? ReadString(mutation.ActionConfig, "objectVariableName");
        if (string.IsNullOrWhiteSpace(variableName) || mutation.RuntimeContext is null || !mutation.RuntimeContext.VariableStore.TryGet(variableName!, out var existing) || existing is null)
        {
            return Failed(RuntimeErrorCode.RuntimeVariableNotFound, "Delete requires an existing object or list variable.");
        }

        var value = ToJsonElement(existing.RawValueJson);
        if (!value.HasValue)
        {
            return Failed(RuntimeErrorCode.RuntimeVariableTypeMismatch, $"Variable '{variableName}' is empty.");
        }

        var deletedCount = 0;
        if (value.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.Value.EnumerateArray())
            {
                var entity = ResolveEntityFromJson(item.GetRawText(), mutation.RuntimeContext);
                if (entity is null)
                {
                    continue;
                }
                deletedCount += await DeleteSingleAsync(entity, item.Clone(), mutation.RuntimeContext, ct) ? 1 : 0;
            }
        }
        else
        {
            var entity = ResolveEntityFromJson(value.Value.GetRawText(), mutation.RuntimeContext) ?? ResolveEntity(mutation.EntityType, mutation.RuntimeContext);
            if (entity is null)
            {
            return Failed(RuntimeErrorCode.RuntimeMetadataNotFound, $"Entity metadata not found: {mutation.EntityType}");
            }
            deletedCount += await DeleteSingleAsync(entity, value.Value, mutation.RuntimeContext, ct) ? 1 : 0;
        }

        var outputType = value.Value.ValueKind == JsonValueKind.Array
            ? JsonSerializer.SerializeToElement(new { kind = "list", itemType = new { kind = "object", entityQualifiedName = mutation.EntityType } }, JsonOptions)
            : JsonSerializer.SerializeToElement(new { kind = "object", entityQualifiedName = mutation.EntityType }, JsonOptions);
        var nullPayload = JsonSerializer.SerializeToElement<object?>(value.Value.ValueKind == JsonValueKind.Array ? Array.Empty<object>() : null, JsonOptions);
        return Success(
            nullPayload,
            BuildProducedVariables(variableName, outputType, nullPayload),
            $"Deleted {deletedCount} object(s).");
    }

    public Task<MicroflowRuntimeObjectStoreResult> RollbackAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
        => Task.FromResult(new MicroflowRuntimeObjectStoreResult
        {
            Success = true,
            Value = mutation.Value,
            Message = "Rollback is a no-op for database-backed Domain Model objects."
        });

    private async Task<MicroflowRuntimeObjectStoreResult> RetrieveByAssociationAsync(
        MicroflowRuntimeObjectQuery query,
        MetadataEntityDto targetEntity,
        int limit,
        int offset,
        CancellationToken ct)
    {
        var associationQualifiedName = ReadStringByPath(query.ActionConfig, "retrieveSource", "associationQualifiedName");
        var startVariableName = ReadStringByPath(query.ActionConfig, "retrieveSource", "startVariableName");
        if (string.IsNullOrWhiteSpace(associationQualifiedName) || string.IsNullOrWhiteSpace(startVariableName) || query.RuntimeContext is null)
        {
            return Failed(RuntimeErrorCode.RuntimeMetadataNotFound, "Association retrieve requires associationQualifiedName and startVariableName.");
        }

        if (!query.RuntimeContext.VariableStore.TryGet(startVariableName!, out var startVariable) || startVariable is null)
        {
            return Failed(RuntimeErrorCode.RuntimeVariableNotFound, $"Association start variable '{startVariableName}' not found.");
        }

        var association = query.RuntimeContext.MetadataCatalog?.Associations.FirstOrDefault(item =>
            string.Equals(item.QualifiedName, associationQualifiedName, StringComparison.OrdinalIgnoreCase));
        if (association is null || string.IsNullOrWhiteSpace(association.SourceField) || string.IsNullOrWhiteSpace(association.TargetField))
        {
            return Failed(RuntimeErrorCode.RuntimeMetadataNotFound, $"Association mapping not found for {associationQualifiedName}.");
        }

        var joinValues = ExtractJoinValues(ToJsonElement(startVariable.RawValueJson), association.SourceField!);
        if (joinValues.Count == 0)
        {
            return Success(JsonSerializer.SerializeToElement(Array.Empty<object>(), JsonOptions), Array.Empty<MicroflowRuntimeVariableValueDto>(), "No association join values available.");
        }

        var filters = new Dictionary<string, IReadOnlyList<object?>>(StringComparer.OrdinalIgnoreCase)
        {
            [association.TargetField!] = joinValues
        };
        var items = await QueryEntityRowsAsync(
            targetEntity,
            filters,
            limit,
            offset,
            ReadSortItems(query.ActionConfig),
            ResolveEnvironment(query.RuntimeContext),
            query.RuntimeContext,
            ct);
        var payload = JsonSerializer.SerializeToElement(items, JsonOptions);
        return new MicroflowRuntimeObjectStoreResult
        {
            Success = true,
            Value = payload,
            Items = items,
            ProducedVariables = BuildProducedVariables(ReadString(query.ActionConfig, "outputVariableName"), ToListType(targetEntity), payload),
            Message = $"Retrieved {items.Count} associated row(s)."
        };
    }

    private async Task<List<JsonElement>> QueryEntityRowsAsync(
        MetadataEntityDto entity,
        IReadOnlyDictionary<string, IReadOnlyList<object?>>? inFilters,
        int limit,
        int offset,
        IReadOnlyList<(string ColumnName, string Direction)> sortItems,
        AiDatabaseRecordEnvironment environment,
        RuntimeExecutionContext? runtimeContext,
        CancellationToken ct)
    {
        var (client, dialect) = await ResolveConnectionAsync(entity, environment, runtimeContext, ct);
        var sql = $"SELECT * FROM {dialect.QuoteFullName(entity.SchemaName, entity.TableName ?? entity.Name)}";
        var parameters = new List<SugarParameter>();
        if (inFilters is { Count: > 0 })
        {
            var clauses = new List<string>();
            foreach (var filter in inFilters)
            {
                if (filter.Value.Count == 0)
                {
                    continue;
                }

                var placeholders = new List<string>();
                for (var index = 0; index < filter.Value.Count; index++)
                {
                    var parameterName = $"@p_{parameters.Count}";
                    placeholders.Add(parameterName);
                    parameters.Add(new SugarParameter(parameterName, filter.Value[index] ?? DBNull.Value));
                }
                clauses.Add($"{dialect.QuoteIdentifier(filter.Key)} IN ({string.Join(", ", placeholders)})");
            }
            if (clauses.Count > 0)
            {
                sql += " WHERE " + string.Join(" AND ", clauses);
            }
        }

        if (sortItems.Count > 0)
        {
            var orderBy = sortItems
                .Select(item => $"{dialect.QuoteIdentifier(item.ColumnName)} {(string.Equals(item.Direction, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC")}")
                .ToArray();
            sql += " ORDER BY " + string.Join(", ", orderBy);
        }

        sql += $" LIMIT {Math.Clamp(limit, 1, 500)} OFFSET {Math.Max(0, offset)}";
        var table = await Task.Run(() => client.Ado.GetDataTable(sql, parameters.ToArray()), ct);
        return table.Rows.Cast<DataRow>().Select(row => ToObjectJson(entity, row)).ToList();
    }

    private async Task<JsonElement> PersistInsertAsync(
        MetadataEntityDto entity,
        JsonElement row,
        RuntimeExecutionContext? runtimeContext,
        CancellationToken ct)
    {
        var objectMap = ToObjectMap(row);
        EnsurePrimaryKey(entity, objectMap);
        var (client, dialect) = await ResolveConnectionAsync(entity, ResolveEnvironment(runtimeContext), runtimeContext, ct);
        var columns = objectMap.Keys.Where(key => !IsMetaField(key)).ToArray();
        var columnSql = string.Join(", ", columns.Select(dialect.QuoteIdentifier));
        var valueSql = string.Join(", ", columns.Select((_, index) => $"@p{index}"));
        var parameters = columns.Select((column, index) => new SugarParameter($"@p{index}", objectMap[column] ?? DBNull.Value)).ToArray();
        var sql = $"INSERT INTO {dialect.QuoteFullName(entity.SchemaName, entity.TableName ?? entity.Name)} ({columnSql}) VALUES ({valueSql})";
        await client.Ado.ExecuteCommandAsync(sql, parameters);
        return Annotate(entity, objectMap, persisted: true);
    }

    private async Task<JsonElement> PersistUpsertAsync(
        MetadataEntityDto entity,
        JsonElement row,
        RuntimeExecutionContext? runtimeContext,
        CancellationToken ct)
    {
        var objectMap = ToObjectMap(row);
        var persisted = ReadBool(row, "$persisted");
        return persisted
            ? await PersistUpdateAsync(entity, objectMap, runtimeContext, ct)
            : await PersistInsertAsync(entity, row, runtimeContext, ct);
    }

    private async Task<JsonElement> PersistUpdateAsync(
        MetadataEntityDto entity,
        Dictionary<string, object?> objectMap,
        RuntimeExecutionContext? runtimeContext,
        CancellationToken ct)
    {
        var primaryKeys = entity.Attributes.Where(attribute => attribute.PrimaryKey).ToArray();
        if (primaryKeys.Length == 0)
        {
            throw new InvalidOperationException($"Entity {entity.QualifiedName} does not define a primary key.");
        }

        var (client, dialect) = await ResolveConnectionAsync(entity, ResolveEnvironment(runtimeContext), runtimeContext, ct);
        var updatableColumns = entity.Attributes
            .Where(attribute => !attribute.PrimaryKey && objectMap.ContainsKey(attribute.ColumnName ?? attribute.Name))
            .ToArray();
        var setClauses = updatableColumns.Select((attribute, index) => $"{dialect.QuoteIdentifier(attribute.ColumnName ?? attribute.Name)}=@v{index}").ToArray();
        var whereClauses = primaryKeys.Select((attribute, index) => $"{dialect.QuoteIdentifier(attribute.ColumnName ?? attribute.Name)}=@k{index}").ToArray();
        var parameters = new List<SugarParameter>();
        parameters.AddRange(updatableColumns.Select((attribute, index) => new SugarParameter($"@v{index}", objectMap[attribute.ColumnName ?? attribute.Name] ?? DBNull.Value)));
        parameters.AddRange(primaryKeys.Select((attribute, index) => new SugarParameter($"@k{index}", objectMap[attribute.ColumnName ?? attribute.Name] ?? DBNull.Value)));
        var sql = $"UPDATE {dialect.QuoteFullName(entity.SchemaName, entity.TableName ?? entity.Name)} SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)}";
        await client.Ado.ExecuteCommandAsync(sql, parameters.ToArray());
        return Annotate(entity, objectMap, persisted: true);
    }

    private async Task<bool> DeleteSingleAsync(
        MetadataEntityDto entity,
        JsonElement row,
        RuntimeExecutionContext? runtimeContext,
        CancellationToken ct)
    {
        var objectMap = ToObjectMap(row);
        var primaryKeys = entity.Attributes.Where(attribute => attribute.PrimaryKey).ToArray();
        if (primaryKeys.Length == 0)
        {
            return false;
        }

        var (client, dialect) = await ResolveConnectionAsync(entity, ResolveEnvironment(runtimeContext), runtimeContext, ct);
        var whereClauses = primaryKeys.Select((attribute, index) => $"{dialect.QuoteIdentifier(attribute.ColumnName ?? attribute.Name)}=@k{index}").ToArray();
        var parameters = primaryKeys.Select((attribute, index) => new SugarParameter($"@k{index}", objectMap[attribute.ColumnName ?? attribute.Name] ?? DBNull.Value)).ToArray();
        var sql = $"DELETE FROM {dialect.QuoteFullName(entity.SchemaName, entity.TableName ?? entity.Name)} WHERE {string.Join(" AND ", whereClauses)}";
        await client.Ado.ExecuteCommandAsync(sql, parameters);
        return true;
    }

    private static JsonElement BuildObjectFromCreateAction(MetadataEntityDto entity, MicroflowRuntimeObjectMutation mutation)
    {
        var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (mutation.ActionConfig.ValueKind == JsonValueKind.Object && mutation.ActionConfig.TryGetProperty("memberChanges", out var memberChanges) && memberChanges.ValueKind == JsonValueKind.Array)
        {
            foreach (var change in memberChanges.EnumerateArray())
            {
                if (!TryReadMemberChange(change, entity, mutation.RuntimeContext, out var fieldName, out var value))
                {
                    continue;
                }
                map[fieldName] = value;
            }
        }

        return Annotate(entity, map, persisted: false);
    }

    private static JsonElement ApplyMemberChanges(MetadataEntityDto entity, JsonElement current, JsonElement actionConfig)
    {
        var map = ToObjectMap(current);
        if (actionConfig.ValueKind == JsonValueKind.Object && actionConfig.TryGetProperty("memberChanges", out var memberChanges) && memberChanges.ValueKind == JsonValueKind.Array)
        {
            foreach (var change in memberChanges.EnumerateArray())
            {
                if (!TryReadMemberChange(change, entity, null, out var fieldName, out var value))
                {
                    continue;
                }
                map[fieldName] = value;
            }
        }

        return Annotate(entity, map, persisted: ReadBool(current, "$persisted"));
    }

    private static bool TryReadMemberChange(
        JsonElement change,
        MetadataEntityDto entity,
        RuntimeExecutionContext? runtimeContext,
        out string fieldName,
        out object? value)
    {
        fieldName = string.Empty;
        value = null;
        var memberQualifiedName = ReadString(change, "memberQualifiedName");
        if (string.IsNullOrWhiteSpace(memberQualifiedName))
        {
            return false;
        }

        var normalizedLeaf = memberQualifiedName
            .Split(['.', '/'], StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();
        var attribute = entity.Attributes.FirstOrDefault(item =>
            string.Equals($"{entity.QualifiedName}.{item.Name}", memberQualifiedName, StringComparison.OrdinalIgnoreCase)
            || string.Equals($"{entity.QualifiedName}/{item.Name}", memberQualifiedName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.ColumnName, memberQualifiedName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.Name, normalizedLeaf, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.ColumnName, normalizedLeaf, StringComparison.OrdinalIgnoreCase));
        if (attribute is null)
        {
            return false;
        }

        fieldName = attribute.ColumnName ?? attribute.Name;
        var rawExpression = ReadStringByPath(change, "valueExpression", "raw");
        value = EvaluateSimpleValue(rawExpression, runtimeContext);
        return true;
    }

    private static object? EvaluateSimpleValue(string? rawExpression, RuntimeExecutionContext? runtimeContext)
    {
        if (string.IsNullOrWhiteSpace(rawExpression))
        {
            return null;
        }

        var raw = rawExpression.Trim();
        if ((raw.StartsWith("'") && raw.EndsWith("'")) || (raw.StartsWith("\"") && raw.EndsWith("\"")))
        {
            return raw[1..^1];
        }
        if (runtimeContext?.VariableStore.TryGet(raw, out var variable) == true && variable is not null)
        {
            var json = ToJsonElement(variable.RawValueJson);
            return json.HasValue ? ToClrValue(json.Value) : variable.RawValueJson;
        }
        if (bool.TryParse(raw, out var booleanValue))
        {
            return booleanValue;
        }
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            return longValue;
        }
        if (decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalValue))
        {
            return decimalValue;
        }
        return raw;
    }

    private async Task<(SqlSugarClient Client, IDatabaseDialect Dialect)> ResolveConnectionAsync(
        MetadataEntityDto entity,
        AiDatabaseRecordEnvironment environment,
        RuntimeExecutionContext? runtimeContext,
        CancellationToken ct)
    {
        if (!long.TryParse(entity.AiDatabaseId, out var databaseId) || databaseId <= 0)
        {
            throw new InvalidOperationException($"Entity {entity.QualifiedName} is missing aiDatabaseId mapping.");
        }

        var tenantId = ParseTenantId(runtimeContext?.RuntimeSecurityContext.TenantId);
        var client = await _clientFactory.GetClientAsync(tenantId, databaseId, environment, ct);
        var dialect = _dialects.Resolve(entity.DriverCode ?? "SQLite");
        return (client, dialect);
    }

    private static MetadataEntityDto? ResolveEntity(string qualifiedName, RuntimeExecutionContext? runtimeContext)
        => runtimeContext?.MetadataCatalog?.Entities.FirstOrDefault(entity => string.Equals(entity.QualifiedName, qualifiedName, StringComparison.OrdinalIgnoreCase));

    private static MetadataEntityDto? ResolveEntityFromJson(string? rawValueJson, RuntimeExecutionContext? runtimeContext)
    {
        var json = ToJsonElement(rawValueJson);
        var entityName = json.HasValue ? ReadString(json.Value, "$entity") : null;
        return string.IsNullOrWhiteSpace(entityName) ? null : ResolveEntity(entityName!, runtimeContext);
    }

    private static AiDatabaseRecordEnvironment ResolveEnvironment(RuntimeExecutionContext? runtimeContext)
        => string.Equals(runtimeContext?.Mode, MicroflowRuntimeExecutionMode.PublishedRun, StringComparison.OrdinalIgnoreCase)
            ? AiDatabaseRecordEnvironment.Online
            : AiDatabaseRecordEnvironment.Draft;

    private static TenantId ParseTenantId(string? tenantId)
        => Guid.TryParse(tenantId, out var parsed) ? new TenantId(parsed) : TenantId.Empty;

    private static JsonElement Annotate(MetadataEntityDto entity, IDictionary<string, object?> payload, bool persisted)
    {
        var map = new Dictionary<string, object?>(payload, StringComparer.OrdinalIgnoreCase)
        {
            ["$entity"] = entity.QualifiedName,
            ["$persisted"] = persisted
        };
        return JsonSerializer.SerializeToElement(map, JsonOptions);
    }

    private static void EnsurePrimaryKey(MetadataEntityDto entity, IDictionary<string, object?> payload)
    {
        foreach (var attribute in entity.Attributes.Where(attribute => attribute.PrimaryKey))
        {
            var key = attribute.ColumnName ?? attribute.Name;
            if (payload.ContainsKey(key) && payload[key] is not null)
            {
                continue;
            }

            payload[key] = attribute.Type.ValueKind == JsonValueKind.Object && ReadString(attribute.Type, "kind") == "integer"
                ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                : Guid.NewGuid().ToString("N");
        }
    }

    private static JsonElement ToObjectJson(MetadataEntityDto entity, DataRow row)
    {
        var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (DataColumn column in row.Table.Columns)
        {
            map[column.ColumnName] = row[column] is DBNull ? null : row[column];
        }
        return Annotate(entity, map, persisted: true);
    }

    private static Dictionary<string, object?> ToObjectMap(JsonElement json)
    {
        var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (json.ValueKind != JsonValueKind.Object)
        {
            return map;
        }

        foreach (var property in json.EnumerateObject())
        {
            map[property.Name] = ToClrValue(property.Value);
        }
        return map;
    }

    private static object? ToClrValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when value.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.String => value.GetString(),
            _ => value.GetRawText()
        };

    private static JsonElement? ToJsonElement(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<object?> ExtractJoinValues(JsonElement? source, string fieldName)
    {
        if (!source.HasValue)
        {
            return Array.Empty<object?>();
        }

        var result = new List<object?>();
        if (source.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in source.Value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty(fieldName, out var fieldValue))
                {
                    result.Add(ToClrValue(fieldValue));
                }
            }
        }
        else if (source.Value.ValueKind == JsonValueKind.Object && source.Value.TryGetProperty(fieldName, out var singleValue))
        {
            result.Add(ToClrValue(singleValue));
        }

        return result.Where(item => item is not null).Distinct().ToArray();
    }

    private static IReadOnlyList<(string ColumnName, string Direction)> ReadSortItems(JsonElement actionConfig)
    {
        if (actionConfig.ValueKind != JsonValueKind.Object
            || !actionConfig.TryGetProperty("retrieveSource", out var retrieveSource)
            || retrieveSource.ValueKind != JsonValueKind.Object
            || !retrieveSource.TryGetProperty("sortItemList", out var sortItemList)
            || sortItemList.ValueKind != JsonValueKind.Object
            || !sortItemList.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<(string ColumnName, string Direction)>();
        }

        var result = new List<(string ColumnName, string Direction)>();
        foreach (var item in items.EnumerateArray())
        {
            var qualifiedName = ReadString(item, "attributeQualifiedName");
            if (string.IsNullOrWhiteSpace(qualifiedName))
            {
                continue;
            }

            var columnName = qualifiedName!.Split('.').LastOrDefault();
            if (string.IsNullOrWhiteSpace(columnName))
            {
                continue;
            }

            result.Add((columnName!, ReadString(item, "direction") ?? "asc"));
        }

        return result;
    }

    private static bool IsMetaField(string key)
        => key.StartsWith("$", StringComparison.Ordinal);

    private static IReadOnlyList<MicroflowRuntimeVariableValueDto> BuildProducedVariables(string? variableName, JsonElement type, JsonElement payload)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return Array.Empty<MicroflowRuntimeVariableValueDto>();
        }

        return
        [
            new MicroflowRuntimeVariableValueDto
            {
                Name = variableName!,
                Type = type,
                RawValue = payload,
                RawValueJson = payload.GetRawText(),
                ValuePreview = payload.ValueKind == JsonValueKind.Array ? $"List[{payload.GetArrayLength()}]" : payload.ValueKind.ToString(),
                Source = MicroflowVariableSourceKind.ActionOutput
            }
        ];
    }

    private static JsonElement ToObjectType(MetadataEntityDto entity)
        => JsonSerializer.SerializeToElement(new { kind = "object", entityQualifiedName = entity.QualifiedName }, JsonOptions);

    private static JsonElement ToListType(MetadataEntityDto entity)
        => JsonSerializer.SerializeToElement(new { kind = "list", itemType = new { kind = "object", entityQualifiedName = entity.QualifiedName } }, JsonOptions);

    private static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    private static string? ReadStringByPath(JsonElement element, params string[] path)
    {
        if (element.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static bool ReadBool(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.True;

    private static bool ReadBoolByPath(JsonElement element, params string[] path)
    {
        if (element.ValueKind == JsonValueKind.Undefined)
        {
            return false;
        }

        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return false;
            }
        }

        return current.ValueKind == JsonValueKind.True;
    }

    private static int? ReadIntByPath(JsonElement element, params string[] path)
    {
        if (element.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        if (current.ValueKind == JsonValueKind.Number && current.TryGetInt32(out var number))
        {
            return number;
        }

        return current.ValueKind == JsonValueKind.Object
            && current.TryGetProperty("raw", out var raw)
            && raw.ValueKind == JsonValueKind.String
            && int.TryParse(raw.GetString(), out var parsed)
                ? parsed
                : null;
    }

    private static MicroflowRuntimeObjectStoreResult Success(
        JsonElement value,
        IReadOnlyList<MicroflowRuntimeVariableValueDto> producedVariables,
        string message)
        => new()
        {
            Success = true,
            Value = value,
            ProducedVariables = producedVariables,
            Message = message
        };

    private static MicroflowRuntimeObjectStoreResult Failed(string code, string message)
        => new()
        {
            Success = false,
            Code = code,
            Message = message
        };
}
