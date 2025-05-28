using FluentResults;
using Neo4j.Driver;

namespace Mnemosyne.Core.Services;

public class Neo4jService
{
    private readonly IDriver _driver;
    private readonly ILogger<Neo4jService> _logger;
    private readonly IConfiguration _configuration;

    public Neo4jService(IDriver driver, ILogger<Neo4jService> logger, IConfiguration configuration)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public static IDriver ConfigureNeo4jDriver(IConfiguration configuration)
    {
        var neo4jSettings = configuration.GetSection("Neo4j");
        var uri = neo4jSettings["Uri"] ?? throw new ArgumentException("Neo4j URI not configured");
        var username = neo4jSettings["Username"] ?? throw new ArgumentException("Neo4j username not configured");
        var password = neo4jSettings["Password"] ?? throw new ArgumentException("Neo4j password not configured");

        return GraphDatabase.Driver(uri, AuthTokens.Basic(username, password));
    }

    public async Task<Result> InitializeSchemaAsync()
    {
        try
        {
            _logger.LogInformation("Initializing Neo4j schema...");

            await using var session = _driver.AsyncSession();

            // Create constraint on Memorygram.id
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(@"
                        CREATE CONSTRAINT memorygram_id_unique IF NOT EXISTS
                        FOR (m:Memorygram)
                        REQUIRE m.id IS UNIQUE
                    ");

                _logger.LogInformation("Created constraint on Memorygram.id");

                // Get vector dimensions from configuration
                var neo4jSettings = _configuration.GetSection("Neo4j");
                var vectorDimensions = neo4jSettings.GetValue<int>("VectorDimensions");

                if (vectorDimensions <= 0)
                {
                    _logger.LogWarning("Invalid vector dimensions: {VectorDimensions}. Using default of 1024.", vectorDimensions);
                    vectorDimensions = 1024;
                }

                // Create vector indexes for all embedding fields
                var parameters = new { dimensions = vectorDimensions };
                
                // Create vector index for Memorygram.topicalEmbedding
                await tx.RunAsync(@"
                        CREATE VECTOR INDEX memorygram_topical_embedding IF NOT EXISTS
                        FOR (m:Memorygram) ON (m.topicalEmbedding)
                        OPTIONS {indexConfig: {
                          `vector.dimensions`: $dimensions,
                          `vector.similarity_function`: 'cosine'
                        }}
                    ", parameters);
                _logger.LogInformation("Created vector index on Memorygram.topicalEmbedding with {Dimensions} dimensions", vectorDimensions);
                
                // Create vector index for Memorygram.contentEmbedding
                await tx.RunAsync(@"
                        CREATE VECTOR INDEX memorygram_content_embedding IF NOT EXISTS
                        FOR (m:Memorygram) ON (m.contentEmbedding)
                        OPTIONS {indexConfig: {
                          `vector.dimensions`: $dimensions,
                          `vector.similarity_function`: 'cosine'
                        }}
                    ", parameters);
                _logger.LogInformation("Created vector index on Memorygram.contentEmbedding with {Dimensions} dimensions", vectorDimensions);
                
                // Create vector index for Memorygram.contextEmbedding
                await tx.RunAsync(@"
                        CREATE VECTOR INDEX memorygram_context_embedding IF NOT EXISTS
                        FOR (m:Memorygram) ON (m.contextEmbedding)
                        OPTIONS {indexConfig: {
                          `vector.dimensions`: $dimensions,
                          `vector.similarity_function`: 'cosine'
                        }}
                    ", parameters);
                _logger.LogInformation("Created vector index on Memorygram.contextEmbedding with {Dimensions} dimensions", vectorDimensions);
                
                // Create vector index for Memorygram.metadataEmbedding
                await tx.RunAsync(@"
                        CREATE VECTOR INDEX memorygram_metadata_embedding IF NOT EXISTS
                        FOR (m:Memorygram) ON (m.metadataEmbedding)
                        OPTIONS {indexConfig: {
                          `vector.dimensions`: $dimensions,
                          `vector.similarity_function`: 'cosine'
                        }}
                    ", parameters);
                _logger.LogInformation("Created vector index on Memorygram.metadataEmbedding with {Dimensions} dimensions", vectorDimensions);

                return 1;
            });

            _logger.LogInformation("Neo4j schema initialization completed successfully");
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message initializing Neo4j schema");
            return Result.Fail(new Error("Failed to initialize Neo4j schema").CausedBy(ex));
        }
    }
}
