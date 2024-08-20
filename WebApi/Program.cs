var random = new Random();

int[] errorStatusCodes =
[
    StatusCodes.Status200OK,
    StatusCodes.Status408RequestTimeout,
    StatusCodes.Status429TooManyRequests,
    StatusCodes.Status500InternalServerError,
    StatusCodes.Status504GatewayTimeout
];

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/test/{waitTimeMs}", async (int waitTimeMs) =>
{
    await Task.Delay(TimeSpan.FromMilliseconds(waitTimeMs)).ConfigureAwait(false);
    var statusCode = errorStatusCodes[random.Next(errorStatusCodes.Length)];
    return Results.StatusCode(statusCode);
});

app.Run();
