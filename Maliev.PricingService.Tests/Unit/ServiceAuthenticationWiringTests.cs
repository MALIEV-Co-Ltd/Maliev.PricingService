using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.PricingService.Api.Controllers;
using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Infrastructure.Clients;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Maliev.PricingService.Tests.Unit;

/// <summary>
/// Verifies PricingService's centrally issued workload-authentication boundary.
/// </summary>
public sealed class ServiceAuthenticationWiringTests
{
    private const string ExpectedToken = "centrally-issued-pricing-token";

    /// <summary>
    /// Startup must opt into the AuthService exchange and remove every legacy local signer.
    /// </summary>
    [Fact]
    public void Program_RegistersPricingExchangeWithoutLegacySigner()
    {
        var source = ReadRepositoryFile("Maliev.PricingService.Api", "Program.cs");

        Assert.Contains("builder.AddAuthServiceTokenExchange(\"PricingService\");", source, StringComparison.Ordinal);
        Assert.Contains("builder.AddAuthServiceIAMClient();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddIAMServiceClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddAuthenticatedServiceClient", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// All four protected downstream clients must use one AuthService-issued bearer token.
    /// </summary>
    [Theory]
    [InlineData("IAMService")]
    [InlineData("MaterialService")]
    [InlineData("JobService")]
    [InlineData("CurrencyService")]
    public async Task ProtectedDownstreamClient_UsesAuthServiceBearer(string clientName)
    {
        var builder = CreateConfiguredBuilder();
        var filter = new TrackingPrimaryHandlerFilter();
        builder.Services.AddSingleton<IHttpMessageHandlerBuilderFilter>(filter);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddMemoryCache();

        builder.AddAuthServiceTokenExchange("PricingService");
        builder.Services.AddSingleton<IAuthServiceTokenProvider>(new StubTokenProvider());
        builder.AddAuthServiceIAMClient();
        RegisterProtectedPricingClients(builder);

        await using var provider = builder.Services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient(clientName);
        using var response = await client.GetAsync("/probe", CancellationToken.None);

        var capture = filter.GetCapture(clientName);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", ExpectedToken), capture.Authorization);
        Assert.True(filter.HasAuthServiceHandler(clientName));
    }

    /// <summary>
    /// Pricing uses the exact code-owned process identity and does not resolve legacy signing services.
    /// </summary>
    [Fact]
    public void PricingExchange_RegistersExactIdentityWithoutLegacySigningServices()
    {
        var builder = CreateConfiguredBuilder();
        builder.AddAuthServiceTokenExchange("PricingService");
        builder.AddAuthServiceIAMClient();

        using var provider = builder.Services.BuildServiceProvider();

        Assert.Equal("PricingService", provider.GetRequiredService<ServiceProcessIdentity>().ServiceName);
        Assert.Null(provider.GetService<IServiceAccountTokenProvider>());
        Assert.Null(provider.GetService<ServiceAccountAuthenticationHandler>());
    }

    /// <summary>
    /// Invalid workload credentials must stop the host instead of permitting anonymous fallback.
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("service-pricing-service", "short")]
    public async Task AuthServiceExchange_InvalidCredentials_FailsClosedAtHostStartup(
        string? clientId,
        string? clientSecret)
    {
        var builder = CreateConfiguredBuilder(clientId, clientSecret);
        builder.AddAuthServiceTokenExchange("PricingService");

        using var host = builder.Build();

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    /// <summary>
    /// CI must restore the exact ServiceDefaults release that owns central exchange behavior.
    /// </summary>
    [Fact]
    public void ServiceDefaultsDependency_PinsPublishedCentralExchangeVersion()
    {
        var source = ReadRepositoryFile("Directory.Build.props");

        Assert.Contains(
            "<ServiceDefaultsVersion Condition=\"'$(ServiceDefaultsVersion)' == ''\">1.0.89-alpha</ServiceDefaultsVersion>",
            source,
            StringComparison.Ordinal);

        foreach (var project in new[]
                 {
                     "Maliev.PricingService.Api/Maliev.PricingService.Api.csproj",
                     "Maliev.PricingService.Application/Maliev.PricingService.Application.csproj",
                     "Maliev.PricingService.Infrastructure/Maliev.PricingService.Infrastructure.csproj"
                 })
        {
            var projectSource = ReadRepositoryFile(project.Split('/'));
            Assert.Contains(
                "<PackageReference Include=\"Maliev.Aspire.ServiceDefaults\" Version=\"$(ServiceDefaultsVersion)\" />",
                projectSource,
                StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Docker package restore must receive the shared version properties first.
    /// </summary>
    [Fact]
    public void Dockerfile_CopiesSharedVersionPropertiesBeforePackageRestore()
    {
        var source = ReadRepositoryFile("Maliev.PricingService.Api", "Dockerfile");
        var propertiesCopy = source.IndexOf("COPY [\"Directory.Build.props\", \".\"]", StringComparison.Ordinal);
        var restore = source.IndexOf(
            "dotnet restore \"Maliev.PricingService.Api/Maliev.PricingService.Api.csproj\"",
            StringComparison.Ordinal);

        Assert.True(propertiesCopy >= 0, "Dockerfile must copy Directory.Build.props into the restore layer.");
        Assert.True(restore > propertiesCopy, "Directory.Build.props must be available before dotnet restore.");
        Assert.Contains("GITHUB_ACTIONS=true", source[..restore], StringComparison.Ordinal);
        Assert.Contains(
            "RUN GITHUB_ACTIONS=true dotnet build \"Maliev.PricingService.Api.csproj\"",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Material authorization failures must propagate to the pricing workflow.
    /// </summary>
    [Fact]
    public async Task MaterialClient_UnauthorizedResponse_FailsClosed()
    {
        var client = new MaterialServiceClient(
            CreateUnauthorizedClient(),
            new HttpContextAccessor(),
            NullLogger<MaterialServiceClient>.Instance);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetMaterialAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    /// <summary>
    /// Job authorization failures must propagate instead of becoming a zero queue depth.
    /// </summary>
    [Fact]
    public async Task JobClient_ForbiddenResponse_FailsClosed()
    {
        var client = new JobServiceClient(
            CreateUnauthorizedClient(HttpStatusCode.Forbidden),
            NullLogger<JobServiceClient>.Instance);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetQueueDepthByTechnologyAsync("FDM", CancellationToken.None));

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
    }

    /// <summary>
    /// Currency authorization failures must stop price conversion.
    /// </summary>
    [Fact]
    public async Task CurrencyClient_UnauthorizedResponse_FailsClosed()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new CurrencyServiceClient(
            CreateUnauthorizedClient(),
            new HttpContextAccessor(),
            NullLogger<CurrencyServiceClient>.Instance,
            cache);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetExchangeRateAsync("THB", "USD", CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    /// <summary>
    /// Existing pricing endpoints must retain explicit permission policies.
    /// </summary>
    [Fact]
    public void PricingEndpoints_RetainExplicitPermissionPolicies()
    {
        foreach (var controller in new[] { typeof(PricingController), typeof(PricingCatalogController) })
        {
            var endpoints = controller.GetMethods()
                .Where(method => method.DeclaringType == controller)
                .Where(method => method.GetCustomAttributes(typeof(HttpMethodAttribute), inherit: true).Length != 0)
                .ToArray();

            Assert.NotEmpty(endpoints);
            Assert.All(endpoints, endpoint =>
                Assert.NotNull(endpoint.GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true).SingleOrDefault()));
        }
    }

    private static void RegisterProtectedPricingClients(IHostApplicationBuilder builder)
    {
        builder.Services.AddHttpClient<IMaterialServiceClient, MaterialServiceClient>("MaterialService", client =>
            {
                client.BaseAddress = new Uri("https://material.test");
            })
            .AddAuthServiceAuthentication();
        builder.Services.AddHttpClient<IJobServiceClient, JobServiceClient>("JobService", client =>
            {
                client.BaseAddress = new Uri("https://job.test");
            })
            .AddAuthServiceAuthentication();
        builder.Services.AddHttpClient<ICurrencyServiceClient, CurrencyServiceClient>("CurrencyService", client =>
            {
                client.BaseAddress = new Uri("https://currency.test");
            })
            .AddAuthServiceAuthentication();
    }

    private static HostApplicationBuilder CreateConfiguredBuilder(
        string? clientId = "service-pricing-service",
        string? clientSecret = "pricing-test-secret-with-at-least-32-bytes")
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Testing"
        });

        using var rsa = RSA.Create(2048);
        builder.Configuration["ServiceAuthentication:ClientId"] = clientId;
        builder.Configuration["ServiceAuthentication:ClientSecret"] = clientSecret;
        builder.Configuration["Services:AuthService:BaseUrl"] = "https://auth.test";
        builder.Configuration["Services:IAMService:BaseUrl"] = "https://iam.test";
        builder.Configuration["Jwt:PublicKey"] = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(rsa.ExportSubjectPublicKeyInfoPem()));
        builder.Configuration["Jwt:Issuer"] = "https://api.maliev.com";
        builder.Configuration["Jwt:Audience"] = "https://api.maliev.com";

        return builder;
    }

    private static HttpClient CreateUnauthorizedClient(HttpStatusCode statusCode = HttpStatusCode.Unauthorized) =>
        new(new FixedResponseHandler(statusCode))
        {
            BaseAddress = new Uri("https://downstream.test")
        };

    private static string ReadRepositoryFile(params string[] segments)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            Path.Combine(segments)));

        Assert.True(File.Exists(path), $"Could not find source file: {path}");
        return File.ReadAllText(path);
    }

