using FluentResults;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Neo4j.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mnemosyne.Core.Persistence;

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
                            m.topicalEmbedding = $topicalEmbedding,
                            m.contentEmbedding = $contentEmbedding,
                            m.contextEmbedding = $contextEmbedding,
                            m.metadataEmbedding = $metadataEmbedding,
                            m.type = $type,
                            m.source = $source,
                            m.timestamp = $timestamp,
                            m.subtype = $subtype,
                            m.previousMemorygramId = $previousMemorygramId,
                            m.nextMemorygramId = $nextMemorygramId,
                            m.sequence = $sequence,
                            m.createdAt = datetime(),
                            m.updatedAt = datetime()
                        ON MATCH SET
                            m.content = $content,
                            m.topicalEmbedding = $topicalEmbedding,
                            m.contentEmbedding = $contentEmbedding,
                            m.contextEmbedding = $contextEmbedding,
                            m.metadataEmbedding = $metadataEmbedding,
                            m.type = $type,
                            m.source = $source,
                            m.timestamp = $timestamp,
                            m.subtype = $subtype,
                            m.previousMemorygramId = $previousMemorygramId,
                            m.nextMemorygramId = $nextMemorygramId,
                            m.sequence = $sequence,
                            m.updatedAt = datetime()
                        RETURN m.id, m.content, m.topicalEmbedding, m.contentEmbedding, m.contextEmbedding, m.metadataEmbedding,
                               m.type, m.source, m.timestamp, m.subtype, m.previousMemorygramId, m.nextMemorygramId,
                               m.sequence, m.createdAt, m.updatedAt";

                var parameters = new
                {
                    id = memorygram.Id.ToString(),
                    content = memorygram.Content,
                    topicalEmbedding = memorygram.TopicalEmbedding,
                    contentEmbedding = memorygram.ContentEmbedding,
                    contextEmbedding = memorygram.ContextEmbedding,
                    metadataEmbedding = memorygram.MetadataEmbedding,
                    type = memorygram.Type.ToString(),
                    source = memorygram.Source,
                    timestamp = memorygram.Timestamp,
                    subtype = memorygram.Subtype,
                    previousMemorygramId = memorygram.PreviousMemorygramId?.ToString(),
                    nextMemorygramId = memorygram.NextMemorygramId?.ToString(),
                    sequence = memorygram.Sequence
                };

                var cursor = await tx.RunAsync(query, parameters);
                if (await cursor.FetchAsync())
                {
                    var record = cursor.Current.Values;
                    return Result.Ok(new Memorygram(
                        Guid.Parse(record["m.id"].As<string>()),
                        record["m.content"].As<string>(),
                        Enum.Parse<MemorygramType>(record["m.type"].As<string>()),
                        ConvertToFloatArray(record["m.topicalEmbedding"]),
                        ConvertToFloatArray(record["m.contentEmbedding"]),
                        ConvertToFloatArray(record["m.contextEmbedding"]),
                        ConvertToFloatArray(record["m.metadataEmbedding"]),
                        record["m.source"].As<string>(),
                        record["m.timestamp"].As<long>(),
                        ConvertToDateTime(record["m.createdAt"]),
                        ConvertToDateTime(record["m.updatedAt"]),
                        record["m.subtype"].As<string>(),
                        record["m.previousMemorygramId"].As<string>() != null ? Guid.Parse(record["m.previousMemorygramId"].As<string>()) : null,
                        record["m.nextMemorygramId"].As<string>() != null ? Guid.Parse(record["m.nextMemorygramId"].As<string>()) : null,
                        record["m.sequence"].As<int?>()
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

            return await session.ExecuteWriteAsync(async tx =>
            {
                var query = @"
                        MATCH (a:Memorygram {id: $fromId})
                        MATCH (b:Memorygram {id: $toId})
                        MERGE (a)-[r:ASSOCIATED_WITH]->(b)
                        SET r.weight = $weight
                        RETURN a.id as id, a.content as content, a.topicalEmbedding as topicalEmbedding,
                               a.contentEmbedding as contentEmbedding, a.contextEmbedding as contextEmbedding,
                               a.metadataEmbedding as metadataEmbedding, a.type as type, a.source as source, 
                               a.timestamp as timestamp, a.createdAt as createdAt, a.updatedAt as updatedAt,
                               a.subtype as subtype, a.previousMemorygramId as previousMemorygramId,
                               a.nextMemorygramId as nextMemorygramId, a.sequence as sequence";

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
                        ConvertToFloatArray(record["topicalEmbedding"]),
                        ConvertToFloatArray(record["contentEmbedding"]),
                        ConvertToFloatArray(record["contextEmbedding"]),
                        ConvertToFloatArray(record["metadataEmbedding"]),
                        record["source"].As<string>(),
                        record["timestamp"].As<long>(),
                        ConvertToDateTime(record["createdAt"]),
                        ConvertToDateTime(record["updatedAt"]),
                        record["subtype"].As<string>(),
                        record["previousMemorygramId"].As<string>() != null ? Guid.Parse(record["previousMemorygramId"].As<string>()) : null,
                        record["nextMemorygramId"].As<string>() != null ? Guid.Parse(record["nextMemorygramId"].As<string>()) : null,
                        record["sequence"].As<int?>()
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
                        RETURN m.id as id, m.content as content, m.topicalEmbedding as topicalEmbedding,
                               m.contentEmbedding as contentEmbedding, m.contextEmbedding as contextEmbedding,
                               m.metadataEmbedding as metadataEmbedding, m.type as type, m.source as source, 
                               m.timestamp as timestamp, m.createdAt as createdAt, m.updatedAt as updatedAt,
                               m.subtype as subtype, m.previousMemorygramId as previousMemorygramId,
                               m.nextMemorygramId as nextMemorygramId, m.sequence as sequence";

                var parameters = new { id = id.ToString() };
                var cursor = await tx.RunAsync(query, parameters);

                if (await cursor.FetchAsync())
                {
                    var record = cursor.Current;
                    return Result.Ok(new Memorygram(
                        Guid.Parse(record["id"].As<string>()),
                        record["content"].As<string>(),
                        Enum.Parse<MemorygramType>(record["type"].As<string>()),
                        ConvertToFloatArray(record["topicalEmbedding"]),
                        ConvertToFloatArray(record["contentEmbedding"]),
                        ConvertToFloatArray(record["contextEmbedding"]),
                        ConvertToFloatArray(record["metadataEmbedding"]),
                        record["source"].As<string>(),
                        record["timestamp"].As<long>(),
                        ConvertToDateTime(record["createdAt"]),
                        ConvertToDateTime(record["updatedAt"]),
                        record["subtype"].As<string>(),
                        record["previousMemorygramId"].As<string>() != null ? Guid.Parse(record["previousMemorygramId"].As<string>()) : null,
                        record["nextMemorygramId"].As<string>() != null ? Guid.Parse(record["nextMemorygramId"].As<string>()) : null,
                        record["sequence"].As<int?>()
                    ));
                }

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

    public async Task<Result<IEnumerable<MemorygramWithScore>>> FindSimilarAsync(
        float[] queryVector,
        MemoryReformulationType reformulationType,
        int topK,
        string? excludeSubtype = null)
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
                // Get the correct index name based on reformulation type
                string reformulationTypeStr = reformulationType.ToString().ToLowerInvariant();
                string indexName = $"memorygram_{reformulationTypeStr}_embedding";

                var query = $@"
                        CALL db.index.vector.queryNodes($indexName, $topK, $queryVector)
                        YIELD node, score
                        WITH node, score
                        WHERE ($excludeChatIdString IS NULL OR node.ChatId <> $excludeChatIdString)
                        RETURN node.id AS id, node.content AS content, node.type AS type,
                               node.topicalEmbedding AS topicalEmbedding, node.contentEmbedding AS contentEmbedding,
                               node.contextEmbedding AS contextEmbedding, node.metadataEmbedding AS metadataEmbedding,
                               node.source AS source, node.timestamp AS timestamp,
                               node.createdAt AS createdAt, node.updatedAt AS updatedAt,
                               node.subtype AS subtype, node.previousMemorygramId AS previousMemorygramId,
                               node.nextMemorygramId AS nextMemorygramId, node.sequence AS sequence,
                               score
                        ORDER BY score DESC";

                var parameters = new
                {
                    indexName = indexName,
                    topK = topK,
                    queryVector = queryVector,
                    excludeSubtypeString = excludeSubtype
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
                        ConvertToFloatArray(record["topicalEmbedding"]),
                        ConvertToFloatArray(record["contentEmbedding"]),
                        ConvertToFloatArray(record["contextEmbedding"]),
                        ConvertToFloatArray(record["metadataEmbedding"]),
                        record["source"].As<string>(),
                        record["timestamp"].As<long>(),
                        ConvertToDateTime(record["createdAt"]),
                        ConvertToDateTime(record["updatedAt"]),
                        record["subtype"].As<string>(),
                        record["previousMemorygramId"].As<string>() != null ? Guid.Parse(record["previousMemorygramId"].As<string>()) : null,
                        record["nextMemorygramId"].As<string>() != null ? Guid.Parse(record["nextMemorygramId"].As<string>()) : null,
                        record["sequence"].As<int?>(),
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

    public async Task<Result<IEnumerable<Memorygram>>> GetBySubtypeAsync(string subtype)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(subtype))
            {
                return Result.Fail<IEnumerable<Memorygram>>("Subtype cannot be null or empty");
            }

            await using var session = _driver.AsyncSession();

            return await session.ExecuteReadAsync(async tx =>
            {
                var query = @"
                        MATCH (m:Memorygram {subtype: $subtype})
                        RETURN m.id as id, m.content as content, m.topicalEmbedding as topicalEmbedding,
                               m.contentEmbedding as contentEmbedding, m.contextEmbedding as contextEmbedding,
                               m.metadataEmbedding as metadataEmbedding, m.type as type, m.source as source, 
                               m.timestamp as timestamp, m.createdAt as createdAt, m.updatedAt as updatedAt,
                               m.subtype as subtype, m.previousMemorygramId as previousMemorygramId,
                               m.nextMemorygramId as nextMemorygramId, m.sequence as sequence
                        ORDER BY m.timestamp ASC";

                var parameters = new { subtype = subtype };
                var cursor = await tx.RunAsync(query, parameters);
                var results = new List<Memorygram>();

                while (await cursor.FetchAsync())
                {
                    var record = cursor.Current;
                    var memorygram = new Memorygram(
                        Guid.Parse(record["id"].As<string>()),
                        record["content"].As<string>(),
                        Enum.Parse<MemorygramType>(record["type"].As<string>()),
                        ConvertToFloatArray(record["topicalEmbedding"]),
                        ConvertToFloatArray(record["contentEmbedding"]),
                        ConvertToFloatArray(record["contextEmbedding"]),
                        ConvertToFloatArray(record["metadataEmbedding"]),
                        record["source"].As<string>(),
                        record["timestamp"].As<long>(),
                        ConvertToDateTime(record["createdAt"]),
                        ConvertToDateTime(record["updatedAt"]),
                        record["subtype"].As<string>(),
                        record["previousMemorygramId"].As<string>() != null ? Guid.Parse(record["previousMemorygramId"].As<string>()) : null,
                        record["nextMemorygramId"].As<string>() != null ? Guid.Parse(record["nextMemorygramId"].As<string>()) : null,
                        record["sequence"].As<int?>()
                    );
                    results.Add(memorygram);
                }

                _logger.LogInformation("Retrieved {Count} memorygrams for subtype {Subtype}", results.Count, subtype);
                return Result.Ok<IEnumerable<Memorygram>>(results);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve memorygrams for subtype {Subtype}", subtype);
            return Result.Fail<IEnumerable<Memorygram>>($"Database error: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<Memorygram>>> GetAllChatsAsync()
    {
        try
        {
            await using var session = _driver.AsyncSession();

            return await session.ExecuteReadAsync(async tx =>
            {
                var query = @"
                        MATCH (m:Memorygram)
                        WHERE m.previousMemorygramId IS NULL AND m.subtype IS NOT NULL
                        RETURN m.id as id, m.content as content, m.topicalEmbedding as topicalEmbedding,
                               m.contentEmbedding as contentEmbedding, m.contextEmbedding as contextEmbedding,
                               m.metadataEmbedding as metadataEmbedding, m.type as type, m.source as source,
                               m.timestamp as timestamp, m.createdAt as createdAt, m.updatedAt as updatedAt,
                               m.subtype as subtype, m.previousMemorygramId as previousMemorygramId,
                               m.nextMemorygramId as nextMemorygramId, m.sequence as sequence
                        ORDER BY m.timestamp DESC";

                var cursor = await tx.RunAsync(query);
                var results = new List<Memorygram>();

                while (await cursor.FetchAsync())
                {
                    var record = cursor.Current;
                    var memorygram = new Memorygram(
                        Guid.Parse(record["id"].As<string>()),
                        record["content"].As<string>(),
                        Enum.Parse<MemorygramType>(record["type"].As<string>()),
                        ConvertToFloatArray(record["topicalEmbedding"]),
                        ConvertToFloatArray(record["contentEmbedding"]),
                        ConvertToFloatArray(record["contextEmbedding"]),
                        ConvertToFloatArray(record["metadataEmbedding"]),
                        record["source"].As<string>(),
                        record["timestamp"].As<long>(),
                        ConvertToDateTime(record["createdAt"]),
                        ConvertToDateTime(record["updatedAt"]),
                        record["subtype"].As<string>(),
                        record["previousMemorygramId"].As<string>() != null ? Guid.Parse(record["previousMemorygramId"].As<string>()) : null,
                        record["nextMemorygramId"].As<string>() != null ? Guid.Parse(record["nextMemorygramId"].As<string>()) : null,
                        record["sequence"].As<int?>()
                    );
                    results.Add(memorygram);
                }

                _logger.LogInformation("Retrieved {Count} chat initiation memorygrams", results.Count);
                return Result.Ok<IEnumerable<Memorygram>>(results);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve chat initiation memorygrams");
            return Result.Fail<IEnumerable<Memorygram>>($"Database error: {ex.Message}");
        }
    }

    public async Task<Result<GraphRelationship>> CreateRelationshipAsync(Guid fromId, Guid toId, string relationshipType, float weight, string? properties = null)
    {
        try
        {
            await using var session = _driver.AsyncSession();

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
                return Result.Fail<GraphRelationship>($"Memorygram with ID {fromId} not found");
            }

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
                return Result.Fail<GraphRelationship>($"Memorygram with ID {toId} not found");
            }

            return await session.ExecuteWriteAsync(async tx =>
            {
                var relationshipId = Guid.NewGuid();
                var query = $@"
                        MATCH (a:Memorygram {{id: $fromId}})
                        MATCH (b:Memorygram {{id: $toId}})
                        CREATE (a)-[r:{relationshipType} {{
                            id: $relationshipId,
                            weight: $weight,
                            properties: $properties,
                            isActive: $isActive,
                            createdAt: datetime(),
                            updatedAt: datetime()
                        }}]->(b)
                        RETURN r.id as id, r.weight as weight, r.properties as properties,
                               r.isActive as isActive, r.createdAt as createdAt, r.updatedAt as updatedAt";

                var parameters = new
                {
                    fromId = fromId.ToString(),
                    toId = toId.ToString(),
                    relationshipId = relationshipId.ToString(),
                    weight,
                    properties,
                    isActive = true
                };

                var cursor = await tx.RunAsync(query, parameters);
                if (await cursor.FetchAsync())
                {
                    var record = cursor.Current;
                    return Result.Ok(new GraphRelationship(
                        Guid.Parse(record["id"].As<string>()),
                        fromId,
                        toId,
                        relationshipType,
                        record["weight"].As<float>(),
                        ConvertToDateTime(record["createdAt"]),
                        ConvertToDateTime(record["updatedAt"]),
                        record["properties"].As<string>(),
                        record["isActive"].As<bool>()
                    ));
                }

                return Result.Fail<GraphRelationship>("Failed to create relationship");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create relationship between memorygrams {FromId} and {ToId}", fromId, toId);
            return Result.Fail<GraphRelationship>($"Database error: {ex.Message}");
        }
    }

    public async Task<Result<GraphRelationship>> UpdateRelationshipAsync(Guid relationshipId, float? weight = null, string? properties = null, bool? isActive = null)
    {
        try
        {
            await using var session = _driver.AsyncSession();

            return await session.ExecuteWriteAsync(async tx =>
            {
                var setClauses = new List<string>();
                var parameters = new Dictionary<string, object>
                {
                    { "relationshipId", relationshipId.ToString() }
                };

                if (weight.HasValue)
                {
                    setClauses.Add("r.weight = $weight");
                    parameters["weight"] = weight.Value;
                }

                if (properties != null)
                {
                    setClauses.Add("r.properties = $properties");
                    parameters["properties"] = properties;
                }

                if (isActive.HasValue)
                {
                    setClauses.Add("r.isActive = $isActive");
                    parameters["isActive"] = isActive.Value;
                }

                if (setClauses.Count == 0)
                {
                    return Result.Fail<GraphRelationship>("No fields to update");
                }

                setClauses.Add("r.updatedAt = datetime()");

                var query = $@"
                        MATCH ()-[r {{id: $relationshipId}}]->()
                        SET {string.Join(", ", setClauses)}
                        RETURN r.id as id, startNode(r).id as fromId, endNode(r).id as toId,
                               type(r) as relationshipType, r.weight as weight, r.properties as properties,
                               r.isActive as isActive, r.createdAt as createdAt, r.updatedAt as updatedAt";

                var cursor = await tx.RunAsync(query, parameters);
                if (await cursor.FetchAsync())
                {
                    var record = cursor.Current;
                    return Result.Ok(new GraphRelationship(
                        Guid.Parse(record["id"].As<string>()),
                        Guid.Parse(record["fromId"].As<string>()),
                        Guid.Parse(record["toId"].As<string>()),
                        record["relationshipType"].As<string>(),
                        record["weight"].As<float>(),
                        ConvertToDateTime(record["createdAt"]),
                        ConvertToDateTime(record["updatedAt"]),
                        record["properties"].As<string>(),
                        record["isActive"].As<bool>()
                    ));
                }

                _logger.LogWarning("Relationship with ID {Id} not found", relationshipId);
                return Result.Fail<GraphRelationship>($"Relationship with ID {relationshipId} not found");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update relationship {RelationshipId}", relationshipId);
            return Result.Fail<GraphRelationship>($"Database error: {ex.Message}");
        }
    }

    public async Task<Result> DeleteRelationshipAsync(Guid relationshipId)
    {
        try
        {
            await using var session = _driver.AsyncSession();

            return await session.ExecuteWriteAsync(async tx =>
            {
                var query = @"
                        MATCH ()-[r {id: $relationshipId}]->()
                        DELETE r
                        RETURN count(r) as deletedCount";

                var parameters = new { relationshipId = relationshipId.ToString() };
                var cursor = await tx.RunAsync(query, parameters);

                if (await cursor.FetchAsync())
                {
                    var deletedCount = cursor.Current["deletedCount"].As<int>();
                    if (deletedCount > 0)
                    {
                        _logger.LogInformation("Deleted relationship {RelationshipId}", relationshipId);
                        return Result.Ok();
                    }
                }

                _logger.LogWarning("Relationship with ID {Id} not found for deletion", relationshipId);
                return Result.Fail($"Relationship with ID {relationshipId} not found");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete relationship {RelationshipId}", relationshipId);
            return Result.Fail($"Database error: {ex.Message}");
        }
    }

    public async Task<Result<GraphRelationship>> GetRelationshipByIdAsync(Guid relationshipId)
    {
        try
        {
            await using var session = _driver.AsyncSession();

            return await session.ExecuteReadAsync(async tx =>
            {
                var query = @"
                        MATCH (a)-[r {id: $relationshipId}]->(b)
                        RETURN r.id as id, a.id as fromId, b.id as toId, type(r) as relationshipType,
                               r.weight as weight, r.properties as properties, r.isActive as isActive,
                               r.createdAt as createdAt, r.updatedAt as updatedAt";

                var parameters = new { relationshipId = relationshipId.ToString() };
                var cursor = await tx.RunAsync(query, parameters);

                if (await cursor.FetchAsync())
                {
                    var record = cursor.Current;
                    return Result.Ok(new GraphRelationship(
                        Guid.Parse(record["id"].As<string>()),
                        Guid.Parse(record["fromId"].As<string>()),
                        Guid.Parse(record["toId"].As<string>()),
                        record["relationshipType"].As<string>(),
                        record["weight"].As<float>(),
                        ConvertToDateTime(record["createdAt"]),
                        ConvertToDateTime(record["updatedAt"]),
                        record["properties"].As<string>(),
                        record["isActive"].As<bool>()
                    ));
                }

                _logger.LogWarning("Relationship with ID {Id} not found", relationshipId);
                return Result.Fail<GraphRelationship>($"Relationship with ID {relationshipId} not found");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve relationship by ID {Id}", relationshipId);
            return Result.Fail<GraphRelationship>($"Database error: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<GraphRelationship>>> GetRelationshipsByMemorygramIdAsync(Guid memorygramId, bool includeIncoming = true, bool includeOutgoing = true)
    {
        try
        {
            await using var session = _driver.AsyncSession();

            return await session.ExecuteReadAsync(async tx =>
            {
                var conditions = new List<string>();
                if (includeOutgoing)
                    conditions.Add("(m)-[r]->(other)");
                if (includeIncoming)
                    conditions.Add("(other)-[r]->(m)");

                if (conditions.Count == 0)
                {
                    return Result.Ok<IEnumerable<GraphRelationship>>(Enumerable.Empty<GraphRelationship>());
                }

                var query = $@"
                        MATCH (m:Memorygram {{id: $memorygramId}})
                        MATCH {string.Join(" OR ", conditions)}
                        RETURN r.id as id, startNode(r).id as fromId, endNode(r).id as toId,
                               type(r) as relationshipType, r.weight as weight, r.properties as properties,
                               r.isActive as isActive, r.createdAt as createdAt, r.updatedAt as updatedAt
                        ORDER BY r.createdAt DESC";

                var parameters = new { memorygramId = memorygramId.ToString() };
                var cursor = await tx.RunAsync(query, parameters);
                var results = new List<GraphRelationship>();

                while (await cursor.FetchAsync())
                {
                    var record = cursor.Current;
                    var relationship = new GraphRelationship(
                        Guid.Parse(record["id"].As<string>()),
                        Guid.Parse(record["fromId"].As<string>()),
                        Guid.Parse(record["toId"].As<string>()),
                        record["relationshipType"].As<string>(),
                        record["weight"].As<float>(),
                        ConvertToDateTime(record["createdAt"]),
                        ConvertToDateTime(record["updatedAt"]),
                        record["properties"].As<string>(),
                        record["isActive"].As<bool>()
                    );
                    results.Add(relationship);
                }

                _logger.LogInformation("Found {Count} relationships for memorygram {MemorygramId}", results.Count, memorygramId);
                return Result.Ok<IEnumerable<GraphRelationship>>(results);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve relationships for memorygram {MemorygramId}", memorygramId);
            return Result.Fail<IEnumerable<GraphRelationship>>($"Database error: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<GraphRelationship>>> GetRelationshipsByTypeAsync(string relationshipType)
    {
        try
        {
            await using var session = _driver.AsyncSession();

            return await session.ExecuteReadAsync(async tx =>
            {
                var query = $@"
                        MATCH ()-[r:{relationshipType}]->()
                        RETURN r.id as id, startNode(r).id as fromId, endNode(r).id as toId,
                               type(r) as relationshipType, r.weight as weight, r.properties as properties,
                               r.isActive as isActive, r.createdAt as createdAt, r.updatedAt as updatedAt
                        ORDER BY r.createdAt DESC";

                var cursor = await tx.RunAsync(query);
                var results = new List<GraphRelationship>();

                while (await cursor.FetchAsync())
                {
                    var record = cursor.Current;
                    var relationship = new GraphRelationship(
                        Guid.Parse(record["id"].As<string>()),
                        Guid.Parse(record["fromId"].As<string>()),
                        Guid.Parse(record["toId"].As<string>()),
                        record["relationshipType"].As<string>(),
                        record["weight"].As<float>(),
                        ConvertToDateTime(record["createdAt"]),
                        ConvertToDateTime(record["updatedAt"]),
                        record["properties"].As<string>(),
                        record["isActive"].As<bool>()
                    );
                    results.Add(relationship);
                }

                _logger.LogInformation("Found {Count} relationships of type {RelationshipType}", results.Count, relationshipType);
                return Result.Ok<IEnumerable<GraphRelationship>>(results);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve relationships of type {RelationshipType}", relationshipType);
            return Result.Fail<IEnumerable<GraphRelationship>>($"Database error: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<GraphRelationship>>> FindRelationshipsAsync(Guid? fromId = null, Guid? toId = null, string? relationshipType = null, float? minWeight = null, float? maxWeight = null, bool? isActive = null)
    {
        try
        {
            await using var session = _driver.AsyncSession();

            return await session.ExecuteReadAsync(async tx =>
            {
                var conditions = new List<string>();
                var parameters = new Dictionary<string, object>();

                var matchClause = relationshipType != null ? $"()-[r:{relationshipType}]->()" : "()-[r]->()";

                if (fromId.HasValue)
                {
                    conditions.Add("startNode(r).id = $fromId");
                    parameters["fromId"] = fromId.Value.ToString();
                }

                if (toId.HasValue)
                {
                    conditions.Add("endNode(r).id = $toId");
                    parameters["toId"] = toId.Value.ToString();
                }

                if (minWeight.HasValue)
                {
                    conditions.Add("r.weight >= $minWeight");
                    parameters["minWeight"] = minWeight.Value;
                }

                if (maxWeight.HasValue)
                {
                    conditions.Add("r.weight <= $maxWeight");
                    parameters["maxWeight"] = maxWeight.Value;
                }

                if (isActive.HasValue)
                {
                    conditions.Add("r.isActive = $isActive");
                    parameters["isActive"] = isActive.Value;
                }

                var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";

                var query = $@"
                        MATCH {matchClause}
                        {whereClause}
                        RETURN r.id as id, startNode(r).id as fromId, endNode(r).id as toId,
                               type(r) as relationshipType, r.weight as weight, r.properties as properties,
                               r.isActive as isActive, r.createdAt as createdAt, r.updatedAt as updatedAt
                        ORDER BY r.createdAt DESC";

                var cursor = await tx.RunAsync(query, parameters);
                var results = new List<GraphRelationship>();

                while (await cursor.FetchAsync())
                {
                    var record = cursor.Current;
                    var relationship = new GraphRelationship(
                        Guid.Parse(record["id"].As<string>()),
                        Guid.Parse(record["fromId"].As<string>()),
                        Guid.Parse(record["toId"].As<string>()),
                        record["relationshipType"].As<string>(),
                        record["weight"].As<float>(),
                        ConvertToDateTime(record["createdAt"]),
                        ConvertToDateTime(record["updatedAt"]),
                        record["properties"].As<string>(),
                        record["isActive"].As<bool>()
                    );
                    results.Add(relationship);
                }

                _logger.LogInformation("Found {Count} relationships matching criteria", results.Count);
                return Result.Ok<IEnumerable<GraphRelationship>>(results);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find relationships with specified criteria");
            return Result.Fail<IEnumerable<GraphRelationship>>($"Database error: {ex.Message}");
        }
    }

    private float[] ConvertToFloatArray(object vectorObj)
    {
        if (vectorObj == null)
            return Array.Empty<float>();

        if (vectorObj is float[] floatArray)
            return floatArray;

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
                    try
                    {
                        list.Add(Convert.ToSingle(item));
                    }
                    catch
                    {
                        list.Add(0.0f);
                    }
                }
            }
            return list.ToArray();
        }

        return Array.Empty<float>();
    }

    private DateTimeOffset ConvertToDateTime(object dateObj)
    {
        if (dateObj == null)
            return DateTimeOffset.UtcNow;

        if (dateObj is DateTimeOffset dateTimeOffset)
            return dateTimeOffset;

        if (dateObj is DateTime dateTime)
            return new DateTimeOffset(dateTime, TimeSpan.Zero);

        if (dateObj is string dateString)
        {
            if (DateTimeOffset.TryParse(dateString, out var parsedDate))
                return parsedDate;
        }

        try
        {
            string? temporalString = dateObj?.ToString();
            if (!string.IsNullOrEmpty(temporalString) && DateTimeOffset.TryParse(temporalString, out var parsedTemporal))
                return parsedTemporal;
        }
        catch
        {
        }

        return DateTimeOffset.UtcNow;
    }
}
