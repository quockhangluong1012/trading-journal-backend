using Microsoft.AspNetCore.SignalR;
using TradingJournal.Modules.Notifications.Hubs;

namespace TradingJournal.Tests.Notifications.Helpers;

/// <summary>
/// Builds a Moq-backed <see cref="IHubContext{NotificationHub}"/> whose
/// <c>Clients.Group(...)</c> returns a mock <see cref="IClientProxy"/> with
/// <c>SendCoreAsync</c> stubbed. <c>SendAsync</c> is an extension over <c>SendCoreAsync</c>.
/// </summary>
public static class SignalRMockHelper
{
    public static Mock<IHubContext<NotificationHub>> CreateHubContext(out Mock<IClientProxy> proxy)
    {
        proxy = new Mock<IClientProxy>();
        proxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);

        var hub = new Mock<IHubContext<NotificationHub>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);

        return hub;
    }
}