    private sealed class StubTokenProvider : IAuthServiceTokenProvider
    {
        public Task<string> GetTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ExpectedToken);
    }

    private sealed class FixedResponseHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }

    private sealed class AuthorizationCaptureHandler : HttpMessageHandler
    {
        public AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class TrackingPrimaryHandlerFilter : IHttpMessageHandlerBuilderFilter
    {
        private readonly Dictionary<string, AuthorizationCaptureHandler> _captures = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _authHandlers = new(StringComparer.Ordinal);

        public AuthorizationCaptureHandler GetCapture(string clientName) => _captures[clientName];

        public bool HasAuthServiceHandler(string clientName) => _authHandlers[clientName];

        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
        {
            next(builder);
            var clientName = builder.Name
                ?? throw new InvalidOperationException("Every HttpClientFactory handler must have a client name.");
            _authHandlers[clientName] = builder.AdditionalHandlers.Any(
                handler => handler is AuthServiceTokenExchangeHandler);

            for (var index = builder.AdditionalHandlers.Count - 1; index >= 0; index--)
            {
                if (builder.AdditionalHandlers[index].GetType().FullName?.Contains(
                        "ServiceDiscovery",
                        StringComparison.Ordinal) == true ||
                    builder.AdditionalHandlers[index].GetType().FullName?.Contains(
                        "ResolvingHttpDelegatingHandler",
                        StringComparison.Ordinal) == true)
                {
                    builder.AdditionalHandlers.RemoveAt(index);
                }
            }

            var capture = new AuthorizationCaptureHandler();
            _captures[clientName] = capture;
            builder.PrimaryHandler = capture;
        };
    }
}
