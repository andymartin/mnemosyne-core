using FluentResults;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mnemosyne.Core.Interfaces;
using Mnemosyne.Core.Models;
using Neo4j.Driver;
using NSubstitute;

namespace MemoryCore.Tests.IntegrationTests.Fixtures;

public class ChatHubFixture : WebApplicationFactory<Mnemosyne.Core.Program>
{
    private readonly Neo4jContainerFixture _neo4jFixture;

    public ChatHubFixture(Neo4jContainerFixture neo4jFixture)
    {
        _neo4jFixture = neo4jFixture;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDriver>();
            services.AddSingleton<IDriver>(_ =>
                GraphDatabase.Driver(_neo4jFixture.GetBoltUri(), AuthTokens.Basic("neo4j", "password")));
            
            // Mock IChatService for tests
            var mockChatService = Substitute.For<IChatService>();
            mockChatService.ProcessUserMessageAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid?>())
                .Returns(Task.FromResult(Result.Ok(new ResponseResult
                {
                    Response = "This is a test response",
                    SystemPrompt = "This is a test system prompt"
                })));
            
            services.RemoveAll<IChatService>();
            services.AddSingleton(mockChatService);
        });
    }
}