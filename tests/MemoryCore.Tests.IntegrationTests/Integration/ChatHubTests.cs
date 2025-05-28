using MemoryCore.Tests.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.SignalR.Client;

namespace MemoryCore.Tests.IntegrationTests.Integration;

[Trait("Category", "Integration")]
[Collection("TestContainerCollection")]
public class ChatHubTests : IClassFixture<ChatHubFixture>
{
    private readonly ChatHubFixture _factory;
    private readonly Neo4jContainerFixture _neo4jFixture;

    public ChatHubTests(ChatHubFixture factory, Neo4jContainerFixture neo4jFixture)
    {
        _factory = factory;
        _neo4jFixture = neo4jFixture;
    }

    [Fact]
    public async Task CanConnectToChatHub()
    {
        var client = _factory.CreateClient();
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/ws/chat"),
                o => o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();

        await hubConnection.StartAsync();
        Assert.Equal(HubConnectionState.Connected, hubConnection.State);
        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task CanSendAndReceiveMessages()
    {
        var client = _factory.CreateClient();
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "/ws/chat"),
                o => o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();

        var messageReceived = false;
        hubConnection.On<object>("ReceiveMessage", (messageDto) =>
        {
            messageReceived = true;
        });

        await hubConnection.StartAsync();
        await hubConnection.InvokeAsync("SendMessage", Guid.NewGuid(), "Hello, this is a test message");

        await Task.Delay(5000); // Give more time for processing
        Assert.True(messageReceived);
        await hubConnection.DisposeAsync();
    }
}
