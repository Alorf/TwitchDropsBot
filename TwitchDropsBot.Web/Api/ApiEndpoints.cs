using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace TwitchDropsBot.Web.Api;

public static class ApiEndpoints
{
    public const string Policy = "web-ui";

    // Sent by the page on every change; a cross-site form or image request can't add it
    public const string RequestHeader = "X-TDB-Request";

    public static void MapWebUiApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/healthz", () => Results.Text("ok"));

        var api = app.MapGroup("/api");

        api.MapGet("/session", (HttpContext context, WebUiOptions options, WebBackend backend) =>
            Results.Ok(new SessionDto(
                !options.RequiresLogin || context.User.Identity?.IsAuthenticated == true,
                options.RequiresLogin,
                backend.Version,
                backend.IsDemo)));

        api.MapPost("/login", async (HttpContext context, LoginRequest request, WebUiOptions options,
            LoginThrottle throttle) =>
        {
            if (!options.RequiresLogin)
            {
                return Results.NoContent();
            }

            var client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (throttle.IsBlocked(client))
            {
                return Results.Json(new ProblemDto("Too many attempts. Try again in a few minutes."),
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            if (!throttle.Check(client, request.Password))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(400));
                return Results.Json(new ProblemDto("Wrong password."), statusCode: StatusCodes.Status401Unauthorized);
            }

            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "owner") },
                CookieAuthenticationDefaults.AuthenticationScheme);
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true });

            return Results.NoContent();
        });

        api.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        var secured = api.MapGroup("").RequireAuthorization(Policy);

        secured.MapGet("/state", (StateBroadcaster broadcaster) =>
            Results.Content(broadcaster.Current, "application/json"));

        secured.MapGet("/events", async (HttpContext context, StateBroadcaster broadcaster) =>
        {
            var response = context.Response;
            response.Headers.ContentType = "text/event-stream";
            response.Headers.CacheControl = "no-cache";
            response.Headers["X-Accel-Buffering"] = "no";

            var aborted = context.RequestAborted;
            var (id, reader) = broadcaster.Subscribe();
            try
            {
                await response.WriteAsync("retry: 5000\n\n", aborted);
                await response.Body.FlushAsync(aborted);

                while (!aborted.IsCancellationRequested)
                {
                    using var heartbeat = CancellationTokenSource.CreateLinkedTokenSource(aborted);
                    heartbeat.CancelAfter(TimeSpan.FromSeconds(20));

                    string? state = null;
                    try
                    {
                        if (await reader.WaitToReadAsync(heartbeat.Token))
                        {
                            reader.TryRead(out state);
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch (OperationCanceledException) when (!aborted.IsCancellationRequested)
                    {
                    }

                    var serverTime = DateTime.UtcNow.ToString("O");
                    var message = state is null
                        ? $": ping\n\n"
                        : $"event: state\ndata: {{\"serverTime\":\"{serverTime}\",\"state\":{state}}}\n\n";

                    await response.WriteAsync(message, aborted);
                    await response.Body.FlushAsync(aborted);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                broadcaster.Unsubscribe(id);
            }
        });

        var account = secured.MapGroup("/accounts/{accountId}");

        account.MapGet("/campaigns", (string accountId, WebBackend backend) =>
            backend.GetCampaigns(accountId) is { } list
                ? Results.Ok(list)
                : Results.NotFound(new ProblemDto("No such account.")));

        account.MapPost("/campaigns/refresh", async (string accountId, WebBackend backend,
                CancellationToken cancellationToken) =>
            ToResult(await backend.RefreshCampaignsAsync(accountId, cancellationToken)));

        account.MapGet("/campaigns/{campaignId}", async (string accountId, string campaignId, bool? recheck,
                WebBackend backend, CancellationToken cancellationToken) =>
            ToResult(await backend.GetCampaignAsync(accountId, campaignId, recheck == true, cancellationToken)));

        account.MapPost("/queue", (string accountId, QueueRequest request, WebBackend backend) =>
            ToResult(backend.AddToQueue(accountId, request)));

        account.MapPut("/queue", (string accountId, ReorderRequest request, WebBackend backend) =>
            ToResult(backend.ReorderQueue(accountId, request)));

        account.MapDelete("/queue/{campaignId}", (string accountId, string campaignId, WebBackend backend) =>
            ToResult(backend.RemoveFromQueue(accountId, campaignId)));

        account.MapPost("/switch", (string accountId, WebBackend backend) =>
            ToResult(backend.SwitchNow(accountId)));

        account.MapGet("/logs", (string accountId, long? after, WebBackend backend) =>
            backend.GetLogs(accountId, after ?? 0) is { } page
                ? Results.Ok(page)
                : Results.NotFound(new ProblemDto("No such account.")));
    }

    private static IResult ToResult(BackendResult result) =>
        result.Status == StatusCodes.Status200OK
            ? Results.Ok(result.Body)
            : Results.Json(result.Body, statusCode: result.Status);
}
