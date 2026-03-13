using System.Text;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class SqliteVectorStore : IVectorStore
{
    private const string MetadataTableName = "__vector_collections";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _connectionString;

    public SqliteVectorStore(IHostEnvironment hostEnvironment)
    {
        var dbPath = Path.Combine(hostEnvironment.ContentRootPath, "vectors.db");
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task EnsureCollectionAsync(string collectionName, int dimensions, CancellationToken ct = default)
    {
        if (dimensions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions), "Vector dimensions must be greater than zero.");
        }

        var safeTable = GetSafeCollectionName(collectionName);
        await using var connection = await OpenConnectionAsync(ct);

        var sql = $"""
                   CREATE TABLE IF NOT EXISTS "{MetadataTableName}" (
                       collection_name TEXT PRIMARY KEY,
                       dimensions INTEGER NOT NULL
                   );
                   INSERT INTO "{MetadataTableName}"(collection_name, dimensions)
                   VALUES ($collectionName, $dimensions)
                   ON CONFLICT(collection_name) DO UPDATE SET dimensions = excluded.dimensions;

                   CREATE TABLE IF NOT EXISTS "{safeTable}" (
                       id TEXT PRIMARY KEY,
                       vector BLOB NOT NULL,
                       content TEXT NOT NULL,
                       metadata TEXT NULL
                   );
                   """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$collectionName", safeTable);
        command.Parameters.AddWithValue("$dimensions", dimensions);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task UpsertAsync(string collectionName, IEnumerable<VectorRecord> records, CancellationToken ct = default)
    {
        var materialized = records as VectorRecord[] ?? records.ToArray();
        if (materialized.Length == 0)
        {
            return;
        }

        var safeTable = GetSafeCollectionName(collectionName);
        await EnsureCollectionExistsAsync(safeTable, ct);

        var parameterBuilder = new StringBuilder();
        var parameters = new List<SqliteParameter>(materialized.Length * 4);
        for (var i = 0; i < materialized.Length; i++)
        {
            var record = materialized[i];
            ValidateRecord(record);

            if (i > 0)
            {
                parameterBuilder.Append(", ");
            }

            parameterBuilder.Append($"($id{i}, $vector{i}, $content{i}, $metadata{i})");
            parameters.Add(new SqliteParameter($"$id{i}", record.Id));
            parameters.Add(new SqliteParameter($"$vector{i}", SerializeVector(record.Vector)));
            parameters.Add(new SqliteParameter($"$content{i}", record.Content));
            parameters.Add(new SqliteParameter(
                $"$metadata{i}",
                (object?)SerializeMetadata(record.Metadata) ?? DBNull.Value));
        }

        var sql = $"""
                   INSERT INTO "{safeTable}"(id, vector, content, metadata)
                   VALUES {parameterBuilder}
                   ON CONFLICT(id) DO UPDATE SET
                       vector = excluded.vector,
                       content = excluded.content,
                       metadata = excluded.metadata;
                   """;

        await using var connection = await OpenConnectionAsync(ct);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collectionName,
        float[] queryVector,
        int topK = 5,
        CancellationToken ct = default)
    {
        if (queryVector.Length == 0)
        {
            throw new ArgumentException("Query vector cannot be empty.", nameof(queryVector));
        }

        if (topK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), "topK must be greater than zero.");
        }

        var safeTable = GetSafeCollectionName(collectionName);
        await EnsureCollectionExistsAsync(safeTable, ct);

        var sql = $"""SELECT id, vector, content, metadata FROM "{safeTable}";""";
        await using var connection = await OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var results = new List<VectorSearchResult>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetString(0);
            var vector = DeserializeVector((byte[])reader["vector"]);
            var content = reader.GetString(2);
            var metadata = reader.IsDBNull(3)
                ? null
                : DeserializeMetadata(reader.GetString(3));

            var score = CosineSimilarity(queryVector, vector);
            results.Add(new VectorSearchResult(id, content, score, metadata));
        }

        return results
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToArray();
    }

    public async Task DeleteAsync(string collectionName, IEnumerable<string> ids, CancellationToken ct = default)
    {
        var idList = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (idList.Length == 0)
        {
            return;
        }

        var safeTable = GetSafeCollectionName(collectionName);
        await EnsureCollectionExistsAsync(safeTable, ct);

        var placeholders = string.Join(", ", idList.Select((_, i) => $"$id{i}"));
        var sql = $"""DELETE FROM "{safeTable}" WHERE id IN ({placeholders});""";

        await using var connection = await OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        for (var i = 0; i < idList.Length; i++)
        {
            command.Parameters.AddWithValue($"$id{i}", idList[i]);
        }

        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task EnsureCollectionExistsAsync(string safeCollectionName, CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = $collectionName;""";
        command.Parameters.AddWithValue("$collectionName", safeCollectionName);
        var count = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        if (count <= 0)
        {
            throw new InvalidOperationException(
                $"Vector collection '{safeCollectionName}' does not exist. Call EnsureCollectionAsync first.");
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private static void ValidateRecord(VectorRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.Id))
        {
            throw new ArgumentException("Vector record id cannot be empty.", nameof(record));
        }

        if (record.Vector.Length == 0)
        {
            throw new ArgumentException("Vector cannot be empty.", nameof(record));
        }
    }

    private static string GetSafeCollectionName(string collectionName)
    {
        if (string.IsNullOrWhiteSpace(collectionName))
        {
            throw new ArgumentException("Collection name cannot be empty.", nameof(collectionName));
        }

        var trimmed = collectionName.Trim();
        var isSafe = trimmed.All(ch => char.IsLetterOrDigit(ch) || ch == '_');
        if (!isSafe)
        {
            throw new ArgumentException("Collection name only allows letters, numbers and underscore.", nameof(collectionName));
        }

        return $"vec_{trimmed.ToLowerInvariant()}";
    }

    private static byte[] SerializeVector(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] DeserializeVector(byte[] bytes)
    {
        if (bytes.Length % sizeof(float) != 0)
        {
            throw new InvalidOperationException("Stored vector has invalid binary length.");
        }

        var vector = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }

    private static string? SerializeMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(metadata, JsonOptions);
    }

    private static IReadOnlyDictionary<string, string>? DeserializeMetadata(string metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(metadata, JsonOptions);
    }

    private static float CosineSimilarity(float[] left, float[] right)
    {
        if (left.Length != right.Length)
        {
            throw new InvalidOperationException("Vector dimensions do not match.");
        }

        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;
        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            leftNorm += left[i] * left[i];
            rightNorm += right[i] * right[i];
        }

        if (leftNorm == 0 || rightNorm == 0)
        {
            return 0;
        }

        return (float)(dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm)));
    }
}
