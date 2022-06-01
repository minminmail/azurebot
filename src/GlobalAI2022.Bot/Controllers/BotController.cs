using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;

namespace GlobalAI2022.Bot.Controllers;

/// <summary>
/// A controller to handle a request. Dependency Injection will provide the 'Adapter' and '<see cref="IBot"/>' implementation at runtime.
/// Multiple different <see cref="IBot"/> implementations running at different endpoints can be achieved by specifying a more specific
/// type (i.e, different interface) for the bot constructor argument.
/// </summary>
[ApiController]
[Route(@"api/messages")]
public class BotController : ControllerBase
{
    private readonly IBot _bot;
    private readonly IBotFrameworkHttpAdapter _adapter;

    /// <summary>
    /// Initializes a new instance of the <see cref="BotController"/> class.
    /// </summary>
    /// <param name="adapter">A bot adapter to use.</param>
    /// <param name="bot">A bot that can operate on incoming activities.</param>
    public BotController(IBotFrameworkHttpAdapter adapter, IBot bot)
    {
        _adapter = adapter;
        _bot = bot;
    }

    /// <summary>
    /// Handles a request for the bot.
    /// </summary>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation of handling the request for the bot.
    /// </returns>
    [HttpGet]
    [HttpPost]
    public async Task HandleAsync()
    {
        // Delegate the processing of the HTTP request to the adapter. The adapter will invoke the bot.
        await _adapter.ProcessAsync(Request, Response, _bot);
    }
}
