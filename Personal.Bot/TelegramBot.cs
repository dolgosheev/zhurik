using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Personal.Bot;

public class TelegramBot
{
    private readonly ITelegramBotClient _client;
    private readonly ILogger<TelegramBot> _logger;
    private readonly ReceiverOptions _receiverOptions;

    public TelegramBot(IConfiguration configuration, ILogger<TelegramBot> logger)
    {
        _logger = logger;
        _client = new TelegramBotClient(configuration.GetValue<string>("App:Token"));

        _receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
        };
    }

    public Task Start(CancellationToken token)
    {
        _logger.LogInformation("Bot started");
        _client.ReceiveAsync(
            HandleUpdateAsync,
            HandlePollingErrorAsync,
            _receiverOptions,
            token
        ).GetAwaiter().GetResult();

        _logger.LogInformation("Bot stopped");
        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        // Only process Message updates: https://core.telegram.org/bots/api#message
        if (update.Message is not { } message)
            return;
        // Only process text messages
        if (message.Text is not { } messageText)
            return;

        var chatId = message.Chat.Id;
        var nickname = message.From?.Username ?? "Anonymous";
        var username = message.From?.FirstName ?? "Anonymous";

        _logger.LogWarning("=> {Text} from room #{RoomId}",messageText,chatId);
        await botClient.SendTextMessageAsync(
            chatId,
            $"Привет {username} (@{nickname}) ! Я пока ничерта не умею :) Но если ты мне напишешь чему научиться - я научусь)",
            cancellationToken: cancellationToken);
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(errorMessage);
        return Task.CompletedTask;
    }
}