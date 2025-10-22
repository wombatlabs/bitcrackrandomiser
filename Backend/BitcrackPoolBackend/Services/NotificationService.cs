using BitcrackPoolBackend.Options;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace BitcrackPoolBackend.Services
{
    public class NotificationService
    {
        private readonly PoolOptions _options;
        private readonly TelegramBotClient? _telegramClient;

        public NotificationService(IOptions<PoolOptions> options)
        {
            _options = options.Value;
            if (!string.IsNullOrWhiteSpace(_options.TelegramBotToken))
            {
                _telegramClient = new TelegramBotClient(_options.TelegramBotToken);
            }
        }

        public async Task SendKeyFoundAsync(string message, CancellationToken cancellationToken)
        {
            if (_telegramClient is null)
                return;

            if (string.IsNullOrWhiteSpace(_options.TelegramChatId))
                return;

            try
            {
                await _telegramClient.SendTextMessageAsync(
                    chatId: _options.TelegramChatId,
                    text: message,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // swallow telegram errors; logging handled by caller
            }
        }
    }
}

