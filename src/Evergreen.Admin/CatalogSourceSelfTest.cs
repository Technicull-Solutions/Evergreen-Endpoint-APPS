using System.Net;
using Evergreen.Configuration;

namespace Evergreen.Admin;

/// <summary>Offline published-binary coverage of the same profile and retrieval path as the UI.</summary>
internal static class CatalogSourceSelfTest
{
    public static async Task RunAsync(string directory)
    {
        var profile = new CatalogSourceProfile
        {
            Name = "Self-test",
            ServerUrl = "https://gitea.example.test",
            Owner = "Example",
            Repository = "endpoint-config",
        };
        string path = Path.Combine(directory, "catalog-source.json");
        await AdminWorkspace.ExportCatalogSourceAsync(path, profile);
        var restored = CatalogSourceProfiles.Parse(await AdminWorkspace.OpenAsync(path));
        if (restored != profile) throw new IOException("Catalog source profile did not round-trip.");
        using var client = new CatalogRepositoryClient(new FixtureHandler());
        var result = await client.FetchAsync(restored, "self-test-token");
        if (result.LibraryCount != 1 || result.AppCount != 0 || result.Sha256.Length != 64)
            throw new IOException("Catalog source retrieval did not validate.");
        await AdminWorkspace.ExportAsync(Path.Combine(directory, "downloaded-catalog.json"), result.Json, true);
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsoluteUri != "https://gitea.example.test/api/v1/repos/Example/endpoint-config/raw/software-catalog.json?ref=main"
                || request.Headers.Authorization?.Scheme != "token" || request.Headers.Authorization.Parameter != "self-test-token")
                throw new IOException("Unexpected catalog source request.");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AdminWorkspace.EmptyLibrary),
            });
        }
    }
}
