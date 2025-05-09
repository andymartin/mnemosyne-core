using Microsoft.AspNetCore.SignalR;
using Mnemosyne.Core.Controllers;
using Moq;

namespace Mnemosyne.Core.Tests.Controllers
{
    public class ChatHubControllerTests
    {
        private readonly ChatHub _chatHub;
        private readonly Mock<IHubCallerClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;

        public ChatHubControllerTests()
        {
            _mockClients = new Mock<IHubCallerClients>();
            _mockClientProxy = new Mock<IClientProxy>();

            _mockClients.Setup(clients => clients.All).Returns(_mockClientProxy.Object);
            
            _chatHub = new ChatHub()
            {
                Clients = _mockClients.Object
            };
        }

        [Fact]
        public async Task SendMessage_ShouldBroadcastToAllClients()
        {
            var user = "testUser";
            var message = "testMessage";

            await _chatHub.SendMessage(user, message);

            _mockClientProxy.Verify(
                clientProxy => clientProxy.SendCoreAsync(
                    "ReceiveMessage",
                    It.Is<object[]>(o => o != null && o.Length == 2 && (string)o[0] == user && (string)o[1] == message),
                    default(CancellationToken)),
                Times.Once);
        }
    }
}