using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Evergreen.Configuration;

namespace Evergreen.Admin;

public sealed record CatalogRepositoryDownload(string Json, string Sha256, int LibraryCount, int AppCount);

/// <summary>Explicit read-only Gitea retrieval. No cookies, redirects, persisted tokens, or policy writes.</summary>
public sealed class CatalogRepositoryClient : IDisposable
{
    private readonly HttpClient _client;

    public CatalogRepositoryClient() : this(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseDefaultCredentials = false,
    })
    { }

    // Explicit transport injection for deterministic, offline boundary tests.
    public CatalogRepositoryClient(HttpMessageHandler handler) => _client = new HttpClient(handler);

    public async Task<CatalogRepositoryDownload> FetchAsync(CatalogSourceProfile profile, string? token,
        CancellationToken cancellationToken = default)
    {
        Uri address = profile.GetDownloadUri();
        if (token is { Length: > 4096 } || token?.Any(value => char.IsWhiteSpace(value) || char.IsControl(value) || value > 126) == true)
            throw new IOException("Enter a valid repository access token without whitespace or control characters.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        if (!string.IsNullOrEmpty(token)) request.Headers.Authorization = new AuthenticationHeaderValue("token", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        try
        {
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if ((int)response.StatusCode is >= 300 and < 400)
                throw new IOException("Gitea redirected the request. Check the server base URL; credentials are never forwarded to redirects.");
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new IOException("Gitea denied access. Check the token's repository read permission and repository membership.");
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new IOException("Catalog not found or not accessible. Check the repository, revision, file path, and token access.");
            if (!response.IsSuccessStatusCode)
                throw new IOException($"Gitea returned HTTP {(int)response.StatusCode}. The current draft has not been replaced.");
            if (response.Content.Headers.ContentType?.MediaType?.Equals("text/html", StringComparison.OrdinalIgnoreCase) == true)
                throw new IOException("Gitea returned an HTML page instead of catalog JSON. Check API access and authentication.");
            if (response.Content.Headers.ContentLength > SoftwareLibrary.MaximumDocumentBytes)
                throw new IOException("Catalog exceeds the 4 MiB limit.");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            byte[] chunk = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(chunk, timeout.Token)) > 0)
            {
                if (buffer.Length + count > SoftwareLibrary.MaximumDocumentBytes)
                    throw new IOException("Catalog exceeds the 4 MiB limit.");
                buffer.Write(chunk, 0, count);
            }
            byte[] data = buffer.ToArray();
            string json = Encoding.UTF8.GetString(data).TrimStart('\uFEFF');
            var libraries = SoftwareLibrary.Parse(json);
            return new CatalogRepositoryDownload(AdminWorkspace.FormatLibrary(json), Convert.ToHexString(SHA256.HashData(data)),
                libraries.Length, libraries.Sum(library => library.Packages.Length));
        }
        catch (HttpRequestException)
        {
            // Never expose server response bodies, URLs, authentication headers, or proxy details.
            throw new IOException("Could not connect to Gitea. Check connectivity and the server's trusted HTTPS certificate.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IOException("The catalog request timed out after 30 seconds. The current draft has not been replaced.");
        }
        catch (JsonException)
        {
            throw new IOException("The downloaded file is not a supported software catalog. The current draft has not been replaced.");
        }
    }

    public void Dispose() => _client.Dispose();
}
