using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.AiPlatform;
using Atlas.Infrastructure.Services.AiPlatform.Parsers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.DependencyInjection;

public static class AiPlatformServiceRegistration
{
    public static IServiceCollection AddAiPlatformInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiPlatformOptions>(configuration.GetSection("AiPlatform"));
        services.AddHttpClient("AiPlatform", client => client.Timeout = TimeSpan.FromSeconds(120));

        services.AddSingleton<ILlmProvider>(sp =>
            CreateProvider(sp, "openai", "https://api.openai.com"));
        services.AddSingleton<ILlmProvider>(sp =>
            CreateProvider(sp, "deepseek", "https://api.deepseek.com"));
        services.AddSingleton<ILlmProvider>(sp =>
            CreateProvider(sp, "ollama", "http://localhost:11434"));

        services.AddSingleton<IEmbeddingProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AiPlatformOptions>>().Value;
            if (options.Providers.TryGetValue("openai", out var option) && option.SupportsEmbedding)
            {
                return CreateProvider(sp, "openai", "https://api.openai.com");
            }

            throw new InvalidOperationException("No embedding provider is configured. Please configure AiPlatform:Providers.");
        });

        services.AddScoped<ModelConfigRepository>();
        services.AddScoped<AgentRepository>();
        services.AddScoped<AgentKnowledgeLinkRepository>();
        services.AddScoped<IModelConfigCommandService, ModelConfigCommandService>();
        services.AddScoped<IModelConfigQueryService, ModelConfigQueryService>();
        services.AddScoped<IAgentCommandService, AgentCommandService>();
        services.AddScoped<IAgentQueryService, AgentQueryService>();

        services.AddScoped<ILlmProviderFactory, LlmProviderFactory>();

        services.AddSingleton<IVectorStore, SqliteVectorStore>();

        services.AddSingleton<TxtDocumentParser>();
        services.AddSingleton<PdfDocumentParser>();
        services.AddSingleton<DocxDocumentParser>();
        services.AddSingleton<MarkdownDocumentParser>();
        services.AddSingleton<SpreadsheetDocumentParser>();
        services.AddSingleton<JsonDocumentParser>();

        services.AddSingleton<IDocumentParser>(sp => sp.GetRequiredService<TxtDocumentParser>());
        services.AddSingleton<IDocumentParser>(sp => sp.GetRequiredService<PdfDocumentParser>());
        services.AddSingleton<IDocumentParser>(sp => sp.GetRequiredService<DocxDocumentParser>());
        services.AddSingleton<IDocumentParser>(sp => sp.GetRequiredService<MarkdownDocumentParser>());
        services.AddSingleton<IDocumentParser>(sp => sp.GetRequiredService<SpreadsheetDocumentParser>());
        services.AddSingleton<IDocumentParser>(sp => sp.GetRequiredService<JsonDocumentParser>());
        services.AddSingleton<DocumentParserComposite>();
        services.AddSingleton<IDocumentParser>(sp => sp.GetRequiredService<DocumentParserComposite>());

        services.AddSingleton<IChunkingService, FixedSizeChunkingService>();

        return services;
    }

    private static OpenAiCompatibleProvider CreateProvider(
        IServiceProvider serviceProvider,
        string providerName,
        string defaultBaseUrl)
    {
        var logger = serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OpenAiCompatibleProvider>>();
        var options = serviceProvider.GetRequiredService<IOptions<AiPlatformOptions>>().Value;
        var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        options.Providers.TryGetValue(providerName, out var provider);
        provider ??= new AiProviderOption();
        var merged = new AiProviderOption
        {
            ApiKey = provider.ApiKey,
            BaseUrl = string.IsNullOrWhiteSpace(provider.BaseUrl) ? defaultBaseUrl : provider.BaseUrl,
            DefaultModel = provider.DefaultModel,
            SupportsEmbedding = provider.SupportsEmbedding
        };

        var client = factory.CreateClient("AiPlatform");
        return new OpenAiCompatibleProvider(providerName, merged, client, logger);
    }
}
