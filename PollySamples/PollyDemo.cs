using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Polly.Telemetry;
using System.Net;

internal static class PollyDemo
{
    public static async Task ExecuteAsync()
    {
        const string requestKey = "request";

        var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.FromMilliseconds(1000),

                ShouldHandle = args =>
                {
                    if (args.Outcome.Result == null)
                    {
                        return PredicateResult.False();
                    }

                    return args.Outcome.Result.StatusCode switch
                    {
                        HttpStatusCode.RequestTimeout => PredicateResult.True(),
                        HttpStatusCode.TooManyRequests => PredicateResult.True(),
                        HttpStatusCode.GatewayTimeout => PredicateResult.True(),
                        _ => PredicateResult.False(),
                    };
                },

                OnRetry = args =>
                {
                    var request = args.Context.Properties.GetValue<HttpRequestMessage>(new(requestKey), null!);

                    Console.WriteLine(
                        $"Request {request.RequestUri} failed, StatusCode={args.Outcome.Result!.StatusCode}. " +
                        $"Retry #{args.AttemptNumber}. Waiting {args.RetryDelay} and try again.");

                    return ValueTask.CompletedTask;
                }
            })
            .ConfigureTelemetry(new TelemetryOptions
            {
                LoggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Debug);
                    builder.AddConsole();
                }),
                TelemetryListeners =
                {
                    new ActualExecutionDurationReporter()
                },
                MeteringEnrichers =
                {
                    new CustomMeteringEnricher()
                }
            })
            .Build();

        int waitTimeMs = 1000;
        using var cancellationTokenSource = new CancellationTokenSource();
        var resilienceContext = ResilienceContextPool.Shared.Get(cancellationTokenSource.Token);

        var outcome = await pipeline
            .ExecuteOutcomeAsync(
                static async (context, waitTimeMs) =>
                {
                    using var client = new HttpClient() { BaseAddress = new Uri("https://localhost:7222") };
                    var request = new HttpRequestMessage(HttpMethod.Get, $"/test/{waitTimeMs}");
                    var response = await client.SendAsync(request, context.CancellationToken).ConfigureAwait(false);

                    context.Properties.Set(new(requestKey), request);

                    return Outcome.FromResult(response);
                },
                resilienceContext,
                state: waitTimeMs)
            .ConfigureAwait(false);

        if (outcome.Result != null)
        {
            Console.WriteLine($"Request result: {outcome.Result.StatusCode}");
        }

        if (outcome.Exception != null)
        {
            Console.WriteLine($"Exception: {outcome.Exception.Message}");
        }

        ResilienceContextPool.Shared.Return(resilienceContext);
    }

    internal sealed class ActualExecutionDurationReporter : TelemetryListener
    {
        public override void Write<TResult, TArgs>(in TelemetryEventArguments<TResult, TArgs> args)
        {
            if (args.Arguments is ExecutionAttemptArguments attemptArgs)
            {
                Console.WriteLine($"Attempt #{attemptArgs.AttemptNumber}, execution duration is {attemptArgs.Duration}");
            }
        }
    }

    internal sealed class CustomMeteringEnricher : MeteringEnricher
    {
        public override void Enrich<TResult, TArgs>(in EnrichmentContext<TResult, TArgs> context)
        {
            context.Tags.Add(new("custom-tag", "custom-value"));
        }
    }
}
