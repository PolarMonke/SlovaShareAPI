using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Polling;

namespace Backend;

public class TelegramBotService : IHostedService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly string _adminPassword;
    private readonly List<long> _adminChatIds;

    private readonly Dictionary<long, bool> _authenticatedAdmins = new();
    private readonly Dictionary<long, string> _pendingActions = new();

    public TelegramBotService(
        ITelegramBotClient botClient,
        IServiceProvider serviceProvider,
        ILogger<TelegramBotService> logger,
        IConfiguration configuration)
    {
        _botClient = botClient;
        _serviceProvider = serviceProvider;
        _logger = logger;

        var botSettings = configuration.GetSection("Telegram");

        _adminPassword = botSettings["AdminPassword"] ?? "defaultPassword";
        var adminIds = botSettings["AdminIds"] ?? "";
        _adminChatIds = adminIds.Split(',')
                              .Select(id => long.Parse(id.Trim()))
                              .ToList();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
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
            switch (pendingAction)
            {
                case "ban_story":
                    await CompleteStoryBan(chatId, messageText, cancellationToken);
                    _pendingActions.Remove(chatId);
                    return;
                case "warn_user":
                    await CompleteUserWarning(chatId, messageText, cancellationToken);
                    _pendingActions.Remove(chatId);
                    return;
            }
        }

        switch (messageText)
        {
            case "🚫 Ban Story":
                await RequestStoryIdForBan(chatId, cancellationToken);
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
                    if (_pendingActions.TryGetValue(chatId, out var action))
                    {
                        switch (action)
                        {
                            case "ban_story_input":
                                _pendingActions[chatId] = "ban_story";
                                await botClient.SendMessage(
                                    chatId: chatId,
                                    text: $"Please enter the reason for banning story (ID: {id}):",
                                    cancellationToken: cancellationToken);
                                return;
                            case "warn_user_input":
                                _pendingActions[chatId] = "warn_user";
                                await botClient.SendMessage(
                                    chatId: chatId,
                                    text: $"Please enter warning message for user (ID: {id}):",
                                    cancellationToken: cancellationToken);
                                return;
                        }
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

    private async Task RequestStoryIdForBan(long chatId, CancellationToken cancellationToken)
    {
        _pendingActions[chatId] = "ban_story_input";
        await _botClient.SendMessage(
            chatId: chatId,
            text: "Please enter the Story ID to ban:",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken);
    }

    private async Task CompleteStoryBan(long chatId, string input, CancellationToken cancellationToken)
    {
        var parts = input.Split(':', 2);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var storyId))
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "❌ Invalid format. Please use: storyId:reason",
                cancellationToken: cancellationToken);
            return;
        }

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
                .FirstOrDefaultAsync(s => s.Id == storyId);

            if (story == null)
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: $"❌ Story {storyId} not found",
                    cancellationToken: cancellationToken);
                return;
            }

            dbContext.StoryParts.RemoveRange(story.Parts);
            dbContext.StoryTags.RemoveRange(story.StoryTags);
            dbContext.Likes.RemoveRange(story.Likes);
            dbContext.Comments.RemoveRange(story.Comments);
            dbContext.Reports.RemoveRange(story.Reports);
            dbContext.Stories.Remove(story);

            await dbContext.SaveChangesAsync();

            await _botClient.SendMessage(
                chatId: chatId,
                text: $"✅ Story deleted: {story.Title}\nID: {storyId}\nReason: {parts[1]}",
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
        _pendingActions[chatId] = "warn_user_input";
        await _botClient.SendMessage(
            chatId: chatId,
            text: "Please enter the User ID to warn:",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken);
    }

    private async Task CompleteUserWarning(long chatId, string input, CancellationToken cancellationToken)
    {
        var parts = input.Split(':', 2);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var userId))
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "❌ Invalid format. Please use: userId:warning",
                cancellationToken: cancellationToken);
            return;
        }

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

            // Here you would implement your warning logic
            // For example, send an email or store the warning in database

            await _botClient.SendMessage(
                chatId: chatId,
                text: $"⚠️ User warned: {user.Login}\nID: {userId}\nWarning: {parts[1]}",
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