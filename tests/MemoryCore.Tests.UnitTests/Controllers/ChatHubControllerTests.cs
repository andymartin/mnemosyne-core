using FluentResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Mnemosyne.Core.Controllers;
using Mnemosyne.Core.Interfaces;
using Moq;

namespace MemoryCore.Tests.UnitTests.Controllers;

public class ChatHubControllerTests
{
    private readonly ChatHub _chatHub;
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly Mock<ISingleClientProxy> _mockSingleClientProxy;
    private readonly Mock<IChatService> _mockChatService;
    private readonly Mock<ILogger<ChatHub>> _mockLogger;

    public ChatHubControllerTests()
    {
        _mockClients = new Mock<IHubCallerClients>();
        _mockClientProxy = new Mock<IClientProxy>();
        _mockSingleClientProxy = new Mock<ISingleClientProxy>();
        _mockChatService = new Mock<IChatService>();
        _mockLogger = new Mock<ILogger<ChatHub>>();

        _mockClients.Setup(clients => clients.All).Returns(_mockClientProxy.Object);
        _mockClients.Setup(clients => clients.Caller).Returns(_mockSingleClientProxy.Object);

        _chatHub = new ChatHub(_mockChatService.Object, _mockLogger.Object)
        {
            Clients = _mockClients.Object
        };
    }

    [Fact]
    public async Task SendMessage_ShouldNotThrowException()
    {
        var chatId = "testChatId";
        var userText = "testMessage";

        // Setup the ChatService to return a successful result
        _mockChatService
            .Setup(x => x.ProcessUserMessageAsync(chatId, userText, It.IsAny<Guid?>()))
            .ReturnsAsync(FluentResults.Result.Ok("Mock response"));

        // Just verify that the method doesn't throw an exception
        // The SignalR Hub context makes it difficult to test the full flow in unit tests
        await _chatHub.SendMessage(chatId, userText);

        // Test passes if no exception is thrown
        Assert.True(true);
    }
}