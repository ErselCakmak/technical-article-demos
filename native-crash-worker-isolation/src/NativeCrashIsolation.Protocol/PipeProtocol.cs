using System.Buffers.Binary;
using System.Text;

namespace NativeCrashIsolation.Protocol;

public static class PipeProtocol
{
    private const int Magic = 0x4E435749; // NCWI
    private const int Version = 1;
    private const int MaxTokenBytes = 256;
    private const int MaxMessageBytes = 16 * 1024;
    private const int MaxPayloadBytes = 1024 * 1024;

    public static async Task WriteRequestAsync(
        Stream stream,
        WorkerRequest request,
        CancellationToken cancellationToken)
    {
        await WriteInt32Async(stream, Magic, cancellationToken);
        await WriteInt32Async(stream, Version, cancellationToken);
        await WriteStringAsync(stream, request.Token, MaxTokenBytes, cancellationToken);
        await stream.WriteAsync(new[] { (byte)request.Operation }, cancellationToken);
        await WriteBytesAsync(stream, request.Payload, MaxPayloadBytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<WorkerRequest> ReadRequestAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        await ValidateHeaderAsync(stream, cancellationToken);
        string token = await ReadStringAsync(stream, MaxTokenBytes, cancellationToken);

        byte[] operationBuffer = new byte[1];
        await stream.ReadExactlyAsync(operationBuffer, cancellationToken);
        var operation = (WorkerOperation)operationBuffer[0];

        if (!Enum.IsDefined(operation))
        {
            throw new InvalidDataException($"Unknown operation: {operationBuffer[0]}");
        }

        byte[] payload = await ReadBytesAsync(stream, MaxPayloadBytes, cancellationToken);
        return new WorkerRequest(token, operation, payload);
    }

    public static async Task WriteResponseAsync(
        Stream stream,
        WorkerResponse response,
        CancellationToken cancellationToken)
    {
        await WriteInt32Async(stream, Magic, cancellationToken);
        await WriteInt32Async(stream, Version, cancellationToken);
        await stream.WriteAsync(new[] { response.Success ? (byte)1 : (byte)0 }, cancellationToken);
        await WriteStringAsync(stream, response.Message, MaxMessageBytes, cancellationToken);
        await WriteBytesAsync(stream, response.Payload, MaxPayloadBytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<WorkerResponse> ReadResponseAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        await ValidateHeaderAsync(stream, cancellationToken);

        byte[] successBuffer = new byte[1];
        await stream.ReadExactlyAsync(successBuffer, cancellationToken);
        if (successBuffer[0] > 1)
        {
            throw new InvalidDataException("Invalid success flag.");
        }

        string message = await ReadStringAsync(stream, MaxMessageBytes, cancellationToken);
        byte[] payload = await ReadBytesAsync(stream, MaxPayloadBytes, cancellationToken);
        return new WorkerResponse(successBuffer[0] == 1, message, payload);
    }

    private static async Task ValidateHeaderAsync(Stream stream, CancellationToken cancellationToken)
    {
        int magic = await ReadInt32Async(stream, cancellationToken);
        int version = await ReadInt32Async(stream, cancellationToken);

        if (magic != Magic)
        {
            throw new InvalidDataException("Invalid protocol marker.");
        }

        if (version != Version)
        {
            throw new InvalidDataException($"Unsupported protocol version: {version}");
        }
    }

    private static async Task WriteStringAsync(
        Stream stream,
        string value,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        await WriteBytesAsync(stream, bytes, maximumBytes, cancellationToken);
    }

    private static async Task<string> ReadStringAsync(
        Stream stream,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        byte[] bytes = await ReadBytesAsync(stream, maximumBytes, cancellationToken);
        return Encoding.UTF8.GetString(bytes);
    }

    private static async Task WriteBytesAsync(
        Stream stream,
        byte[] value,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (value.Length > maximumBytes)
        {
            throw new InvalidDataException($"Payload exceeds the {maximumBytes}-byte limit.");
        }

        await WriteInt32Async(stream, value.Length, cancellationToken);
        await stream.WriteAsync(value, cancellationToken);
    }

    private static async Task<byte[]> ReadBytesAsync(
        Stream stream,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        int length = await ReadInt32Async(stream, cancellationToken);
        if (length < 0 || length > maximumBytes)
        {
            throw new InvalidDataException($"Invalid payload length: {length}");
        }

        byte[] value = new byte[length];
        await stream.ReadExactlyAsync(value, cancellationToken);
        return value;
    }

    private static async Task WriteInt32Async(
        Stream stream,
        int value,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        await stream.WriteAsync(buffer, cancellationToken);
    }

    private static async Task<int> ReadInt32Async(
        Stream stream,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(buffer, cancellationToken);
        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }
}
