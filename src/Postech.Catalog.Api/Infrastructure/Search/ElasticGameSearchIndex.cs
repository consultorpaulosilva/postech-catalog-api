using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Postech.Catalog.Api.Application.DTOs;
using Postech.Catalog.Api.Domain.Entities;

namespace Postech.Catalog.Api.Infrastructure.Search;

/// <summary>
/// Integração com o Elasticsearch pela REST API.
///
/// Optamos por falar HTTP direto em vez de usar o pacote Elastic.Clients.Elasticsearch
/// porque a versão major/minor daquele client é acoplada à versão do servidor. Falando
/// REST, o mesmo código atende Elastic Cloud 8.x, 9.x e Amazon OpenSearch sem recompilar.
/// </summary>
public class ElasticGameSearchIndex : IGameSearchIndex
{
    /// <summary>Serializer dos documentos: camelCase, casando com o mapping do índice.</summary>
    private static readonly JsonSerializerOptions DocumentJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Serializer das queries: SEM naming policy. A DSL do Elasticsearch usa snake_case
    /// (multi_match, prefix_length) e qualquer conversão automática quebraria a query.
    /// </summary>
    private static readonly JsonSerializerOptions QueryJson = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string IndexDefinition = """
    {
      "settings": {
        "number_of_shards": 1,
        "number_of_replicas": 0,
        "analysis": {
          "analyzer": {
            "game_analyzer": {
              "type": "custom",
              "tokenizer": "standard",
              "filter": ["lowercase", "asciifolding"]
            }
          }
        }
      },
      "mappings": {
        "properties": {
          "id":          { "type": "keyword" },
          "name":        { "type": "text", "analyzer": "game_analyzer",
                           "fields": { "keyword": { "type": "keyword" } } },
          "description": { "type": "text", "analyzer": "game_analyzer" },
          "genre":       { "type": "text", "analyzer": "game_analyzer",
                           "fields": { "keyword": { "type": "keyword" } } },
          "price":       { "type": "double" },
          "releaseDate": { "type": "date" }
        }
      }
    }
    """;

    private readonly HttpClient _http;
    private readonly ILogger<ElasticGameSearchIndex> _logger;
    private readonly string _index;

    public ElasticGameSearchIndex(
        HttpClient http,
        IOptions<ElasticsearchOptions> options,
        ILogger<ElasticGameSearchIndex> logger)
    {
        _http = http;
        _logger = logger;
        _index = options.Value.IndexName;
    }

    public async Task EnsureIndexAsync(CancellationToken cancellationToken = default)
    {
        using var probe = new HttpRequestMessage(HttpMethod.Head, _index);
        var exists = await _http.SendAsync(probe, cancellationToken);

        if (exists.StatusCode == HttpStatusCode.OK)
        {
            _logger.LogInformation("Elasticsearch: índice '{Index}' já existe", _index);
            return;
        }

        using var content = new StringContent(IndexDefinition, Encoding.UTF8, "application/json");
        var created = await _http.PutAsync(_index, content, cancellationToken);

        if (!created.IsSuccessStatusCode)
        {
            var error = await created.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Elasticsearch: falha ao criar índice '{Index}'. {Status} {Error}",
                _index, created.StatusCode, error);
            return;
        }

