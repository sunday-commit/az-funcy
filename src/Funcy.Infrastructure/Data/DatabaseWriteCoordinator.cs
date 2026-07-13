namespace Funcy.Infrastructure.Data;

public sealed class DatabaseWriteCoordinator
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await operation();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            return await operation();
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
