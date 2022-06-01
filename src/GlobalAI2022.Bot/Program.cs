using System.Diagnostics;

using GlobalAI2022.Bot;

using Microsoft.ApplicationInsights.Extensibility;

using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.ApplicationInsights;
using Microsoft.Bot.Builder.Integration.ApplicationInsights.Core;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;

using Microsoft.Extensions.Logging.ApplicationInsights;

using IMiddleware = Microsoft.Bot.Builder.IMiddleware;

var programType = typeof(Program);
var applicationName = programType.Assembly.FullName;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions()
{
    ApplicationName = applicationName,
    Args = args,
    ContentRootPath = Directory.GetCurrentDirectory(),
    WebRootPath = Path.Combine(Directory.GetCurrentDirectory(), @"wwwroot"),
});

builder.Configuration.SetBasePath(Directory.GetCurrentDirectory());

if (Debugger.IsAttached)
{
    builder.Configuration.AddJsonFile(@"appsettings.debug.json", optional: true, reloadOnChange: true);
}

builder.Configuration.AddJsonFile($@"appsettings.{Environment.UserName}.json", optional: true, reloadOnChange: true)
                     .AddEnvironmentVariables();

/*************************/
/* Logging Configuration */
/*************************/

if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddDebug()
                   .AddConsole()
                   ;
}

builder.Logging.AddApplicationInsights()
               .AddFilter<ApplicationInsightsLoggerProvider>(applicationName, LogLevel.Trace)
               .AddFilter<ApplicationInsightsLoggerProvider>(programType.FullName, LogLevel.Trace); // Adding this filter to ensure logs of all severity from here are sent to Application Insights.

/**************************/
/* Services Configuration */
/**************************/

// Add application services
builder.Services.AddApplicationInsightsTelemetry(builder.Configuration)
                .AddHttpClient()
                .AddHttpContextAccessor()
                .AddLocalization(options =>
                {
                    options.ResourcesPath = @"Resources";
                })
                .AddMemoryCache()
                .AddOptions()
                .AddRouting()
                ;

// Add MVC services
builder.Services.AddControllers(options =>
                {
                    options.RequireHttpsPermanent = true;
                    options.SuppressAsyncSuffixInActionNames = true;
                })
                ;

// Add bot-state related services
builder.Services.AddSingleton<IStorage, MemoryStorage>()
                .AddSingleton<UserState>().AddSingleton<BotState, UserState>()
                .AddSingleton<ConversationState>().AddSingleton<BotState, ConversationState>()
                ;

// Add other bot related servives                
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>() // Create the Bot Framework Authentication to be used with the Bot Adapter.
                .AddSingleton<IBotFrameworkHttpAdapter, CloudAdapterWithErrorHandler>() // Create the Bot Adapter with error handling enabled.
                .AddSingleton<MainDialog>()
                .AddTransient<IBot, Bot<MainDialog>>() // Create the bot as a transient. In this case any Controller should be expecting an IBot as dependency.
                ;

// Add bot-related telemetry services
builder.Services.AddSingleton<IBotTelemetryClient, BotTelemetryClient>() // Create the telemetry client.
                .AddSingleton<ITelemetryInitializer, OperationCorrelationTelemetryInitializer>() // Add telemetry initializer that will set the correlation context for all telemetry items.
                .AddSingleton<ITelemetryInitializer, TelemetryBotIdInitializer>() // Add telemetry initializer that sets the user ID and session ID (in addition to other bot-specific properties such as activity ID).
                ;

// Add bot-middleware realted services
builder.Services
                .AddSingleton(serviceProvider => new AutoSaveStateMiddleware(serviceProvider.GetServices<BotState>().ToArray()))
                .AddSingleton<TelemetryLoggerMiddleware>()
                .AddSingleton<TelemetryInitializerMiddleware>()
                .AddSingleton<InspectionMiddleware>()
                .AddSingleton<ShowTypingMiddleware>()
                .AddSingleton<TranscriptLoggerMiddleware>()
                .AddSingleton<IMiddleware, TelemetryLoggerMiddleware>()
                .AddSingleton<IMiddleware, TelemetryInitializerMiddleware>()
                .AddSingleton<BotState, InspectionState>()
                .AddSingleton<IMiddleware, AutoSaveStateMiddleware>(serviceProvider => serviceProvider.GetRequiredService<AutoSaveStateMiddleware>())
                .AddSingleton<IMiddleware, InspectionMiddleware>()
                .AddSingleton<IMiddleware, ShowTypingMiddleware>()
                .AddSingleton<IMiddleware, TranscriptLoggerMiddleware>()
                .AddSingleton<InspectionState>()
                .AddSingleton<ITranscriptLogger, MemoryTranscriptStore>()
                ;

/****************************************/
/* Application Middleware Configuration */
/****************************************/

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

var supportedCultures = app.Configuration.GetSection(@"SupportedCultures").Get<string[]>() ?? Array.Empty<string>();

app.UseHttpsRedirection()
   .UseRequestLocalization(options =>
   {
       options.AddSupportedCultures(supportedCultures)
              .AddSupportedUICultures(supportedCultures)
              .SetDefaultCulture(supportedCultures.FirstOrDefault() ?? string.Empty)
              ;

       options.ApplyCurrentCultureToResponseHeaders = true;
   })
   .UseDefaultFiles()
   .UseStaticFiles()
   .UseWebSockets()
   .UseRouting()
   .UseAuthentication()
   .UseAuthorization()
   .UseEndpoints(endpoints =>
   {
       endpoints.MapControllers();
   })
   ;

app.Run();
