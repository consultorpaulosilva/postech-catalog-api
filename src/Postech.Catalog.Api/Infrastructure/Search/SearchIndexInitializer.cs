using Microsoft.Extensions.Options;
using Postech.Catalog.Api.Domain.Entities;
using Postech.Catalog.Api.Infrastructure.Repositories;

namespace Postech.Catalog.Api.Infrastructure.Search;

/// <summary>
/// No startup: garante que o índice existe e reindexa o catálogo vindo do Postgres.
///
/// Isso resolve o problema clássico de motor de busca desacoplado — um pod novo, um
/// cluster recriado ou um índice apagado voltam sozinhos ao estado consistente, sem
/// intervenção manual. Falha aqui nunca derruba a aplicação: sem Elasticsearch a API
/// continua servindo o catálogo normalmente, só a busca avançada fica degradada.
/// </summary>
public class SearchIndexInitializer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ElasticsearchOptions _options;
    private readonly ILogger<SearchIndexInitializer> _logger;

    public SearchIndexInitializer(
        IServiceScopeFactory scopeFactory,
        IOptions<ElasticsearchOptions> options,
        ILogger<SearchIndexInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var searchIndex = scope.ServiceProvider.GetRequiredService<IGameSearchIndex>();

            await searchIndex.EnsureIndexAsync(stoppingToken);

            if (!_options.ReindexOnStartup)
            {
                _logger.LogInformation("Backfill do índice desabilitado por configuração");
                return;
            }

            var repository = scope.ServiceProvider.GetRequiredService<IGameRepository>();
            var games = (await repository.GetAllAsync(stoppingToken))
                .Where(game => game is not null)
                .Select(game => game!)
                .ToList();

            if (games.Count == 0)
            {
                _logger.LogInformation("Nenhum jogo no banco principal para indexar");
                return;
            }

            var indexed = await searchIndex.BulkIndexAsync(games, stoppingToken);
            _logger.LogInformation("Backfill concluído: {Indexed} jogo(s) sincronizados no Elasticsearch", indexed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao inicializar o índice de busca. A API segue no ar sem busca avançada.");
        }
    }
}
