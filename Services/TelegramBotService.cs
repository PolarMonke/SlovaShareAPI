using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Polling;
using System.Net;

namespace Backend;

public class TelegramBotService : IHostedService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly string _adminPassword;
    private readonly List<long> _adminChatIds;
    private readonly EmailService _emailService;

    private readonly Dictionary<long, bool> _authenticatedAdmins = new();
    private readonly Dictionary<long, (string Action, int TargetId)> _pendingActions = new();

    public TelegramBotService(
        ITelegramBotClient botClient,
        IServiceProvider serviceProvider,
        ILogger<TelegramBotService> logger,
        IConfiguration configuration,
        EmailService emailService)
    {
        _botClient = botClient;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _emailService = emailService;

        var botSettings = configuration.GetSection("Telegram");
        _adminPassword = botSettings["AdminPassword"] ?? "defaultPassword";
        var adminIds = botSettings["AdminIds"] ?? "";
        _adminChatIds = adminIds.Split(',')
                              .Select(id => long.Parse(id.Trim()))
                              .ToList();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>(),
                DropPendingUpdates = true,
            };

            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Telegram bot started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Telegram bot");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Telegram bot stopped");
        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update.Type == UpdateType.Message && update.Message?.Text != null)
            {
                await HandleMessageAsync(botClient, update.Message, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Telegram update");
        }
    }

    private async Task HandleMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var messageText = message.Text;

        if (!_authenticatedAdmins.TryGetValue(chatId, out var isAuthenticated))
        {
            isAuthenticated = false;
            _authenticatedAdmins[chatId] = false;
        }

        if (!isAuthenticated)
        {
            if (messageText == _adminPassword && _adminChatIds.Contains(chatId))
            {
                _authenticatedAdmins[chatId] = true;
                await ShowMainMenu(chatId, cancellationToken);
            }
            else
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Please enter the admin password:",
                    cancellationToken: cancellationToken);
            }
            return;
        }

        if (_pendingActions.TryGetValue(chatId, out var pendingAction))
        {
            switch (pendingAction.Action)
            {
                case "ban_story":
                    await CompleteStoryBan(chatId, messageText, pendingAction.TargetId, cancellationToken);
                    _pendingActions.Remove(chatId);
                    return;
                case "warn_user":
                    await CompleteUserWarning(chatId, messageText, pendingAction.TargetId, cancellationToken);
                    _pendingActions.Remove(chatId);
                    return;
            }
        }

        switch (messageText)
        {
            case "🚫 Ban Story":
                await ListReportedStories(chatId, cancellationToken);
                break;
            case "⚠️ Warn User":
                await RequestUserIdForWarning(chatId, cancellationToken);
                break;
            case "🏠 Main Menu":
                await ShowMainMenu(chatId, cancellationToken);
                break;
            default:
                if (int.TryParse(messageText, out var id))
                {
                    if (!_pendingActions.ContainsKey(chatId))
                    {
                        // Assume this is a story ID from the reports list
                        _pendingActions[chatId] = ("ban_story", id);
                        await botClient.SendMessage(
                            chatId: chatId,
                            text: $"Please enter the reason for banning story (ID: {id}):",
                            cancellationToken: cancellationToken);
                        return;
                    }
                }
                await ShowMainMenu(chatId, cancellationToken);
                break;
        }
    }

    private async Task ShowMainMenu(long chatId, CancellationToken cancellationToken)
    {
        var keyboard = new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("🚫 Ban Story") },
            new[] { new KeyboardButton("⚠️ Warn User") }
        })
        {
            ResizeKeyboard = true
        };

        await _botClient.SendMessage(
            chatId: chatId,
            text: "Admin Dashboard - Main Menu",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task ListReportedStories(long chatId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var reportedStories = await dbContext.Reports
                .Include(r => r.Story)
                .ThenInclude(s => s.Owner)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .Take(10)
                .ToListAsync();

            if (!reportedStories.Any())
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "No reported stories found.",
                    cancellationToken: cancellationToken);
                return;
            }

            var message = "📋 Reported Stories:\n\n" + string.Join("\n\n", 
                reportedStories.Select(r => 
                    $"📖 Story: {r.Story.Title} (ID: {r.StoryId})\n" +
                    $"👤 Author: {r.Story.Owner.Login}\n" +
                    $"⚠️ Reason: {r.Reason}\n" +
                    $"📄 Details: {r.Content}\n" +
                    $"🕒 Reported at: {r.CreatedAt:g}"));

            await _botClient.SendMessage(
                chatId: chatId,
                text: message,
                cancellationToken: cancellationToken);

            await _botClient.SendMessage(
                chatId: chatId,
                text: "Enter the Story ID to ban or /menu to return:",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing reported stories");
            await _botClient.SendMessage(
                chatId: chatId,
                text: "❌ Error fetching reported stories",
                cancellationToken: cancellationToken);
        }
    }

    private async Task CompleteStoryBan(long chatId, string reason, int storyId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var story = await dbContext.Stories
                .Include(s => s.Parts)
                .Include(s => s.StoryTags)
                .Include(s => s.Likes)
                .Include(s => s.Comments)
                .Include(s => s.Reports)
                .Include(s => s.Owner)
                .FirstOrDefaultAsync(s => s.Id == storyId);

            if (story == null)
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: $"❌ Story {storyId} not found",
                    cancellationToken: cancellationToken);
                return;
            }

            // Delete the story
            dbContext.StoryParts.RemoveRange(story.Parts);
            dbContext.StoryTags.RemoveRange(story.StoryTags);
            dbContext.Likes.RemoveRange(story.Likes);
            dbContext.Comments.RemoveRange(story.Comments);
            dbContext.Reports.RemoveRange(story.Reports);
            dbContext.Stories.Remove(story);

            await dbContext.SaveChangesAsync();

            // Notify the author
            if (!string.IsNullOrEmpty(story.Owner.Email))
            {
                try
                {
                    await _emailService.SendEmailAsync(
                        story.Owner.Email,
                        "Your story has been removed",
                        $"Dear {story.Owner.Login},\n\n" +
                        $"Your story \"{story.Title}\" has been removed for the following reason:\n\n" +
                        $"{reason}\n\n" +
                        $"If you believe this is a mistake, please contact support.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send email notification");
                }
            }

            await _botClient.SendMessage(
                chatId: chatId,
                text: $"✅ Story deleted: {story.Title}\nID: {storyId}\nReason: {reason}",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting story");
            await _botClient.SendMessage(
                chatId: chatId,
                text: $"❌ Error deleting story: {ex.Message}",
                cancellationToken: cancellationToken);
        }
    }

    private async Task RequestUserIdForWarning(long chatId, CancellationToken cancellationToken)
    {
        await _botClient.SendMessage(
            chatId: chatId,
            text: "Please enter the User ID to warn:",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken);
    }

    private async Task CompleteUserWarning(long chatId, string warning, int userId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = await dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: $"❌ User {userId} not found",
                    cancellationToken: cancellationToken);
                return;
            }

            if (!string.IsNullOrEmpty(user.Email))
            {
                await _emailService.SendEmailAsync(
                    user.Email,
                    "Warning from administration",
                    $"Dear {user.Login},\n\n" +
                    $"You have received a warning from the administration:\n\n" +
                    $"{warning}\n\n" +
                    $"Please review our community guidelines.");
            }

            await _botClient.SendMessage(
                chatId: chatId,
                text: $"⚠️ User warned: {user.Login}\nID: {userId}\nWarning: {warning}",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error warning user");
            await _botClient.SendMessage(
                chatId: chatId,
                text: $"❌ Error warning user: {ex.Message}",
                cancellationToken: cancellationToken);
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException 
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogError(errorMessage);
        return Task.CompletedTask;
    }
}