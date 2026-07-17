namespace Maliev.PricingService.Tests.Unit;

/// <summary>
/// Protects the release-integrity contracts implemented by the Docker and GitHub Actions files.
/// </summary>
public sealed class ContainerWorkflowContractTests
{
    /// <summary>
    /// Docker restore must select published shared packages itself instead of depending on a workflow rewrite.
    /// </summary>
    [Fact]
    public void DockerAndDevelopWorkflow_UseOneUnmodifiedRestorePath()
    {
        var dockerfile = ReadRepositoryFile("Maliev.PricingService.Api", "Dockerfile");
        var workflow = ReadRepositoryFile(".github", "workflows", "ci-develop.yml");

        Assert.Contains("GITHUB_ACTIONS=true", dockerfile, StringComparison.Ordinal);
        Assert.Contains(
            "dotnet restore \"Maliev.PricingService.Api/Maliev.PricingService.Api.csproj\"",
            dockerfile,
            StringComparison.Ordinal);
        Assert.DoesNotContain("sed -i", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("Switch to PackageReference", workflow, StringComparison.Ordinal);
    }

    /// <summary>
    /// The distroless-style ASP.NET runtime image cannot execute a curl-based Docker health check.
    /// </summary>
    [Fact]
    public void FinalImage_ReliesOnKubernetesProbesInsteadOfUnavailableCurl()
    {
        var dockerfile = ReadRepositoryFile("Maliev.PricingService.Api", "Dockerfile");

        Assert.DoesNotContain("HEALTHCHECK", dockerfile, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("curl", dockerfile, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Pull-request validation must be bounded and least privilege.
    /// </summary>
    [Fact]
    public void PullRequestWorkflow_UsesLeastPrivilegeAndConcurrencyCancellation()
    {
        var workflow = ReadRepositoryFile(".github", "workflows", "pr-validation.yml");

        Assert.Contains("permissions:\n  contents: read", workflow, StringComparison.Ordinal);
        Assert.Contains("concurrency:", workflow, StringComparison.Ordinal);
        Assert.Contains("cancel-in-progress: true", workflow, StringComparison.Ordinal);
    }

    /// <summary>
    /// Pull-request validation must execute the production image and prove its live process is non-root.
    /// </summary>
    [Fact]
    public void PullRequestWorkflow_RunsBoundedNonRootLivenessSmoke()
    {
        var workflow = ReadRepositoryFile(".github", "workflows", "pr-validation.yml");

        Assert.Contains("timeout 90", workflow, StringComparison.Ordinal);
        Assert.Contains("docker run --detach", workflow, StringComparison.Ordinal);
        Assert.Contains("docker top \"$CONTAINER_NAME\" -eo uid", workflow, StringComparison.Ordinal);
        Assert.Contains("test \"$runtime_uid\" != \"0\"", workflow, StringComparison.Ordinal);
        Assert.Contains("http://127.0.0.1:8080/pricing/liveness", workflow, StringComparison.Ordinal);
        Assert.Contains("docker inspect --format '{{.State.Running}}'", workflow, StringComparison.Ordinal);
    }

    /// <summary>
    /// Runtime-smoke resources must be cleaned up even when startup or liveness validation fails.
    /// </summary>
    [Fact]
    public void PullRequestWorkflow_AlwaysCleansUpRuntimeSmokeResources()
    {
        var workflow = ReadRepositoryFile(".github", "workflows", "pr-validation.yml");

        Assert.Contains("trap cleanup EXIT", workflow, StringComparison.Ordinal);
        Assert.Contains("docker logs \"$CONTAINER_NAME\"", workflow, StringComparison.Ordinal);
        Assert.Contains("docker rm --force \"$CONTAINER_NAME\"", workflow, StringComparison.Ordinal);
        Assert.Contains("docker rm --force \"$POSTGRES_CONTAINER_NAME\"", workflow, StringComparison.Ordinal);
        Assert.Contains("docker network rm \"$SMOKE_NETWORK\"", workflow, StringComparison.Ordinal);
    }

    /// <summary>
    /// The reusable build gate must not introduce a deprecated Node 20 cache runtime.
    /// </summary>
    [Fact]
    public void BuildWorkflow_UsesPinnedNode24CacheAction()
    {
        var workflow = ReadRepositoryFile(".github", "workflows", "_build-and-test.yml");

        Assert.Contains(
            "actions/cache@55cc8345863c7cc4c66a329aec7e433d2d1c52a9 # v6.1.0",
            workflow,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "actions/cache@0057852bfaa89a56745cba8c7296529d2fc39830",
            workflow,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Development publication must use short-lived GCP credentials and immutable image evidence.
    /// </summary>
    [Fact]
    public void DevelopWorkflow_UsesWifAndPublishesVerifiableImageEvidence()
    {
        var workflow = ReadRepositoryFile(".github", "workflows", "ci-develop.yml");

        Assert.Contains("permissions:\n  contents: read", workflow, StringComparison.Ordinal);
        Assert.Contains("id-token: write", workflow, StringComparison.Ordinal);
        Assert.Contains("workload_identity_provider:", workflow, StringComparison.Ordinal);
        Assert.Contains("service_account:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("credentials_json:", workflow, StringComparison.Ordinal);
        Assert.Contains("provenance: mode=max", workflow, StringComparison.Ordinal);
        Assert.Contains("sbom: true", workflow, StringComparison.Ordinal);
        Assert.Contains("steps.build.outputs.digest", workflow, StringComparison.Ordinal);
        Assert.Contains("severity: HIGH,CRITICAL", workflow, StringComparison.Ordinal);
    }

    /// <summary>
    /// GitOps output is evidence only while every PricingService application remains disabled.
    /// </summary>
    [Fact]
    public void DevelopWorkflow_CreatesDraftDoNotMergeEvidenceWithoutAutoSyncClaims()
    {
        var workflow = ReadRepositoryFile(".github", "workflows", "ci-develop.yml");

        Assert.Contains("--draft", workflow, StringComparison.Ordinal);
        Assert.Contains("--title \"[DO NOT MERGE]", workflow, StringComparison.Ordinal);
        Assert.Contains("_disabled_apps", workflow, StringComparison.Ordinal);
        Assert.Contains("changed=\"$(git diff --name-only)\"", workflow, StringComparison.Ordinal);
        Assert.Contains("test \"$changed\" = \"$expected\"", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("automatically sync", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Once merged", workflow, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Release workflows must promote the verified digest and keep every GitOps change review-only.
    /// </summary>
    [Theory]
    [InlineData("ci-staging.yml")]
    [InlineData("ci-main.yml")]
    public void ReleaseWorkflow_PromotesWithoutRebuildAndCreatesDraftEvidence(string workflowName)
    {
        var workflow = ReadRepositoryFile(".github", "workflows", workflowName);

        Assert.Contains("docker buildx imagetools create", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("docker build ", workflow, StringComparison.Ordinal);
        Assert.Contains("--draft", workflow, StringComparison.Ordinal);
        Assert.Contains("--title \"[DO NOT MERGE]", workflow, StringComparison.Ordinal);
        Assert.Contains("_disabled_apps", workflow, StringComparison.Ordinal);
        Assert.Contains("changed=\"$(git diff --name-only)\"", workflow, StringComparison.Ordinal);
        Assert.Contains("test \"$changed\" = \"$expected\"", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("automatically sync", workflow, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Image workflows install a pinned Kustomize binary without the deprecated Node 20 action.
    /// </summary>
    [Theory]
    [InlineData("ci-develop.yml")]
    [InlineData("ci-staging.yml")]
    [InlineData("ci-main.yml")]
    public void ImageWorkflow_UsesChecksumVerifiedKustomizeBinary(string workflowName)
    {
        var workflow = ReadRepositoryFile(".github", "workflows", workflowName);

        Assert.DoesNotContain("imranismail/setup-kustomize", workflow, StringComparison.Ordinal);
        Assert.Contains("KUSTOMIZE_VERSION: v5.8.1", workflow, StringComparison.Ordinal);
        Assert.Contains(
            "KUSTOMIZE_SHA256: 029a7f0f4e1932c52a0476cf02a0fd855c0bb85694b82c338fc648dcb53a819d",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains("sha256sum --check", workflow, StringComparison.Ordinal);
    }

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
        return File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
