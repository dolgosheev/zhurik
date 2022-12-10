using Fas.Catalog.Extensions.SerilogEnricher;

using Serilog;
using Serilog.OpenTelemetry;

namespace Personal.Bot;

public static class Startup
{
    // Config Host & Services
    internal static WebApplicationBuilder ConfigureHost(WebApplicationBuilder builder)
    {
        // Logger config
        builder.Host.UseSerilog((context, lc) => lc
            .Enrich.WithCaller()
            .Enrich.WithResource(
                ("server", Environment.MachineName),
                ("app", AppDomain.CurrentDomain.FriendlyName))
            .WriteTo.Console()
            .ReadFrom.Configuration(context.Configuration)
        );

        // Kestrel config
        builder.WebHost.ConfigureKestrel((_, opt) =>
        {
            opt.Limits.MinRequestBodyDataRate = null;
            opt.AllowAlternateSchemes = true;
        });

        builder.Services.AddSingleton<TelegramBot>();

        return builder;
    }

    internal static WebApplication ConfigApp(WebApplication app, CancellationToken token)
    {
        using (var serviceScope = app.Services.GetService<IServiceScopeFactory>()?.CreateScope())
        {
            if (serviceScope != null)
            {
                /* Prometheus */
                var bot = serviceScope.ServiceProvider.GetRequiredService<TelegramBot>();
                bot.Start(token);
            }
        }

        app.UseRouting();
        return app;
    }
}