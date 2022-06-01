using System.Globalization;

using Azure;
using Azure.AI.Language.Conversations;
using Azure.AI.Language.QuestionAnswering;
using Azure.AI.TextAnalytics;

using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;

namespace GlobalAI2022.Bot;

internal class MainDialog : Dialog
{
    private readonly IDictionary<TargetKind, Func<DialogContext, TargetIntentResult, CancellationToken, Task>> _intentHandlers;

    private readonly IHttpClientFactory _httpClientFactory;

    private readonly ConversationAnalysisClient _conversationAnalysisClient;
    private readonly ConversationsProject _conversationsProject;

    private readonly TextAnalyticsClient _textAnalyticsClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainDialog"/> class.
    /// </summary>
    /// <param name="httpClientFactory">A factory to get valid instances of <see cref="HttpClient"/>.</param>
    /// <param name="botTelemetryClinet">A bot telemetry client to gather data from this dialog.</param>
    public MainDialog(IHttpClientFactory httpClientFactory, IBotTelemetryClient botTelemetryClinet) : base(nameof(MainDialog))
    {
        _httpClientFactory = httpClientFactory;

        TelemetryClient = botTelemetryClinet;

        var deploymentSlot = @"production"; // This can be 'test', 'prod' or 'production'
        var orchestratorName = "Orquestador";

        var endpoint = new Uri(@"https://test-relv-orq.cognitiveservices.azure.com");

        var credential = new AzureKeyCredential(@"866c2cbb833b4c83bd295e9353c0561d");

        _conversationAnalysisClient = new ConversationAnalysisClient(endpoint, credential);
        _conversationsProject = new ConversationsProject(orchestratorName, deploymentSlot);

        _textAnalyticsClient = new TextAnalyticsClient(endpoint, credential);

        // Si quisieramos llamar directamente al Custom Quesiton Answering, usaríamos QuestionAnsweringClient

        _intentHandlers = new Dictionary<TargetKind, Func<DialogContext, TargetIntentResult, CancellationToken, Task>>()
        {
            { TargetKind.QuestionAnswering, AsnwerQuestionHandlerAsync },
            { TargetKind.Conversation, ConversationHandlerAsync },
        };
    }

    /// <inheritdoc/>
    public override async Task<DialogTurnResult> BeginDialogAsync(DialogContext dc, object options = null, CancellationToken cancellationToken = default)
    {
        if (dc.Context.Activity.Type == ActivityTypes.Message)
        {
            var message = dc.Context.Activity.Text;

            //var detectLanguageResult = await _textAnalyticsClient.DetectLanguageAsync(message, DetectLanguageInput.None, cancellationToken);

            ////var analyzeConversationOptions = new AnalyzeConversationOptions()
            ////{
            ////    Language = detectLanguageResult.Value.Iso6391Name,
            ////};

            ////var response = _conversationAnalysisClient.AnalyzeConversation(message, _conversationsProject, analyzeConversationOptions, cancellationToken);

            ////var orchestratorPrediction = response.Value.Prediction as OrchestratorPrediction;

            ////var targetIntentResult = orchestratorPrediction.Intents[orchestratorPrediction.TopIntent];

            TextConversationItem input = new TextConversationItem(participantId: "1", id: "1", text: message)
            {
                Language = "es"
            };

            AnalyzeConversationOptions opt = new AnalyzeConversationOptions(input);

            //Response<AnalyzeConversationTaskResult> response = await _conversationAnalysisClient.AnalyzeConversationAsync(message, _conversationsProject, opt);

            var response = _conversationAnalysisClient.AnalyzeConversation(message, _conversationsProject, opt);

            CustomConversationalTaskResult customConversationalTaskResult = response.Value as CustomConversationalTaskResult;
            
            ////ConversationPrediction conversationPrediction = customConversationalTaskResult.Results.Prediction as ConversationPrediction;

            var orchestratorPrediction = customConversationalTaskResult.Results.Prediction as OrchestratorPrediction;

            var targetIntentResult = orchestratorPrediction.Intents[orchestratorPrediction.TopIntent];

            if (_intentHandlers.TryGetValue(targetIntentResult.TargetKind, out var handlerAsync))
            {
                await handlerAsync(dc, targetIntentResult, cancellationToken);
            }
            else
            {
                await dc.Context.SendActivityAsync(@"Lo siento… ¡pero no tengo el conocimiento sobre lo que me preguntas!", cancellationToken: cancellationToken);
            }
        }

        return await dc.EndDialogAsync(cancellationToken: cancellationToken);
    }

    private static async Task AsnwerQuestionHandlerAsync(DialogContext dc, TargetIntentResult targetIntentResult, CancellationToken cancellationToken)
    {
        var questionAnsweringTargetIntentResult = targetIntentResult as QuestionAnsweringTargetIntentResult;

        await dc.Context.SendActivityAsync(questionAnsweringTargetIntentResult.Result?.Answers?.Where(a => a.Confidence >= 0.40d)
                                                                                               .OrderBy(a => a.Confidence)
                                                                                               .FirstOrDefault()?.Answer ?? @"¡Oops... no encontré una respuesta para lo que me preguntaste!", cancellationToken: cancellationToken);
    }

    private async Task ConversationHandlerAsync(DialogContext dc, TargetIntentResult targetIntentResult, CancellationToken cancellationToken)
    {
        var conversationTargetIntentResult = targetIntentResult as ConversationTargetIntentResult;

        var rover = conversationTargetIntentResult.Result.Prediction.Entities.SingleOrDefault(e => e.Category == @"Rovers")?.Text;

        if (!IsValidRover(rover))
        {
            await dc.Context.SendActivityAsync(@"Vaya, parece que no me has dicho el nombre del rover que quieres o me has pedido de uno que no conozco. Los rovers que conozco son: Curiosity, Opportunity y Spirit.", cancellationToken: cancellationToken);
            return;
        }

        var earthDateText = conversationTargetIntentResult.Result.Prediction.Entities.SingleOrDefault(e => e.Category == @"EarthDate")?.Text;

        if (!DateTime.TryParse(earthDateText, CultureInfo.GetCultureInfo("es"), DateTimeStyles.None, out var earthDate))
        {
            await dc.Context.SendActivityAsync(@"Vaya, parece que no me haz dicho la fecha para la cual quieres ver fotos", cancellationToken: cancellationToken);
            return;
        }

        var response = await _httpClientFactory.CreateClient()
                                               .GetFromJsonAsync<MarsRoverPhotos>(new Uri($@"https://api.nasa.gov/mars-photos/api/v1/rovers/{rover}/photos?earth_date={earthDate:yyyy-MM-dd}&camera=fhaz&api_key=DEMO_KEY"), cancellationToken);

        if (response.Photos.Any())
        {
            await dc.Context.SendActivityAsync(MessageFactory.Attachment(response.Photos.Select(p => new Attachment(@"image/jpg", p.ImageSource))), cancellationToken);
        }
        else
        {
            await dc.Context.SendActivityAsync($@"¡No he encontrado fotos para el rover {rover} en la fecha {earthDate:yyyy-MM-dd}!", cancellationToken: cancellationToken);
        }
    }

    private static bool IsValidRover(string rover)
    {
        var validRovers = new[] { "Curiosity", "Opportunity", "Spirit" };

        return !string.IsNullOrWhiteSpace(rover) && validRovers.Contains(rover, StringComparer.OrdinalIgnoreCase);
    }
}
