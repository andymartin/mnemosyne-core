using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Mnemosyne.Core.Tests.Integration;

public class EmbeddingServiceContainer : IAsyncLifetime
{
    private readonly IContainer _container;
    private int _mappedPort;

    public EmbeddingServiceContainer()
    {
        _container = new ContainerBuilder()
            .WithImage("mnemosyne-embed:latest")
            .WithName($"embedding-service-test-{Guid.NewGuid().ToString("N").Substring(0, 8)}")
            .WithPortBinding(8000, true)
            .WithExposedPort(8000)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(request => request.ForPort(8000).ForPath("/health").ForStatusCode(System.Net.HttpStatusCode.OK))
            )
            .WithCreateParameterModifier(parameters =>
            {
                // Request 1 GPU via the nvidia driver
                parameters.HostConfig.DeviceRequests = new List<DeviceRequest>
                {
                    new DeviceRequest
                    {
                        Driver = "nvidia",
                        Count = 1,
                        Capabilities = new List<IList<string>>
                        {
                            // "gpu" capability is required to enable CUDA support
                            new List<string> { "gpu" }
                        }
                    }
                };
            })
            .WithEnvironment("NVIDIA_VISIBLE_DEVICES", "all")
            .WithEnvironment("NVIDIA_DRIVER_CAPABILITIES", "compute,utility")
            .Build();
    }

    public string GetConnectionString()
    {
        if (_mappedPort == 0)
        {
            throw new InvalidOperationException("Container has not been started or port not mapped. Call InitializeAsync first.");
        }
        return $"http://localhost:{_mappedPort}";
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _mappedPort = _container.GetMappedPublicPort(8000);
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }
}