        _logger.LogInformation("Elasticsearch: índice '{Index}' criado com analyzer asciifolding", _index);
    }

    public async Task IndexGameAsync(Game game, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = ToDocument(game);
            var json = JsonSerializer.Serialize(document, DocumentJson);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            // refresh=wait_for garante que o documento fica visível para a próxima busca.
            // Em produção com alto volume trocaríamos por refresh=false, mas aqui a
            // consistência imediata vale mais do que o throughput.
            var response = await _http.PutAsync(
                $"{_index}/_doc/{game.Id}?refresh=wait_for", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Elasticsearch: falha ao indexar jogo {GameId}. {Status} {Error}",
                    game.Id, response.StatusCode, error);
                return;
            }

            _logger.LogInformation("Elasticsearch: jogo {GameId} ({Name}) indexado", game.Id, game.Name);
        }
        catch (Exception ex)
        {
            // A busca é um recurso auxiliar: se o Elasticsearch cair, o cadastro no
            // banco principal não pode falhar junto.
            _logger.LogError(ex, "Elasticsearch indisponível ao indexar o jogo {GameId}", game.Id);
        }
    }

    public async Task<int> BulkIndexAsync(IEnumerable<Game> games, CancellationToken cancellationToken = default)
    {
        var list = games.ToList();
        if (list.Count == 0) return 0;

        var ndjson = new StringBuilder();
        foreach (var game in list)
        {
            var action = JsonSerializer.Serialize(
                new { index = new { _id = game.Id.ToString() } }, QueryJson);

            ndjson.Append(action).Append('\n');
            ndjson.Append(JsonSerializer.Serialize(ToDocument(game), DocumentJson)).Append('\n');
        }

        try
        {
            using var content = new StringContent(ndjson.ToString(), Encoding.UTF8, "application/x-ndjson");
            var response = await _http.PostAsync($"{_index}/_bulk?refresh=wait_for", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Elasticsearch: bulk falhou. {Status} {Error}", response.StatusCode, error);
                return 0;
            }

            _logger.LogInformation("Elasticsearch: {Count} jogos indexados em lote", list.Count);
            return list.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Elasticsearch indisponível durante o bulk index");
            return 0;
        }
    }

    public async Task DeleteGameAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.DeleteAsync(
                $"{_index}/_doc/{gameId}?refresh=wait_for", cancellationToken);

            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Elasticsearch: jogo {GameId} removido do índice", gameId);
                return;
            }

            _logger.LogError("Elasticsearch: falha ao remover jogo {GameId}. {Status}",
                gameId, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Elasticsearch indisponível ao remover o jogo {GameId}", gameId);
        }
    }

    public async Task<GameSearchResponse> SearchAsync(
        string query, int size = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new GameSearchResponse(query, 0, 0, Array.Empty<GameSearchHit>());

        // Duas cláusulas complementares:
        //  1. multi_match com fuzziness AUTO  -> tolera erro de digitação ("wicther" acha "The Witcher")
        //  2. match_phrase_prefix no nome     -> busca incremental enquanto o usuário digita
        // O bool/should soma as pontuações, e o Elasticsearch já devolve ordenado por _score.
        var body = new
        {
            size,
            query = new
            {
                @bool = new
                {
                    should = new object[]
                    {
                        new
                        {
                            multi_match = new
                            {
                                query,
                                fields = new[] { "name^4", "genre^2", "description" },
                                fuzziness = "AUTO",
                                prefix_length = 1,
                                max_expansions = 50,
                                @operator = "or"
                            }
                        },
                        new
                        {
                            match_phrase_prefix = new
                            {
                                name = new { query, boost = 3.0 }
                            }
                        }
                    },
                    minimum_should_match = 1
                }
            }
        };

        var json = JsonSerializer.Serialize(body, QueryJson);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync($"{_index}/_search", content, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Elasticsearch: busca falhou. {Status} {Error}", response.StatusCode, payload);
            throw new InvalidOperationException($"Falha na busca: {response.StatusCode}");
        }

        return Parse(query, payload);
    }

    private GameSearchResponse Parse(string query, string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        var took = root.TryGetProperty("took", out var tookElement) ? tookElement.GetInt64() : 0;
        var hitsRoot = root.GetProperty("hits");

        long total = 0;
        if (hitsRoot.TryGetProperty("total", out var totalElement))
        {
            total = totalElement.ValueKind == JsonValueKind.Object
                ? totalElement.GetProperty("value").GetInt64()
                : totalElement.GetInt64();
        }

        var results = new List<GameSearchHit>();

        foreach (var hit in hitsRoot.GetProperty("hits").EnumerateArray())
        {
            var source = hit.GetProperty("_source");
            var score = hit.TryGetProperty("_score", out var scoreElement)
                        && scoreElement.ValueKind == JsonValueKind.Number
                ? scoreElement.GetDouble()
                : 0d;

            results.Add(new GameSearchHit(
                Id: source.GetProperty("id").GetGuid(),
                Name: GetString(source, "name"),
                Description: GetString(source, "description"),
                Price: source.TryGetProperty("price", out var price) ? price.GetDecimal() : 0m,
                Genre: GetString(source, "genre"),
                ReleaseDate: source.TryGetProperty("releaseDate", out var date)
                    ? date.GetDateTime()
                    : default,
                Score: score
            ));
        }

        _logger.LogInformation(
            "Elasticsearch: busca '{Query}' retornou {Total} resultado(s) em {Took}ms",
            query, total, took);

        return new GameSearchResponse(query, total, took, results);
    }

    private static string GetString(JsonElement source, string property) =>
        source.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;

    private static GameSearchDocument ToDocument(Game game) => new(
        game.Id,
        game.Name,
        game.Description,
        game.Genre,
        game.Price,
        DateTime.SpecifyKind(game.ReleaseDate, DateTimeKind.Utc)
    );
}
