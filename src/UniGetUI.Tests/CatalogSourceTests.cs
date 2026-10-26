using System.Net;
using System.Text;
using System.Text.Json;
using Evergreen.Admin;
using Evergreen.Configuration;

namespace UniGetUI.Tests;

public sealed class CatalogSourceTests
{
    private static CatalogSourceProfile Profile => new()
    {
        Name = "Test environment",
        ServerUrl = "https://gitea.example.test/forge",
        Owner = "Example",
        Repository = "endpoint-config",
        Revision = "pilot/reviewed",
        FilePath = "catalogs/software catalog.json",
    };

    [Fact]
    public void SourceProfileRoundTripsAndBuildsAnEscapedRawApiAddress()
    {
        var profile = CatalogSourceProfiles.Parse(CatalogSourceProfiles.Serialize(Profile));
        Assert.Equal(Profile, profile);
        Assert.Equal("https://gitea.example.test/forge/api/v1/repos/Example/endpoint-config/raw/catalogs/software%20catalog.json?ref=pilot%2Freviewed",
            profile.GetDownloadUri().AbsoluteUri);
    }

    [Theory]
    [InlineData("http://gitea.example.test")]
    [InlineData("https://user:secret@gitea.example.test")]
    [InlineData("https://gitea.example.test?token=secret")]
    [InlineData("https://gitea.example.test/#fragment")]
    [InlineData("file:///catalog.json")]
    [InlineData("https://gitea.example.test/a/../b")]
    public void UnsafeServerAddressesAreRejected(string server) =>
        Assert.Throws<JsonException>(() => (Profile with { ServerUrl = server }).GetDownloadUri());

    [Theory]
    [InlineData("../catalog.json")]
    [InlineData("catalogs/../../catalog.json")]
    [InlineData("/catalog.json")]
    [InlineData("catalogs//catalog.json")]
    [InlineData("catalogs\\catalog.json")]
    [InlineData("%2e%2e/catalog.json")]
    public void TraversalAndAmbiguousFilePathsAreRejected(string path) =>
        Assert.Throws<JsonException>(() => (Profile with { FilePath = path }).Validate());

    [Fact]
    public void CredentialsUnknownVersionsAndOversizedProfilesAreRejected()
    {
        string json = CatalogSourceProfiles.Serialize(Profile);
        Assert.Throws<JsonException>(() => CatalogSourceProfiles.Parse(json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99")));
        Assert.Throws<JsonException>(() => CatalogSourceProfiles.Parse(json.Insert(1, "\"token\":\"do-not-save\",")));
        Assert.Throws<JsonException>(() => CatalogSourceProfiles.Parse(json.Replace("\"schemaVersion\": 1,", "")));
        Assert.Throws<JsonException>(() => CatalogSourceProfiles.Parse(json.Replace("\"provider\": \"gitea\",", "")));
        Assert.Throws<JsonException>(() => CatalogSourceProfiles.Parse(new string(' ', CatalogSourceProfiles.MaximumDocumentBytes + 1)));
        Assert.Throws<JsonException>(() => CatalogSourceProfiles.Parse("{\"schemaVersion\":1}"));
    }

    [Fact]
    public async Task FetchUsesOnlyAnAuthorizationHeaderAndValidatesTheEmployeeContract()
    {
        using var client = new CatalogRepositoryClient(new StubHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(Profile.GetDownloadUri(), request.RequestUri);
            Assert.Equal("token", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-only-token", request.Headers.Authorization?.Parameter);
            Assert.DoesNotContain("test-only-token", request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(AdminWorkspace.EmptyLibrary) });
        }));
        var result = await client.FetchAsync(Profile, "test-only-token");
        Assert.Equal(1, result.LibraryCount);
        Assert.Equal(0, result.AppCount);
        Assert.Equal(64, result.Sha256.Length);
        Assert.Single(SoftwareLibrary.Parse(result.Json));
    }

    [Theory]
    [InlineData(302)]
    [InlineData(307)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(500)]
    public async Task FailureResponsesDoNotExposeBodiesOrFollowRedirects(int status)
    {
        int calls = 0;
        using var client = new CatalogRepositoryClient(new StubHandler((_, _) =>
        {
            calls++;
            var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("sensitive-response-body") };
            response.Headers.Location = new Uri("https://other.example.test/steal");
            return Task.FromResult(response);
        }));
        var error = await Assert.ThrowsAsync<IOException>(() => client.FetchAsync(Profile, "test-only-token"));
        Assert.DoesNotContain("sensitive-response-body", error.Message);
        Assert.DoesNotContain("test-only-token", error.Message);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("text/html", "<html>Sign in</html>")]
    [InlineData("application/json", "{\"version\":99,\"catalogs\":[]}")]
    [InlineData("application/json", "invalid-json")]
    public async Task LoginPagesAndInvalidCatalogsAreRejected(string contentType, string body)
    {
        using var client = new CatalogRepositoryClient(new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, contentType) })));
        await Assert.ThrowsAsync<IOException>(() => client.FetchAsync(Profile, null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OversizedBodiesAreRejectedWithAndWithoutContentLength(bool knownLength)
    {
        using var stream = new NonSeekableStream(new byte[SoftwareLibrary.MaximumDocumentBytes + 1]);
        using var client = new CatalogRepositoryClient(new StubHandler((_, _) =>
        {
            var content = new StreamContent(stream);
            if (knownLength) content.Headers.ContentLength = SoftwareLibrary.MaximumDocumentBytes + 1;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }));
        var error = await Assert.ThrowsAsync<IOException>(() => client.FetchAsync(Profile, null));
        Assert.Contains("4 MiB", error.Message);
    }

    [Fact]
    public async Task InvalidProfilesAndTokensAreRejectedBeforeNetworkAccess()
    {
        using var client = new CatalogRepositoryClient(new StubHandler((_, _) => throw new InvalidOperationException("Unexpected network request")));
        await Assert.ThrowsAsync<JsonException>(() => client.FetchAsync(Profile with { ServerUrl = "http://example.test" }, null));
        await Assert.ThrowsAsync<IOException>(() => client.FetchAsync(Profile, "secret\r\nheader"));
        await Assert.ThrowsAsync<IOException>(() => client.FetchAsync(Profile, "secret\0"));
    }

    [Fact]
    public async Task CancellationDoesNotReturnAPartialCatalog()
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new CatalogRepositoryClient(new StubHandler(async (_, token) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.FetchAsync(Profile, null, cancellation.Token));
    }

    [Fact]
    public async Task ProfileExportRemainsAnUnprivilegedWorkspaceOperation()
    {
        await Assert.ThrowsAsync<IOException>(() => AdminWorkspace.ExportCatalogSourceAsync(SoftwareLibrary.FilePath, Profile));
        string directory = Path.Combine(Path.GetTempPath(), "evergreen-source-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "catalog-source.json");
            await AdminWorkspace.ExportCatalogSourceAsync(path, Profile);
            Assert.Equal(Profile, CatalogSourceProfiles.Parse(await AdminWorkspace.OpenAsync(path)));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handle(request, cancellationToken);
    }

    private sealed class NonSeekableStream(byte[] content) : MemoryStream(content)
    {
        public override bool CanSeek => false;
    }
}
