using System.Collections.Concurrent;
using System.Globalization;
using ClosedXML.Excel;
using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class DynamicRecordImportService : IDynamicRecordImportService
{
    private static readonly ConcurrentDictionary<string, ImportSessionState> Sessions = new();

    private readonly IDynamicTableQueryService _tableQueryService;
    private readonly IDynamicRecordCommandService _recordCommandService;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly TimeProvider _timeProvider;

    public DynamicRecordImportService(
        IDynamicTableQueryService tableQueryService,
        IDynamicRecordCommandService recordCommandService,
        IAppContextAccessor appContextAccessor,
        TimeProvider timeProvider)
    {
        _tableQueryService = tableQueryService;
        _recordCommandService = recordCommandService;
        _appContextAccessor = appContextAccessor;
        _timeProvider = timeProvider;
    }

    public async Task<DynamicRecordImportAnalyzeResult> AnalyzeAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicRecordImportRequest request,
        CancellationToken cancellationToken)
    {
        var appId = _appContextAccessor.ResolveAppId();
        var fields = await _tableQueryService.GetFieldsAsync(tenantId, tableKey, appId, cancellationToken);
        var parsed = ParseRows(request.Format, request.Content);
        if (parsed.Count == 0)
        {
            throw new InvalidOperationException("Import content is empty.");
        }

        var headers = parsed[0];
        var mappings = BuildSuggestedMappings(headers, fields.Select(x => x.Name).ToArray());
        var previewRows = parsed.Skip(1).Take(20)
            .Select(row => BuildPreviewRow(headers, row))
            .ToArray();

        CleanupExpiredSessions();
        var sessionId = Guid.NewGuid().ToString("N");
        Sessions[sessionId] = new ImportSessionState(
            sessionId,
            tenantId.Value.ToString(),
            userId,
            tableKey,
            NormalizeFormat(request.Format),
            parsed,
            _timeProvider.GetUtcNow().AddMinutes(30));

        return new DynamicRecordImportAnalyzeResult(
            sessionId,
            NormalizeFormat(request.Format),
            headers,
            mappings,
            previewRows.Length,
            previewRows);
    }

    public async Task<DynamicRecordImportResult> CommitAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicRecordImportCommitRequest request,
        CancellationToken cancellationToken)
    {
        if (!Sessions.TryGetValue(request.SessionId, out var session))
        {
            throw new InvalidOperationException("Import session not found or expired.");
        }

        if (!string.Equals(session.TenantId, tenantId.Value.ToString(), StringComparison.Ordinal)
            || session.UserId != userId
            || !string.Equals(session.TableKey, tableKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Import session is invalid for current context.");
        }

        var appId = _appContextAccessor.ResolveAppId();
        var fields = await _tableQueryService.GetFieldsAsync(tenantId, tableKey, appId, cancellationToken);
        var fieldMap = fields.ToDictionary(field => field.Name, StringComparer.OrdinalIgnoreCase);
        var rows = session.Rows;
        if (rows.Count <= 1)
        {
            return new DynamicRecordImportResult(0, 0, 0, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<DynamicRecordImportRowError>(), request.SessionId);
        }

        var mappings = ResolveMappings(rows[0], request.Mappings);
        var imported = 0;
        var skipped = 0;
        var warnings = new List<string>();
        var errors = new List<string>();
        var rowErrors = new List<DynamicRecordImportRowError>();
        var batchSize = request.BatchSize <= 0 ? 200 : Math.Min(request.BatchSize, 1000);
        var pendingRows = new List<(int RowIndex, List<DynamicFieldValueDto> Values)>(batchSize);

        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            var raw = rows[rowIndex];
            var values = new List<DynamicFieldValueDto>();
            foreach (var mapping in mappings)
            {
                if (!fieldMap.TryGetValue(mapping.TargetField, out var fieldDef))
                {
                    warnings.Add($"Row {rowIndex}: field '{mapping.TargetField}' not found in table definition.");
                    continue;
                }

                var sourceIndex = FindHeaderIndex(rows[0], mapping.SourceField);
                if (sourceIndex < 0 || sourceIndex >= raw.Count)
                {
                    continue;
                }

                var cell = raw[sourceIndex];
                var parseResult = TryToValue(fieldDef.FieldType, fieldDef.Name, cell);
                if (!parseResult.Success)
                {
                    skipped += 1;
                    rowErrors.Add(new DynamicRecordImportRowError(rowIndex, fieldDef.Name, "FIELD_PARSE_ERROR", parseResult.ErrorMessage ?? "Parse failed"));
                    goto NextRow;
                }

                values.Add(parseResult.Value!);
            }

            if (values.Count == 0)
            {
                skipped += 1;
                rowErrors.Add(new DynamicRecordImportRowError(rowIndex, null, "EMPTY_ROW", "No mapped value found."));
                goto NextRow;
            }

            pendingRows.Add((rowIndex, values));
            if (pendingRows.Count >= batchSize)
            {
                var result = await FlushRowsAsync(tenantId, userId, tableKey, pendingRows, request.DryRun, cancellationToken);
                imported += result.Imported;
                skipped += result.Skipped;
                errors.AddRange(result.Errors);
                rowErrors.AddRange(result.RowErrors);
                pendingRows.Clear();
            }

        NextRow:
            continue;
        }

        if (pendingRows.Count > 0)
        {
            var result = await FlushRowsAsync(tenantId, userId, tableKey, pendingRows, request.DryRun, cancellationToken);
            imported += result.Imported;
            skipped += result.Skipped;
            errors.AddRange(result.Errors);
            rowErrors.AddRange(result.RowErrors);
        }

        if (!request.DryRun)
        {
            Sessions.TryRemove(request.SessionId, out _);
        }

        return new DynamicRecordImportResult(rows.Count - 1, imported, skipped, warnings, errors, rowErrors, request.SessionId);
    }

    public Task<DynamicRecordImportResult> PasteFromExcelAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicRecordImportRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = request with { Format = "tsv" };
        return ImportAsync(tenantId, userId, tableKey, normalized, cancellationToken);
    }

    public async Task<DynamicRecordImportResult> ImportAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicRecordImportRequest request,
        CancellationToken cancellationToken)
    {
        var analyze = await AnalyzeAsync(tenantId, userId, tableKey, request, cancellationToken);
        return await CommitAsync(
            tenantId,
            userId,
            tableKey,
            new DynamicRecordImportCommitRequest(
                analyze.SessionId,
                request.DryRun,
                200,
                request.Mappings ?? analyze.SuggestedMappings),
            cancellationToken);
    }

    private async Task<FlushResult> FlushRowsAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        List<(int RowIndex, List<DynamicFieldValueDto> Values)> rows,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var imported = 0;
        var skipped = 0;
        var errors = new List<string>();
        var rowErrors = new List<DynamicRecordImportRowError>();
        if (dryRun)
        {
            imported = rows.Count;
            return new FlushResult(imported, skipped, errors, rowErrors);
        }

        foreach (var row in rows)
        {
            try
            {
                await _recordCommandService.CreateAsync(tenantId, userId, tableKey, new DynamicRecordUpsertRequest(row.Values), cancellationToken);
                imported += 1;
            }
            catch (Exception ex)
            {
                skipped += 1;
                errors.Add($"Row {row.RowIndex}: {ex.Message}");
                rowErrors.Add(new DynamicRecordImportRowError(row.RowIndex, null, "CREATE_FAILED", ex.Message));
            }
        }

        return new FlushResult(imported, skipped, errors, rowErrors);
    }

    private static List<DynamicRecordImportFieldMapping> ResolveMappings(
        IReadOnlyList<string> headers,
        IReadOnlyList<DynamicRecordImportFieldMapping>? mappings)
    {
        if (mappings is { Count: > 0 })
        {
            return mappings.ToList();
        }

        return headers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => new DynamicRecordImportFieldMapping(x.Trim(), x.Trim()))
            .ToList();
    }

    private static int FindHeaderIndex(IReadOnlyList<string> headers, string sourceField)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            if (string.Equals(headers[i], sourceField, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static Dictionary<string, string?> BuildPreviewRow(IReadOnlyList<string> headers, IReadOnlyList<string> row)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            var header = headers[i];
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            result[header] = i < row.Count ? row[i] : null;
        }

        return result;
    }

    private static IReadOnlyList<DynamicRecordImportFieldMapping> BuildSuggestedMappings(IReadOnlyList<string> headers, IReadOnlyList<string> fields)
    {
        var fieldSet = fields.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return headers
            .Where(header => !string.IsNullOrWhiteSpace(header) && fieldSet.Contains(header))
            .Select(header => new DynamicRecordImportFieldMapping(header, header))
            .ToArray();
    }

    private static List<List<string>> ParseRows(string format, string content)
    {
        var normalized = NormalizeFormat(format);
        return normalized switch
        {
            "xlsx" => ParseExcelRows(content),
            "tsv" or "excel" => ParseDelimitedRows(content, '\t'),
            _ => ParseDelimitedRows(content, ',')
        };
    }

    private static string NormalizeFormat(string format)
    {
        return (format ?? "csv").Trim().ToLowerInvariant() switch
        {
            "xls" or "xlsx" => "xlsx",
            "excel" => "tsv",
            "tsv" => "tsv",
            _ => "csv"
        };
    }

    private static List<List<string>> ParseExcelRows(string base64Content)
    {
        if (string.IsNullOrWhiteSpace(base64Content))
        {
            return new List<List<string>>();
        }

        var bytes = Convert.FromBase64String(base64Content);
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet is null)
        {
            return new List<List<string>>();
        }

        var rows = new List<List<string>>();
        var range = worksheet.RangeUsed();
        if (range is null)
        {
            return rows;
        }

        foreach (var row in range.Rows())
        {
            var values = row.Cells().Select(cell => cell.GetFormattedString().Trim()).ToList();
            if (values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(values);
        }

        return rows;
    }

    private static List<List<string>> ParseDelimitedRows(string content, char delimiter)
    {
        var rows = new List<List<string>>();
        if (string.IsNullOrWhiteSpace(content))
        {
            return rows;
        }

        var current = new List<string>();
        var value = new System.Text.StringBuilder();
        var inQuote = false;

        for (var i = 0; i < content.Length; i++)
        {
            var ch = content[i];
            if (ch == '"')
            {
                if (inQuote && i + 1 < content.Length && content[i + 1] == '"')
                {
                    value.Append('"');
                    i++;
                }
                else
                {
                    inQuote = !inQuote;
                }

                continue;
            }

            if (!inQuote && ch == delimiter)
            {
                current.Add(value.ToString().Trim());
                value.Clear();
                continue;
            }

            if (!inQuote && (ch == '\n' || ch == '\r'))
            {
                if (ch == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                {
                    i++;
                }

                current.Add(value.ToString().Trim());
                value.Clear();
                if (current.Any(x => !string.IsNullOrWhiteSpace(x)))
                {
                    rows.Add(current);
                }

                current = new List<string>();
                continue;
            }

            value.Append(ch);
        }

        if (value.Length > 0 || current.Count > 0)
        {
            current.Add(value.ToString().Trim());
            if (current.Any(x => !string.IsNullOrWhiteSpace(x)))
            {
                rows.Add(current);
            }
        }

        return rows;
    }

    private static ParseValueResult TryToValue(string fieldType, string fieldName, string raw)
    {
        var normalized = fieldType switch
        {
            "Text" => "String",
            _ => fieldType
        };

        var dto = new DynamicFieldValueDto
        {
            Field = fieldName,
            ValueType = normalized
        };

        switch (normalized)
        {
            case "Int":
                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                {
                    return ParseValueResult.Fail($"Field '{fieldName}' expects Int.");
                }
                dto = dto with { IntValue = intValue };
                break;
            case "Long":
                if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
                {
                    return ParseValueResult.Fail($"Field '{fieldName}' expects Long.");
                }
                dto = dto with { LongValue = longValue };
                break;
            case "Decimal":
                if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue))
                {
                    return ParseValueResult.Fail($"Field '{fieldName}' expects Decimal.");
                }
                dto = dto with { DecimalValue = decimalValue };
                break;
            case "Bool":
                if (!bool.TryParse(raw, out var boolValue))
                {
                    return ParseValueResult.Fail($"Field '{fieldName}' expects Bool.");
                }
                dto = dto with { BoolValue = boolValue };
                break;
            case "Date":
                if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateValue))
                {
                    return ParseValueResult.Fail($"Field '{fieldName}' expects Date.");
                }
                dto = dto with { DateValue = dateValue };
                break;
            case "DateTime":
                if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateTimeValue))
                {
                    return ParseValueResult.Fail($"Field '{fieldName}' expects DateTime.");
                }
                dto = dto with { DateTimeValue = dateTimeValue };
                break;
            default:
                dto = dto with { ValueType = "String", StringValue = raw };
                break;
        }

        return ParseValueResult.Ok(dto);
    }

    private static void CleanupExpiredSessions()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in Sessions)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                Sessions.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed record ImportSessionState(
        string SessionId,
        string TenantId,
        long UserId,
        string TableKey,
        string Format,
        List<List<string>> Rows,
        DateTimeOffset ExpiresAt);

    private sealed record ParseValueResult(bool Success, DynamicFieldValueDto? Value, string? ErrorMessage)
    {
        public static ParseValueResult Ok(DynamicFieldValueDto value) => new(true, value, null);
        public static ParseValueResult Fail(string message) => new(false, null, message);
    }

    private sealed record FlushResult(
        int Imported,
        int Skipped,
        IReadOnlyList<string> Errors,
        IReadOnlyList<DynamicRecordImportRowError> RowErrors);
}
