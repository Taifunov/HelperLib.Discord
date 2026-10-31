using System.Net;

namespace HelperLib.Discord.Tests.Mocks;

/// <summary>Answers every request from <paramref name="createResponse"/> and counts the requests that reach it.</summary>
internal sealed class FakeHttpMessageHandler(Func<HttpResponseMessage> createResponse) : HttpMessageHandler
{
    public int Requests { get; private set; }

    public static FakeHttpMessageHandler Returning(byte[] content) =>
        new(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Like the real handler, a cancelled request must not reach the network.
        cancellationToken.ThrowIfCancellationRequested();
        Requests++;
        return Task.FromResult(createResponse());
    }
}
