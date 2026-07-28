using MassTransit;
using Microsoft.Net.Http.Headers;
using Postech.Catalog.Api.Extensions;
using Postech.Catalog.Api.Infrastructure.MassTransit;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

#region [Logging Configuration]

// Bootstrap logger
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

// Main logger
builder.Host.UseSerilog((context, services, options) =>
{
    options
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.WithCorrelationId(headerName: "X-Correlation-Id", addValueIfHeaderAbsence: true);
});

#endregion

#region [Builder Extensions]

builder.Services.AddApplicationServices();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSearchEngine(builder.Configuration);

builder.Services.AddMassTransitServices(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddOpenApiWithAuth();

#endregion

var app = builder.Build();

// Sondas do Kubernetes.
//  live  -> o processo esta vivo; se falhar, o pod e reiniciado.
//  ready -> o pod pode receber trafego; enquanto retorna 503 o Service
//           nao o inclui no balanceamento, o que evita 502 durante o
//           rolling update enquanto a aplicacao ainda esta subindo.
app.MapHealthChecks("/health/live").AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();

#region [App Extensions]

await app.ApplyMigrationsAsync();

app.ConfigurePipeline();

#endregion

try
{
    Log.Information("Starting Users API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

