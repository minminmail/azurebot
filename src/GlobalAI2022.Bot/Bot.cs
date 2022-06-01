using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;

namespace GlobalAI2022.Bot;

/// <summary>
/// A custom implementation of a bot's handler for specific dialogs.
/// </summary>
/// <typeparam name="TDialog">The specific dialog type.</typeparam>
internal class Bot<TDialog> : ActivityHandler
    where TDialog : Dialog
{
    private readonly ILogger _logger;

    private readonly ConversationState _conversationState;
    private readonly UserState _userState;

    private readonly TDialog _dialog;

    /// <summary>
    /// Initializes a new instance of the <see cref="Bot{TDialog}"/> class.
    /// </summary>
    /// <param name="dialog">The main <see cref="Dialog"/> for this bot.</param>
    /// <param name="logger">A logger for this bot.</param>
    public Bot(TDialog dialog, ConversationState conversationState, UserState userState, ILogger<Bot<TDialog>> logger)
    {
        _conversationState = conversationState;
        _dialog = dialog;
        _logger = logger;
        _userState = userState;
    }

    /// <inheritdoc/>
    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        _logger.LogInformation($@"Running dialog with Message Activity and Id '{turnContext.Activity.Id}'...");

        // Run the Dialog with the new message Activity.
        await _dialog.RunAsync(turnContext, _conversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
    }

    public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
    {
        await base.OnTurnAsync(turnContext, cancellationToken);

        if (turnContext.Activity.Type == ActivityTypes.ConversationUpdate)
        {
            await _dialog.RunAsync(turnContext, _conversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
        }

        // Save any state changes that might have occurred during the turn.
        await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
        await _userState.SaveChangesAsync(turnContext, false, cancellationToken);
    }
}