namespace Faktura.Api.Features.Invoicing;

/// <summary>
/// Kör <see cref="ReminderJob"/> vid uppstart och därefter dagligen. Registreras inte i
/// Testing-miljön (tester kör jobblogiken direkt).
/// </summary>
public sealed class ReminderBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ReminderBackgroundService> _logger;

    public ReminderBackgroundService(IServiceScopeFactory scopes, ILogger<ReminderBackgroundService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var job = scope.ServiceProvider.GetRequiredService<ReminderJob>();
                await job.RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reminder background run failed; retrying at next interval");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
