using Postech.Catalog.Api.Application.DTOs;
using Postech.Catalog.Api.Domain.Entities;

namespace Postech.Catalog.Api.Infrastructure.Search;

/// <summary>
/// Abstração do motor de busca. A implementação atual fala com o Elasticsearch
/// via REST API, mas o contrato é agnóstico — trocar por OpenSearch ou por um
/// stub em teste não exige mudança em nenhum consumidor.
/// </summary>
public interface IGameSearchIndex
{
    /// <summary>Cria o índice com o mapping correto, caso ainda não exista. Idempotente.</summary>
    Task EnsureIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>Indexa (ou substitui) um jogo. Chamado a cada insert/update no banco principal.</summary>
    Task IndexGameAsync(Game game, CancellationToken cancellationToken = default);

    /// <summary>Indexação em lote via Bulk API. Usado no backfill de startup.</summary>
    Task<int> BulkIndexAsync(IEnumerable<Game> games, CancellationToken cancellationToken = default);

    /// <summary>Remove o documento do índice.</summary>
    Task DeleteGameAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>Busca com tolerância a erro de digitação, ordenada por relevância (_score).</summary>
    Task<GameSearchResponse> SearchAsync(string query, int size = 20, CancellationToken cancellationToken = default);
}
