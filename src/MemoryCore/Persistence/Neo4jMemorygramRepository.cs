using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Neo4j.Driver;

namespace Mnemosyne.Core.Persistence
{
    public class Neo4jMemorygramRepository : IMemorygramRepository
    {
        private readonly IDriver _driver;
        private readonly ILogger<Neo4jMemorygramRepository> _logger;

        public Neo4jMemorygramRepository(IDriver driver, ILogger<Neo4jMemorygramRepository> logger)
        {
            _driver = driver;
            _logger = logger;
        }

        public async Task<Result<Memorygram>> CreateOrUpdateMemorygramAsync(Memorygram memorygram)
        {
            try
            {
                await using var session = _driver.AsyncSession();
                return await session.ExecuteWriteAsync(async tx =>
                {
                    var query = @"
                        MERGE (m:Memorygram {id: $id})
                        ON CREATE SET
                            m.content = $content,
                            m.vectorEmbedding = $vectorEmbedding,
                            m.type = $type,
                            m.createdAt = datetime(),
                            m.updatedAt = datetime()
                        ON MATCH SET
                            m.content = $content,
                            m.vectorEmbedding = $vectorEmbedding,
                            m.type = $type,
                            m.updatedAt = datetime()
                        RETURN m.id, m.content, m.vectorEmbedding, m.type, m.createdAt, m.updatedAt";
                    
                    var parameters = new
                    {
                        id = memorygram.Id.ToString(),
                        content = memorygram.Content,
                        vectorEmbedding = memorygram.VectorEmbedding,
                        type = memorygram.Type.ToString()
                    };
                    
                    var cursor = await tx.RunAsync(query, parameters);
                    if (await cursor.FetchAsync())
                    {
                        var record = cursor.Current.Values;
                        return Result.Ok(new Memorygram(
                            Guid.Parse(record["m.id"].As<string>()),
                            record["m.content"].As<string>(),
                            Enum.Parse<MemorygramType>(record["m.type"].As<string>()),
                            ConvertToFloatArray(record["m.vectorEmbedding"]),
                            ConvertToDateTime(record["m.createdAt"]),
                            ConvertToDateTime(record["m.updatedAt"])
                        ));
                    }

                    return Result.Fail<Memorygram>("not found");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create or update memorygram");
                return Result.Fail<Memorygram>($"Database error: {ex.Message}");
            }
        }

        public async Task<Result<Memorygram>> CreateAssociationAsync(Guid fromId, Guid toId, float weight)
        {
            try
            {
                await using var session = _driver.AsyncSession();
                
                // First check if source memorygram exists
                var sourceExists = await session.ExecuteReadAsync(async tx =>
                {
                    var query = "MATCH (m:Memorygram {id: $id}) RETURN count(m) > 0 as exists";
                    var parameters = new { id = fromId.ToString() };
                    var cursor = await tx.RunAsync(query, parameters);
                    if (await cursor.FetchAsync())
                    {
                        return cursor.Current["exists"].As<bool>();
                    }
                    return false;
                });
                
                if (!sourceExists)
                {
                    _logger.LogWarning("Source Memorygram with ID {Id} not found", fromId);
                    return Result.Fail<Memorygram>($"Memorygram with ID {fromId} not found");
                }
                
                // Then check if target memorygram exists
                var targetExists = await session.ExecuteReadAsync(async tx =>
                {
                    var query = "MATCH (m:Memorygram {id: $id}) RETURN count(m) > 0 as exists";
                    var parameters = new { id = toId.ToString() };
                    var cursor = await tx.RunAsync(query, parameters);
                    if (await cursor.FetchAsync())
                    {
                        return cursor.Current["exists"].As<bool>();
                    }
                    return false;
                });
                
                if (!targetExists)
                {
                    _logger.LogWarning("Target Memorygram with ID {Id} not found", toId);
                    return Result.Fail<Memorygram>($"Memorygram with ID {toId} not found");
                }
                
                // If both exist, create the association
                return await session.ExecuteWriteAsync(async tx =>
                {
                    var query = @"
                        MATCH (a:Memorygram {id: $fromId})
                        MATCH (b:Memorygram {id: $toId})
                        MERGE (a)-[r:ASSOCIATED_WITH]->(b)
                        SET r.weight = $weight
                        RETURN a.id as id, a.content as content, a.vectorEmbedding as vectorEmbedding,
                               a.type as type, a.createdAt as createdAt, a.updatedAt as updatedAt";
                    
                    var parameters = new
                    {
                        fromId = fromId.ToString(),
                        toId = toId.ToString(),
                        weight
                    };
                    
                    var cursor = await tx.RunAsync(query, parameters);
                    if (await cursor.FetchAsync())
                    {
                        var record = cursor.Current;
                        return Result.Ok(new Memorygram(
                            Guid.Parse(record["id"].As<string>()),
                            record["content"].As<string>(),
                            Enum.Parse<MemorygramType>(record["type"].As<string>()),
                            ConvertToFloatArray(record["vectorEmbedding"]),
                            ConvertToDateTime(record["createdAt"]),
                            ConvertToDateTime(record["updatedAt"])
                        ));
                    }

                    return Result.Fail<Memorygram>("Failed to create association");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create association between memorygrams {FromId} and {ToId}", fromId, toId);
                return Result.Fail<Memorygram>($"Database error: {ex.Message}");
            }
        }

        public async Task<Result<Memorygram>> GetMemorygramByIdAsync(Guid id)
        {
            try
            {
                await using var session = _driver.AsyncSession();
                
                return await session.ExecuteReadAsync(async tx =>
                {
                    var query = @"
                        MATCH (m:Memorygram {id: $id})
                        RETURN m.id as id, m.content as content, m.vectorEmbedding as vectorEmbedding,
                               m.type as type, m.createdAt as createdAt, m.updatedAt as updatedAt";
                    
                    var parameters = new { id = id.ToString() };
                    var cursor = await tx.RunAsync(query, parameters);
                    
                    // Check if we have a record
                    if (await cursor.FetchAsync())
                    {
                        var record = cursor.Current;
                        return Result.Ok(new Memorygram(
                            Guid.Parse(record["id"].As<string>()),
                            record["content"].As<string>(),
                            Enum.Parse<MemorygramType>(record["type"].As<string>()),
                            ConvertToFloatArray(record["vectorEmbedding"]),
                            ConvertToDateTime(record["createdAt"]),
                            ConvertToDateTime(record["updatedAt"])
                        ));
                    }
                    
                    // No record found
                    _logger.LogWarning("Memorygram with ID {Id} not found", id);
                    return Result.Fail<Memorygram>($"Memorygram with ID {id} not found");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve memorygram by ID {Id}", id);
                return Result.Fail<Memorygram>($"Database error: {ex.Message}");
            }
        }

        public async Task<Result<IEnumerable<MemorygramWithScore>>> FindSimilarAsync(float[] queryVector, int topK)
        {
            if (queryVector == null || queryVector.Length == 0)
            {
                return Result.Fail<IEnumerable<MemorygramWithScore>>("Query vector cannot be null or empty");
            }

            if (topK <= 0)
            {
                return Result.Fail<IEnumerable<MemorygramWithScore>>("TopK must be greater than 0");
            }

            try
            {
                await using var session = _driver.AsyncSession();
                
                return await session.ExecuteReadAsync(async tx =>
                {
                    // Use the vector index to find similar memorygrams
                    var query = @"
                        CALL db.index.vector.queryNodes($indexName, $topK, $queryVector)
                        YIELD node, score
                        RETURN node.id AS id, node.content AS content, node.type AS type,
                               node.createdAt AS createdAt, node.updatedAt AS updatedAt,
                               score
                        ORDER BY score DESC";
                    
                    var parameters = new
                    {
                        indexName = "memorygram_content_embedding",
                        topK = topK,
                        queryVector = queryVector
                    };
                    
                    var cursor = await tx.RunAsync(query, parameters);
                    var results = new List<MemorygramWithScore>();
                    
                    while (await cursor.FetchAsync())
                    {
                        var record = cursor.Current;
                        var memorygram = new MemorygramWithScore(
                            Guid.Parse(record["id"].As<string>()),
                            record["content"].As<string>(),
                            Enum.Parse<MemorygramType>(record["type"].As<string>()),
                            ConvertToDateTime(record["createdAt"]),
                            ConvertToDateTime(record["updatedAt"]),
                            record["score"].As<float>()
                        );
                        results.Add(memorygram);
                    }
                    
                    _logger.LogInformation("Found {Count} similar memorygrams", results.Count);
                    return Result.Ok<IEnumerable<MemorygramWithScore>>(results);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find similar memorygrams");
                return Result.Fail<IEnumerable<MemorygramWithScore>>($"Database error: {ex.Message}");
            }
        }

        private float[] ConvertToFloatArray(object vectorObj)
        {
            if (vectorObj == null)
                return Array.Empty<float>();

            // If it's already a float array, return it
            if (vectorObj is float[] floatArray)
                return floatArray;

            // If it's a list, convert it
            if (vectorObj is System.Collections.IEnumerable enumerable)
            {
                var list = new List<float>();
                foreach (var item in enumerable)
                {
                    if (item is float f)
                    {
                        list.Add(f);
                    }
                    else if (item is double d)
                    {
                        list.Add((float)d);
                    }
                    else if (item is int i)
                    {
                        list.Add((float)i);
                    }
                    else if (item is long l)
                    {
                        list.Add((float)l);
                    }
                    else if (item != null)
                    {
                        // Try to convert using Convert.ToSingle
                        try
                        {
                            list.Add(Convert.ToSingle(item));
                        }
                        catch
                        {
                            // If conversion fails, use 0.0f
                            list.Add(0.0f);
                        }
                    }
                }
                return list.ToArray();
            }

            // If all else fails, return an empty array
            return Array.Empty<float>();
        }

        private DateTimeOffset ConvertToDateTime(object dateObj)
        {
            if (dateObj == null)
                return DateTimeOffset.UtcNow;

            // If it's already a DateTimeOffset, return it
            if (dateObj is DateTimeOffset dateTimeOffset)
                return dateTimeOffset;
                
            // If it's a DateTime, convert it to DateTimeOffset
            if (dateObj is DateTime dateTime)
                return new DateTimeOffset(dateTime, TimeSpan.Zero);

            // If it's a string, try to parse it
            if (dateObj is string dateString)
            {
                if (DateTimeOffset.TryParse(dateString, out var parsedDate))
                    return parsedDate;
            }

            // If it's a ZonedDateTime or other temporal type, try to get its value
            try
            {
                // Get the string representation and parse it
                string? temporalString = dateObj?.ToString();
                if (!string.IsNullOrEmpty(temporalString) && DateTimeOffset.TryParse(temporalString, out var parsedTemporal))
                    return parsedTemporal;
            }
            catch
            {
                // Ignore conversion errors
            }

            // If all else fails, return current UTC time
            return DateTimeOffset.UtcNow;
        }
    }
}
