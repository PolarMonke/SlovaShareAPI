using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;

namespace Backend;
public class TelegramBotWorker : IHostedService
{
    private readonly IServiceProvider _services;
    private TelegramBotService _botService;

    public TelegramBotWorker(IServiceProvider services)
    {
        _services = services;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        _botService = scope.ServiceProvider.GetRequiredService<TelegramBotService>();
        await _botService.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_botService != null)
        {
            await _botService.StopAsync(cancellationToken);
        }
    }
}