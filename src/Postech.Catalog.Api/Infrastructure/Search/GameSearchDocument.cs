namespace Postech.Catalog.Api.Infrastructure.Search;

/// <summary>
/// Projeção do agregado Game para o índice de busca. Deliberadamente separada da
/// entidade de domínio: o índice guarda só o que participa da busca ou da exibição
/// do resultado, e pode evoluir sem arrastar o modelo relacional junto.
/// Os nomes das propriedades casam com o mapping via camelCase.
/// </summary>
public sealed record GameSearchDocument(
    Guid Id,
    string Name,
    string Description,
    string Genre,
    decimal Price,
    DateTime ReleaseDate
);
