using Microsoft.AspNetCore.Mvc.Testing;
using MemoryCore.Controllers;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace MemoryCore.Tests.Integration
{
    public class ChatHubTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public ChatHubTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
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
            hubConnection.On<string, string>("ReceiveMessage", (user, message) => 
            {
                messageReceived = true;
            });

            await hubConnection.StartAsync();
            await hubConnection.InvokeAsync("SendMessage", "testUser", "testMessage");
            
            await Task.Delay(100); // Small delay for message processing
            Assert.True(messageReceived);
            await hubConnection.DisposeAsync();
        }
    }
}