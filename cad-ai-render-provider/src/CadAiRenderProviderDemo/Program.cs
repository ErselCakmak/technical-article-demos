var scene = new SceneCapture(
    ImageBytes: new byte[] { 1, 2, 3 },
    MimeType: "image/png",
    AspectRatio: "16:9");

var request = new RenderRequest(
    scene,
    UserPrompt: "Create a warm showroom-style preview.",
    History: Array.Empty<ConversationMessage>());

IAiRenderProvider provider = AiRenderProviderFactory.Create(RenderProvider.OpenAI);
RenderResult result = await provider.GenerateAsync(request, CancellationToken.None);

Console.WriteLine(result.TextResponse);

public enum RenderProvider
{
    OpenAI,
    Gemini,
    MyArchitectAI,
}

public sealed record SceneCapture(byte[] ImageBytes, string MimeType, string AspectRatio);

public sealed record ConversationMessage(string Role, string Content);

public sealed record RenderRequest(
    SceneCapture Scene,
    string UserPrompt,
    IReadOnlyList<ConversationMessage> History);

public sealed record RenderResult(byte[] ImageBytes, string MimeType, string? TextResponse);

public interface IAiRenderProvider
{
    Task<RenderResult> GenerateAsync(RenderRequest request, CancellationToken cancellationToken);
}

public static class AiRenderProviderFactory
{
    public static IAiRenderProvider Create(RenderProvider provider) => provider switch
    {
        RenderProvider.OpenAI => new FakeRenderProvider("OpenAI"),
        RenderProvider.Gemini => new FakeRenderProvider("Gemini"),
        RenderProvider.MyArchitectAI => new FakeRenderProvider("MyArchitectAI"),
        _ => throw new NotSupportedException($"Unsupported provider: {provider}"),
    };
}

public sealed class FakeRenderProvider : IAiRenderProvider
{
    private readonly string _name;

    public FakeRenderProvider(string name) => _name = name;

    public Task<RenderResult> GenerateAsync(RenderRequest request, CancellationToken cancellationToken)
    {
        string prompt = RenderPromptComposer.BuildPrompt(request, hasPreviousRender: request.History.Count > 0);

        return Task.FromResult(new RenderResult(
            ImageBytes: request.Scene.ImageBytes,
            MimeType: request.Scene.MimeType,
            TextResponse: $"{_name} request prepared with prompt: {prompt}"));
    }
}

public static class RenderPromptComposer
{
    public static string BuildPrompt(RenderRequest request, bool hasPreviousRender)
    {
        var lines = new List<string>
        {
            "Preserve geometry, proportions, camera framing, and layout from the CAD reference.",
            "Improve materials, lighting, shadows, and presentation quality.",
            $"Aspect ratio: {request.Scene.AspectRatio}",
            $"User request: {request.UserPrompt}",
        };

        if (hasPreviousRender)
        {
            lines.Add("Use the previous render as a refinement reference, not as permission to change the design.");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
