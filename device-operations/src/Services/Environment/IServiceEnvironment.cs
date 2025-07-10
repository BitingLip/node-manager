namespace DeviceOperations.Services.Environment;

public interface IServiceEnvironment
{
    Task<object> GetEnvironmentStatusAsync();
}

public class ServiceEnvironment : IServiceEnvironment
{
    private readonly ILogger<ServiceEnvironment> _logger;

    public ServiceEnvironment(ILogger<ServiceEnvironment> logger)
    {
        _logger = logger;
    }

    public async Task<object> GetEnvironmentStatusAsync()
    {
        await Task.Delay(1);
        return new { Status = "Not Implemented" };
    }
}
