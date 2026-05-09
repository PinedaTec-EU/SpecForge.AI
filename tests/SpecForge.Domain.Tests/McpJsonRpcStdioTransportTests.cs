using System.Text;
using SpecForge.McpServer;

namespace SpecForge.Domain.Tests;

public sealed class McpJsonRpcStdioTransportTests
{
    [Fact]
    public async Task ReadMessageAsync_ReadsValidContentLengthFrame()
    {
        await using var stream = ToStream("Content-Length: 17\r\n\r\n{\"jsonrpc\":\"2.0\"}");

        var payload = await McpJsonRpcStdioTransport.ReadMessageAsync(stream);

        Assert.Equal("2.0", payload?["jsonrpc"]?.GetValue<string>());
    }

    [Fact]
    public async Task ReadMessageAsync_ReturnsNullAtEndOfStreamBeforeHeader()
    {
        await using var stream = ToStream(string.Empty);

        var payload = await McpJsonRpcStdioTransport.ReadMessageAsync(stream);

        Assert.Null(payload);
    }

    [Fact]
    public async Task ReadMessageAsync_RejectsMissingContentLength()
    {
        await using var stream = ToStream("X-Test: value\r\n\r\n{}");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            McpJsonRpcStdioTransport.ReadMessageAsync(stream));

        Assert.Contains("missing Content-Length", exception.Message);
    }

    [Fact]
    public async Task ReadMessageAsync_RejectsInvalidContentLength()
    {
        await using var stream = ToStream("Content-Length: no\r\n\r\n{}");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            McpJsonRpcStdioTransport.ReadMessageAsync(stream));

        Assert.Contains("non-negative integer", exception.Message);
    }

    [Fact]
    public async Task ReadMessageAsync_RejectsTruncatedPayload()
    {
        await using var stream = ToStream("Content-Length: 10\r\n\r\n{}");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            McpJsonRpcStdioTransport.ReadMessageAsync(stream));

        Assert.Contains("ended before the declared Content-Length", exception.Message);
    }

    [Fact]
    public async Task WriteMessageAsync_WritesContentLengthFrame()
    {
        await using var stream = new MemoryStream();

        await McpJsonRpcStdioTransport.WriteMessageAsync(stream, "{\"ok\":true}");

        Assert.Equal("Content-Length: 11\r\n\r\n{\"ok\":true}", Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static MemoryStream ToStream(string value) =>
        new(Encoding.UTF8.GetBytes(value));
}
