using System.Diagnostics;

using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.ApplicationInsights.Core;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;

using IMiddleware = Microsoft.Bot.Builder.IMiddleware;

namespace GlobalAI2022.Bot;

internal class CloudAdapterWithErrorHandler : CloudAdapter
{
    private readonly IBotTelemetryClient _botTelemetryClient;

    private readonly ConversationState _conversationState;

    public CloudAdapterWithErrorHandler(
        BotFrameworkAuthentication botFrameworkAuthentication, 
        ConversationState conversationState, 
        IBotTelemetryClient botTelemetryClient, 
        IEnumerable<IMiddleware> middlewares, 
        ILogger<CloudAdapterWithErrorHandler> logger)
        : base(botFrameworkAuthentication, logger)
    {
        OnTurnError = ErrorHandlerAsync;

        _botTelemetryClient = botTelemetryClient;
        _conversationState = conversationState;

        InitializeWithDefaultMiddlewares(middlewares);
    }

    protected virtual async Task ErrorHandlerAsync(ITurnContext turnContext, Exception exception)
    {
        // Send a message to the user
        var errorMessageText = "The bot encountered an error or bug.";
        var errorMessage = MessageFactory.Text(errorMessageText, errorMessageText, InputHints.ExpectingInput);
        await turnContext.SendActivityAsync(errorMessage);

        errorMessageText = "To continue to run this bot, please fix the bot source code.";
        errorMessage = MessageFactory.Text(errorMessageText, errorMessageText, InputHints.ExpectingInput);
        await turnContext.SendActivityAsync(errorMessage);

        // Send the exception telemetry
        _botTelemetryClient.TrackException(exception, new Dictionary<string, string> { { @"Bot exception caught in", $"{GetType().Name} - {nameof(OnTurnError)}" } });

        // Log any leaked exception from the application.
        Logger.LogError(exception, $@"Unhandled error on method '{nameof(OnTurnError)}': {exception.Message}!");

        if (_conversationState != null)
        {
            try
            {
                // Delete the conversationState for the current conversation to prevent the
                // bot from getting stuck in a error-loop caused by being in a bad state.
                await _conversationState.DeleteAsync(turnContext);
            }
            catch (Exception e)
            {
                Logger.LogError(e, $@"Exception caught on attempting to Delete ConversationState: {e.Message}");
            }
        }

        // Send a trace activity, which will be displayed in the Bot Framework Emulator
        await turnContext.TraceActivityAsync($@"{nameof(OnTurnError)}Trace", exception.Message, "https://www.botframework.com/schemas/error", nameof(OnTurnError));
    }

    private void InitializeWithDefaultMiddlewares(IEnumerable<IMiddleware> middlewares)
    {
        if (middlewares?.Any() ?? false)
        {
            var dicMiddlewares = middlewares.ToDictionary(i => i.GetType(), i => i);

            if (Debugger.IsAttached && dicMiddlewares.TryGetValue(typeof(InspectionMiddleware), out var inspectionMiddleware))
            {
                Use(inspectionMiddleware);
            }

            if (dicMiddlewares.TryGetValue(typeof(TelemetryInitializerMiddleware), out var telemetryInitializerMiddleware))
            {
                Use(telemetryInitializerMiddleware);

                if (dicMiddlewares.TryGetValue(typeof(TelemetryLoggerMiddleware), out var telemetryLoggerMiddleware))
                {
                    Use(telemetryLoggerMiddleware);
                }
            }

            if (dicMiddlewares.TryGetValue(typeof(TranscriptLoggerMiddleware), out var transcriptLoggerMiddleware))
            {
                Use(transcriptLoggerMiddleware);
            }

            if (dicMiddlewares.TryGetValue(typeof(ShowTypingMiddleware), out var showTypingMiddleware))
            {
                Use(showTypingMiddleware);
            }

            if (dicMiddlewares.TryGetValue(typeof(AutoSaveStateMiddleware), out var autoSaveStateMiddleware))
            {
                Use(autoSaveStateMiddleware);
            }
        }
    }
}
