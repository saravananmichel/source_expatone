using ExpatOne.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExpatOne.Api.Services;

public class ReminderProcessorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderProcessorService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(15);

    public ReminderProcessorService(IServiceScopeFactory scopeFactory, ILogger<ReminderProcessorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reminder processor started, checking every {Interval} minutes", _interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reminderService = scope.ServiceProvider.GetRequiredService<IReminderService>();
                await reminderService.ProcessDueRemindersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing due reminders");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
