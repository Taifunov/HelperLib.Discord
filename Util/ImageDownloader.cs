namespace HelperLib.Discord.Util;

internal sealed class ImageDownloader : IDisposable
{
    internal const int MaxImageBytes = 8 * 1024 * 1024;
    private readonly HttpClient _client;

    internal ImageDownloader(HttpMessageHandler? handler = null)
    {
        _client = new HttpClient(handler ?? new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        })
        {
            Timeout = TimeSpan.FromSeconds(30),
            MaxResponseContentBufferSize = MaxImageBytes
        };
    }

    internal async Task<byte[]> DownloadAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort ||
            uri.UserInfo.Length != 0 ||
            uri.IdnHost is not ("cdn.discordapp.com" or "media.discordapp.net"))
        {
            throw new ArgumentException("Images must use HTTPS on cdn.discordapp.com or media.discordapp.net.", nameof(url));
        }

        // ResponseContentRead enforces HttpClient's buffer limit, including chunked responses.
        using var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseContentRead);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    public void Dispose() => _client.Dispose();
}
