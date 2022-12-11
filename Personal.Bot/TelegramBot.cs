using System.Net;

using Personal.Bot.Services;

using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Personal.Bot;

public class TelegramBot
{
    private readonly ITelegramBotClient _client;
    private readonly GisMeteoService _gisMeteoService;
    private readonly ILogger<TelegramBot> _logger;

    private readonly List<string> _reactions = new()
    {
        "👌", "😘", "😂", "❤️", "😍", "😊"
    };

    private readonly ReceiverOptions _receiverOptions;

    private readonly Dictionary<long, CancellationTokenSource> _subscribes = new();

    private CancellationToken _token;

    public TelegramBot(IConfiguration configuration, ILogger<TelegramBot> logger, GisMeteoService gisMeteoService)
    {
        _logger = logger;
        _gisMeteoService = gisMeteoService;
        _client = new TelegramBotClient(configuration.GetValue<string>("App:Token"));

        _receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
        };
    }

    public Task Start(CancellationToken token)
    {
        _token = token;
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
        if (update.Message is not { } message)
            return;
        if (message.Text is not { } messageText)
            return;

        var (chatId, nickname, username) = GetInfo(message);
        _logger.LogInformation("=>Message [{Text}] received from room #{RoomId} [{NickName} - {UserName}] ",
            messageText, chatId, nickname, username);

        await Process(message);
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

    private Tuple<long, string, string> GetInfo(Message? message)
    {
        var chatId = message?.Chat.Id ?? 0;
        var nickname = message?.From?.Username ?? "Anonymous";
        var username = message?.From?.FirstName ?? "Anonymous";

        return new Tuple<long, string, string>(chatId, nickname, username);
    }

    private async Task Process(Message? message)
    {
        if (message?.Text is null)
            return;

        if (message.Text.Contains("научись") || message.Text.StartsWith("/request"))
        {
            await Reaction(Reactions.SayOk, message);
            return;
        }

        if (message.Text.StartsWith("/weather"))
        {
            await Reaction(Reactions.SayWeather, message);
            return;
        }
        
        if (message.Text.Contains("Кусь") || message.Text.Contains("кусь"))
        {
            await Reaction(Reactions.Bite, message);
            return;
        }

        // if (message.Text.StartsWith("/subscribe"))
        // {
        //     await Reaction(Reactions.Subscribe, message);
        //     return;
        // }
        //
        // if (message.Text.StartsWith("/unsubscribe"))
        // {
        //     await Reaction(Reactions.UnSubscribe, message);
        //     return;
        // }

        await Reaction(Reactions.Default, message);
    }

    private async Task Reaction(Reactions reactions, Message? message)
    {
        switch (reactions)
        {
            case Reactions.Default:
                await ReactionDefault(message);
                break;
            case Reactions.SayOk:
                await ReactionSayOk(message);
                break;
            case Reactions.SayWeather:
                await ReactionSayWeather(message);
                break;
            case Reactions.Subscribe:
                await ReactionSubscribe(message);
                break;
            case Reactions.UnSubscribe:
                await ReactionUnSubscribe(message);
                break;
            case Reactions.Bite:
                await ReactionBite(message);
                break;
        }
    }

    private async Task ReactionDefault(Message? message)
    {
        var (chatId, nickname, username) = GetInfo(message);

        await _client.SendTextMessageAsync(
            chatId,
            $"Привет {username} (@{nickname}) ! По вашим просьбам я научился рассказывать о погоде и реагировать на слово 'научись' ! :) Пишите мне чему еще научиться!",
            cancellationToken: _token);
    }

    private async Task ReactionSayOk(Message? message)
    {
        var (chatId, nickname, username) = GetInfo(message);

        await _client.SendTextMessageAsync(
            chatId,
            $"Хорошо {username} (@{nickname}) ! Я передам моему создателю твою просьбу!",
            cancellationToken: _token);
    }
    
    private async Task ReactionBite(Message? message)
    {
        var (chatId, nickname, username) = GetInfo(message);

        var client = new WebClient();
        var image = client.DownloadData("http://i.giphy.com/7NUDCypKavZzkQGyp9.gif");
        Stream stream = new MemoryStream(image);
        
        await _client.SendVideoAsync(
            chatId,
            stream,
            cancellationToken: _token);
    }

    private async Task ReactionSayWeather(Message? message)
    {
        var (chatId, nickname, username) = GetInfo(message);

        try
        {
            var client = new WebClient();
            var image = client.DownloadData("https://weather.paradox-server.ru/weather.php");
            Stream stream = new MemoryStream(image);

            await _client.SendPhotoAsync(
                chatId,
                stream,
                cancellationToken: _token);
            
            await stream.DisposeAsync();
        }
        catch (Exception e)
        {
            await _client.SendTextMessageAsync(
                chatId,
                "Чот у меня проблемы с прогнозом погоды",
                cancellationToken: _token);
        }
    }

    private async Task TimedTask(long talker)
    {
        await _client.SendTextMessageAsync(
            talker,
            _reactions[new Random().Next(0, _reactions.Count)],
            cancellationToken: _token);
    }

    private Task ReactionSubscribe(Message? message)
    {
        var (chatId, nickname, username) = GetInfo(message);

        _subscribes.Add(chatId, new CancellationTokenSource());

        Task.Run(async () =>
        {
            while (!_subscribes[chatId].Token.IsCancellationRequested)
            {
                await TimedTask(chatId);
                await Task.Delay(3000, _subscribes[chatId].Token);
            }
        }, _token);

        return Task.CompletedTask;
    }

    private Task ReactionUnSubscribe(Message? message)
    {
        var (chatId, nickname, username) = GetInfo(message);

        _subscribes[chatId].Cancel();
        _subscribes[chatId].Dispose();

        _subscribes.Remove(chatId);

        return Task.CompletedTask;
    }

    // Reactions

    private enum Reactions
    {
        Default = 0,
        SayOk = 1,
        SayWeather = 2,
        Subscribe = 3,
        UnSubscribe = 4,
        Bite = 5,
    }
}