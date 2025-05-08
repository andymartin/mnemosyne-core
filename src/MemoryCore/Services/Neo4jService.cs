using Neo4j.Driver;
using Microsoft.Extensions.Configuration;

namespace MemoryCore.Services
{
    public static class Neo4jService
    {
        public static IDriver ConfigureNeo4jDriver(IConfiguration configuration)
        {
            var neo4jSettings = configuration.GetSection("Neo4j");
            var uri = neo4jSettings["Uri"] ?? throw new ArgumentException("Neo4j URI not configured");
            var username = neo4jSettings["Username"] ?? throw new ArgumentException("Neo4j username not configured");
            var password = neo4jSettings["Password"] ?? throw new ArgumentException("Neo4j password not configured");
            
            return GraphDatabase.Driver(uri, AuthTokens.Basic(username, password));
        }
    }
}