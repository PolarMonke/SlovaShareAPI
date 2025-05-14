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
    private readonly AppDbContext _dbContext;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly string _adminPassword;

    private readonly Dictionary<long, bool> _authenticatedAdmins = new();
    private readonly Dictionary<long, string> _pendingActions = new();

    public TelegramBotService(
        ITelegramBotClient botClient,
        AppDbContext dbContext,
        ILogger<TelegramBotService> logger,
        IConfiguration configuration)
    {
        _botClient = botClient;
        _dbContext = dbContext;
        _logger = logger;
        _adminPassword = configuration["Telegram:AdminPassword"] ?? "defaultPassword";
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
            switch (update.Type)
            {
                case UpdateType.Message:
                    await HandleMessageAsync(botClient, update.Message!, cancellationToken);
                    break;
                case UpdateType.CallbackQuery:
                    await HandleCallbackQueryAsync(botClient, update.CallbackQuery!, cancellationToken);
                    break;
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

        if (messageText == null) return;

        _logger.LogInformation($"Received message from {chatId}: {messageText}");

        if (!_authenticatedAdmins.TryGetValue(chatId, out var isAuthenticated))
        {
            isAuthenticated = false;
            _authenticatedAdmins[chatId] = false;
        }

        if (!isAuthenticated)
        {
            if (messageText == _adminPassword)
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
                case "ban_user":
                    await CompleteUserBan(chatId, messageText, cancellationToken);
                    _pendingActions.Remove(chatId);
                    return;
                case "request_edit":
                    await CompleteEditRequest(chatId, messageText, cancellationToken);
                    _pendingActions.Remove(chatId);
                    return;
            }
        }

        switch (messageText)
        {
            case "📋 View Reports":
                await ListPendingReports(chatId, cancellationToken);
                break;
            case "🚫 Ban Story":
                await RequestStoryIdForBan(chatId, cancellationToken);
                break;
            case "👤 Ban User":
                await RequestUserIdForBan(chatId, cancellationToken);
                break;
            case "✏️ Request Edit":
                await RequestStoryIdForEdit(chatId, cancellationToken);
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
                            case "ban_user_input":
                                _pendingActions[chatId] = "ban_user";
                                await botClient.SendMessage(
                                    chatId: chatId,
                                    text: $"Please enter the reason for banning user (ID: {id}):",
                                    cancellationToken: cancellationToken);
                                return;
                            case "request_edit_input":
                                _pendingActions[chatId] = "request_edit";
                                await botClient.SendMessage(
                                    chatId: chatId,
                                    text: $"Please enter your edit request message for story (ID: {id}):",
                                    cancellationToken: cancellationToken);
                                return;
                        }
                    }
                }
                await ShowMainMenu(chatId, cancellationToken);
                break;
        }
    }

    private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var data = callbackQuery.Data;

        if (data == null) return;

        if (!_authenticatedAdmins.TryGetValue(chatId, out var isAuthenticated) || !isAuthenticated)
        {
            await botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "You need to authenticate first",
                cancellationToken: cancellationToken);
            return;
        }

        if (data.StartsWith("ban_story:"))
        {
            var storyId = data.Split(':')[1];
            await botClient.SendMessage(
                chatId: chatId,
                text: $"Please enter the reason for banning story (ID: {storyId}):",
                cancellationToken: cancellationToken);
            _pendingActions[chatId] = "ban_story";
        }
        else if (data.StartsWith("ban_user:"))
        {
            var userId = data.Split(':')[1];
            await botClient.SendMessage(
                chatId: chatId,
                text: $"Please enter the reason for banning user (ID: {userId}):",
                cancellationToken: cancellationToken);
            _pendingActions[chatId] = "ban_user";
        }
        else if (data.StartsWith("request_edit:"))
        {
            var storyId = data.Split(':')[1];
            await botClient.SendMessage(
                chatId: chatId,
                text: $"Please enter your edit request message for story (ID: {storyId}):",
                cancellationToken: cancellationToken);
            _pendingActions[chatId] = "request_edit";
        }

        await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
    }

    private async Task ShowMainMenu(long chatId, CancellationToken cancellationToken)
    {
        var keyboard = new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("📋 View Reports") },
            new[] { new KeyboardButton("🚫 Ban Story"), new KeyboardButton("👤 Ban User") },
            new[] { new KeyboardButton("✏️ Request Edit") }
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

    private async Task ListPendingReports(long chatId, CancellationToken cancellationToken)
    {
        var pendingReports = await _dbContext.Reports
            .Include(r => r.Story)
            .ThenInclude(s => s.Owner)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .ToListAsync();

        if (!pendingReports.Any())
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "No pending reports found.",
                cancellationToken: cancellationToken);
            return;
        }

        foreach (var report in pendingReports)
        {
            var message = $"📝 Report ID: {report.Id}\n\n" +
                        $"📖 Story: {report.Story.Title} (ID: {report.StoryId})\n" +
                        $"👤 Author: {report.Story.Owner.Login} (ID: {report.Story.OwnerId})\n" +
                        $"⚠️ Reason: {report.Reason}\n" +
                        $"📄 Details: {report.Content}\n\n" +
                        $"🕒 Reported at: {report.CreatedAt:g}";

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🚫 Ban Story", $"ban_story:{report.StoryId}"),
                    InlineKeyboardButton.WithCallbackData("👤 Ban Author", $"ban_user:{report.Story.OwnerId}")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("✏️ Request Edit", $"request_edit:{report.StoryId}")
                }
            });

            await _botClient.SendMessage(
                chatId: chatId,
                text: message,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        await ShowMainMenu(chatId, cancellationToken);
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

    private async Task CompleteStoryBan(long chatId, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "Invalid reason provided. Please try again.",
                cancellationToken: cancellationToken);
            return;
        }

        // In a real implementation, you would parse the story ID from the pending action
        // and perform the ban operation here

        await _botClient.SendMessage(
            chatId: chatId,
            text: $"Story has been banned. Reason: {reason}",
            cancellationToken: cancellationToken);

        await ShowMainMenu(chatId, cancellationToken);
    }

    private async Task RequestUserIdForBan(long chatId, CancellationToken cancellationToken)
    {
        _pendingActions[chatId] = "ban_user_input";
        await _botClient.SendMessage(
            chatId: chatId,
            text: "Please enter the User ID to ban:",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken);
    }

    private async Task CompleteUserBan(long chatId, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "Invalid reason provided. Please try again.",
                cancellationToken: cancellationToken);
            return;
        }

        // In a real implementation, you would parse the user ID from the pending action
        // and perform the ban operation here

        await _botClient.SendMessage(
            chatId: chatId,
            text: $"User has been banned. Reason: {reason}",
            cancellationToken: cancellationToken);

        await ShowMainMenu(chatId, cancellationToken);
    }

    private async Task RequestStoryIdForEdit(long chatId, CancellationToken cancellationToken)
    {
        _pendingActions[chatId] = "request_edit_input";
        await _botClient.SendMessage(
            chatId: chatId,
            text: "Please enter the Story ID to request edit for:",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken);
    }

    private async Task CompleteEditRequest(long chatId, string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "Invalid message provided. Please try again.",
                cancellationToken: cancellationToken);
            return;
        }

        // In a real implementation, you would parse the story ID from the pending action
        // and send the edit request here

        await _botClient.SendMessage(
            chatId: chatId,
            text: $"Edit request has been sent. Message: {message}",
            cancellationToken: cancellationToken);

        await ShowMainMenu(chatId, cancellationToken);
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