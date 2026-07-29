namespace Postech.Catalog.Api.Infrastructure.Search;

/// <summary>
/// Configuração de conexão com o Elasticsearch / OpenSearch.
/// Todos os valores sensíveis (Username, Password, ApiKey) vêm de Secret do
/// Kubernetes em runtime — nunca do appsettings.json versionado.
/// </summary>
public class ElasticsearchOptions
{
    public const string SectionName = "Elasticsearch";

    /// <summary>Endpoint completo, ex.: https://meu-deploy.es.eastus.azure.elastic-cloud.com:443</summary>
    public string Uri { get; set; } = "http://localhost:9200";

    public string IndexName { get; set; } = "games";

    /// <summary>Autenticação básica (Elastic Cloud usa o usuário "elastic").</summary>
    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary>Alternativa à autenticação básica. Se preenchido, tem prioridade.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Se true, reindexa todo o catálogo do Postgres no startup.</summary>
    public bool ReindexOnStartup { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 10;
}
