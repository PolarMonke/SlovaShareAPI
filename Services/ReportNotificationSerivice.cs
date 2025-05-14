using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

namespace Backend;
public class ReportNotificationService
{
    private readonly ITelegramBotClient _botClient;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ReportNotificationService> _logger;

    public ReportNotificationService(
        ITelegramBotClient botClient,
        AppDbContext dbContext,
        ILogger<ReportNotificationService> logger)
    {
        _botClient = botClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task NotifyAdminsAboutNewReport(int reportId)
    {
        try
        {
            var report = await _dbContext.Reports
                .Include(r => r.Story)
                .ThenInclude(s => s.Owner)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null)
            {
                _logger.LogWarning($"Report {reportId} not found");
                return;
            }

            var adminIds = Environment.GetEnvironmentVariable("TELEGRAM_ADMIN_IDS")?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(long.Parse)
                .ToList() ?? new List<long>();

            var message = $"🚨 New Report\n\n" +
                         $"Story: {report.Story.Title} (ID: {report.StoryId})\n" +
                         $"Author: {report.Story.Owner.Login} (ID: {report.Story.OwnerId})\n" +
                         $"Reporter: {report.User.Login} (ID: {report.UserId})\n" +
                         $"Reason: {report.Reason}\n" +
                         $"Details: {report.Content}\n\n" +
                         $"Actions:\n" +
                         $"/banstory {report.StoryId} [reason]\n" +
                         $"/banuser {report.Story.OwnerId} [reason]\n" +
                         $"/requestedit {report.StoryId} [message]";

            foreach (var adminId in adminIds)
            {
                await _botClient.SendMessage(
                    chatId: adminId,
                    text: message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying admins about new report");
        }
    }
}