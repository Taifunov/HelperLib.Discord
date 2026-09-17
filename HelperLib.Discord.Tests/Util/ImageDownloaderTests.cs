using System.Net;
using HelperLib.Discord.Util;

namespace HelperLib.Discord.Tests.Util;

public class ImageDownloaderTests
{
    [Theory]
    [InlineData("http://cdn.discordapp.com/image.png")]
    [InlineData("https://127.0.0.1/image.png")]
    [InlineData("https://[::1]/image.png")]
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("https://cdn.discordapp.com.attacker.example/image.png")]
    [InlineData("https://cdn.discordapp.com@attacker.example/image.png")]
    [InlineData("https://user@cdn.discordapp.com/image.png")]
    [InlineData("https://cdn.discordapp.com:8443/image.png")]
    [InlineData("file:///etc/passwd")]
    [InlineData("/relative.png")]
    public async Task Rejects_untrusted_urls_before_sending(string url)
    {
        using var handler = new FakeHandler(() => throw new InvalidOperationException("Network must not be used"));
        using var downloader = new ImageDownloader(handler);
        await Assert.ThrowsAsync<ArgumentException>(() => downloader.DownloadAsync(url));
        Assert.Equal(0, handler.Requests);
    }

    [Theory]
    [InlineData("cdn.discordapp.com")]
    [InlineData("media.discordapp.net")]
    public async Task Downloads_from_allowed_hosts(string host)
    {
        using var handler = new FakeHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3])
        });
        using var downloader = new ImageDownloader(handler);
        Assert.Equal(new byte[] { 1, 2, 3 }, await downloader.DownloadAsync($"https://{host}/image.png"));
        Assert.Equal(1, handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Redirect)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Rejects_redirects_and_unsuccessful_responses(HttpStatusCode status)
    {
        using var handler = new FakeHandler(() =>
        {
            var response = new HttpResponseMessage(status);
            response.Headers.Location = new Uri("http://127.0.0.1/private");
            return response;
        });
        using var downloader = new ImageDownloader(handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => downloader.DownloadAsync("https://cdn.discordapp.com/image.png"));
        Assert.Equal(1, handler.Requests);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Rejects_oversized_bodies_even_without_content_length(bool knownLength)
    {
        using var handler = new FakeHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = knownLength
                ? new ByteArrayContent(new byte[ImageDownloader.MaxImageBytes + 1])
                : new UnknownLengthContent()
        });
        using var downloader = new ImageDownloader(handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => downloader.DownloadAsync("https://cdn.discordapp.com/image.png"));
    }

    private sealed class FakeHandler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        internal int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(response());
        }
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var chunk = new byte[8192];
            for (var written = 0; written <= ImageDownloader.MaxImageBytes; written += chunk.Length)
                await stream.WriteAsync(chunk);
        }
    }
}
