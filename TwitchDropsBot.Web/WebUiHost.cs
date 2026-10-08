using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Extensions.Logging;
using TwitchDropsBot.Core;
using TwitchDropsBot.Core.Platform.Shared.Services;
using TwitchDropsBot.Core.Platform.Shared.Settings;
using TwitchDropsBot.Web.Accounts;
using TwitchDropsBot.Web.Api;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace TwitchDropsBot.Web;

/// <summary>
/// The optional web UI, hosted inside the console app. Off unless WEBUI_PORT is set.
/// </summary>
public sealed class WebUiHost
{
    private readonly WebUiOptions _options;
    private readonly IAccountSource _accounts;
    private readonly ILogger _logger;

    private WebUiHost(WebUiOptions options, IAccountSource accounts, ILogger logger)
    {
        _options = options;
        _accounts = accounts;
        _logger = logger;
    }

    public bool IsDemo => _options.Demo;

    /// <summary>
    /// Reads the WEBUI_* variables. Call before the bots start, so their log lines reach the Activity view.
    /// Returns null when the UI is off or misconfigured (the reason is logged).
    /// </summary>
    public static WebUiHost? Create(IServiceProvider botServices)
    {
        var logger = botServices.GetRequiredService<ILoggerFactory>().CreateLogger("WebUI");
        var dataDirectory = CampaignQueueService.DataDirectory();

        var options = WebUiOptions.FromEnvironment(dataDirectory, out var error);
        if (options is null)
        {
            if (error is not null)
            {
                logger.LogError("Web UI not started: {Reason}", error);
            }

            return null;
        }

        var loggerFactory = botServices.GetRequiredService<ILoggerFactory>();
        IAccountSource accounts = options.Demo
            ? new DemoAccountSource(loggerFactory)
            : new LiveAccountSource(
                botServices.GetRequiredService<BotRegistry>(),
                botServices.GetRequiredService<CampaignQueueService>(),
                botServices.GetRequiredService<IOptionsMonitor<BotSettings>>(),
                new CampaignStatusStore(dataDirectory, loggerFactory.CreateLogger<CampaignStatusStore>()));

        return new WebUiHost(options, accounts, logger);
    }

    /// <summary>Runs until cancelled. Failures are logged, never thrown, so the bots keep running.</summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var app = Build();
            _logger.LogInformation("Web UI listening on port {Port}", _options.Port);
            if (_options.Demo)
            {
                _logger.LogInformation("Web UI is serving demo data");
            }
            else if (!_options.RequiresLogin)
            {
                _logger.LogWarning("Web UI runs without a login (WEBUI_AUTH=none)");
            }

            await app.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Web UI stopped: {Message}", e.Message);
        }
    }

    private WebApplication Build()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(WebUiHost).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger);
        builder.Logging.AddFilter<SerilogLoggerProvider>("Microsoft", LogLevel.Warning);
        builder.Logging.AddFilter<SerilogLoggerProvider>("System", LogLevel.Warning);
        // Keys sit unencrypted beside config.json, which already holds the tokens; nothing to act on
        builder.Logging.AddFilter<SerilogLoggerProvider>("Microsoft.AspNetCore.DataProtection", LogLevel.Error);

        // The aspnet base image sets ASPNETCORE_HTTP_PORTS; the port here is WEBUI_PORT
        builder.WebHost.UseSetting(WebHostDefaults.HttpPortsKey, string.Empty);
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.ListenAnyIP(_options.Port);
            kestrel.AddServerHeader = false;
        });

        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        builder.Services.ConfigureHttpJsonOptions(x =>
        {
            x.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            x.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        var version = VersionHelper.GetUIVersion(Assembly.GetEntryAssembly() ?? typeof(WebUiHost).Assembly);
        var backend = new WebBackend(_accounts, version, _options.Demo);

        builder.Services.AddSingleton(_options);
        builder.Services.AddSingleton(json);
        builder.Services.AddSingleton(backend);
        builder.Services.AddSingleton<LoginThrottle>();
        builder.Services.AddSingleton<StateBroadcaster>();
        builder.Services.AddHostedService(x => x.GetRequiredService<StateBroadcaster>());
        builder.Services.AddHostedService<StatusScanner>();

        // Keys for the login cookie live beside config.json, so a restart doesn't log the phone out
        builder.Services.AddDataProtection()
            .SetApplicationName("TwitchDropsBot.Web")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(_options.DataDirectory, "webui-keys")));

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(cookie =>
            {
                cookie.Cookie.Name = "tdb_session";
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Strict;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                cookie.ExpireTimeSpan = TimeSpan.FromDays(30);
                cookie.SlidingExpiration = true;
                cookie.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                cookie.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        builder.Services.AddAuthorization(authorization =>
            authorization.AddPolicy(ApiEndpoints.Policy, policy =>
            {
                if (_options.RequiresLogin)
                {
                    policy.RequireAuthenticatedUser();
                }
                else
                {
                    policy.RequireAssertion(_ => true);
                }
            }));

        if (_options.TrustedProxies.Count > 0)
        {
            builder.Services.Configure<ForwardedHeadersOptions>(forwarded =>
            {
                forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                forwarded.KnownNetworks.Clear();
                forwarded.KnownProxies.Clear();
                foreach (var proxy in _options.TrustedProxies)
                {
                    forwarded.KnownProxies.Add(proxy);
                }
            });
        }

        var app = builder.Build();

        if (_options.TrustedProxies.Count > 0)
        {
            app.UseForwardedHeaders();
        }

        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["Content-Security-Policy"] =
                "default-src 'self'; img-src 'self' data: https:; style-src 'self'; script-src 'self'; " +
                "connect-src 'self'; manifest-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";

            // Changes must come from the page itself
            if (context.Request.Path.StartsWithSegments("/api") &&
                !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) &&
                context.Request.Headers[ApiEndpoints.RequestHeader] != "1")
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            await next();
        });

        var files = new ManifestEmbeddedFileProvider(typeof(WebUiHost).Assembly, "wwwroot");
        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".webmanifest"] = "application/manifest+json";

        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            ContentTypeProvider = contentTypes,
            OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache"
        });

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapWebUiApi();

        return app;
    }
}
