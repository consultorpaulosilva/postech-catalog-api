namespace Postech.Catalog.Api.Application.DTOs;

/// <summary>Um resultado da busca, já com a pontuação de relevância do Elasticsearch.</summary>
public sealed record GameSearchHit(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Genre,
    DateTime ReleaseDate,
    double Score
);

/// <summary>
/// Envelope da busca. TookMs vem do próprio Elasticsearch e é ótimo para
/// demonstrar no pitch a diferença de latência contra o LIKE no Postgres.
/// </summary>
public sealed record GameSearchResponse(
    string Query,
    long Total,
    long TookMs,
    IReadOnlyList<GameSearchHit> Results
);
