using System.Text;
using System.Text.Json.Nodes;

namespace SpecForge.McpServer;

public static class McpJsonRpcStdioTransport
{
    private const int MaxHeaderSize = 8192;

    public static async Task<JsonNode?> ReadMessageAsync(Stream input, CancellationToken cancellationToken = default)
    {
        var headerBytes = new List<byte>(256);
        var buffer = new byte[1];
        while (true)
        {
            var bytesRead = await input.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                return null;
            }

            headerBytes.Add(buffer[0]);
            if (headerBytes.Count > MaxHeaderSize)
            {
                throw new InvalidOperationException($"MCP message header exceeds maximum allowed size of {MaxHeaderSize} bytes.");
            }

            var headerString = Encoding.UTF8.GetString(headerBytes.ToArray());
            if (!headerString.EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                continue;
            }

            var contentLengthLine = headerString
                .Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
            if (contentLengthLine is null)
            {
                throw new InvalidOperationException("MCP message header is missing Content-Length.");
            }

            if (!int.TryParse(contentLengthLine.Split(':', 2)[1].Trim(), out var contentLength) || contentLength < 0)
            {
                throw new InvalidOperationException("MCP message Content-Length must be a non-negative integer.");
            }

            var contentBytes = new byte[contentLength];
            var totalRead = 0;
            while (totalRead < contentLength)
            {
                var read = await input.ReadAsync(contentBytes.AsMemory(totalRead, contentLength - totalRead), cancellationToken);
                if (read == 0)
                {
                    throw new InvalidOperationException("MCP message ended before the declared Content-Length was read.");
                }

                totalRead += read;
            }

            return JsonNode.Parse(contentBytes) ?? throw new InvalidOperationException("Invalid JSON payload.");
        }
    }

    public static async Task WriteMessageAsync(Stream output, string json, CancellationToken cancellationToken = default)
    {
        var contentBytes = Encoding.UTF8.GetBytes(json);
        var headerBytes = Encoding.ASCII.GetBytes($"Content-Length: {contentBytes.Length}\r\n\r\n");
        await output.WriteAsync(headerBytes, cancellationToken);
        await output.WriteAsync(contentBytes, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }
}
