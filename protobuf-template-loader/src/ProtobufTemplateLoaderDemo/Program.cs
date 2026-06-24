using ProtoBuf;

var store = new FileTemplateStore(Path.Combine(AppContext.BaseDirectory, "templates"));
var settings = new AppSettings { LastSelectedTemplate = "starter" };

var template = new TemplateFile
{
    Name = "starter",
    Version = 1,
    Entities = { new EntityDto("line", 0, 0, 100, 0) },
    SnapPoints = { new SnapPointDto("origin", 0, 0) },
    Parameters = { new TemplateParameterDto("material", "oak") },
};

store.Save(template.Name, TemplateSerializer.ToBytes(template));

TemplateFile? startupTemplate = StartupTemplateLoader.Load(settings, store);
Console.WriteLine(startupTemplate?.Name ?? "No template selected.");

[ProtoContract]
public sealed class TemplateFile
{
    [ProtoMember(1)] public string Name { get; set; } = string.Empty;
    [ProtoMember(2)] public int Version { get; set; }
    [ProtoMember(3)] public List<EntityDto> Entities { get; } = new();
    [ProtoMember(4)] public List<SnapPointDto> SnapPoints { get; } = new();
    [ProtoMember(5)] public List<TemplateParameterDto> Parameters { get; } = new();
}

[ProtoContract]
public sealed record EntityDto(
    [property: ProtoMember(1)] string Type,
    [property: ProtoMember(2)] double X1,
    [property: ProtoMember(3)] double Y1,
    [property: ProtoMember(4)] double X2,
    [property: ProtoMember(5)] double Y2);

[ProtoContract]
public sealed record SnapPointDto(
    [property: ProtoMember(1)] string Name,
    [property: ProtoMember(2)] double X,
    [property: ProtoMember(3)] double Y);

[ProtoContract]
public sealed record TemplateParameterDto(
    [property: ProtoMember(1)] string Key,
    [property: ProtoMember(2)] string Value);

public static class TemplateSerializer
{
    public static byte[] ToBytes(TemplateFile template)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, template);
        return stream.ToArray();
    }

    public static TemplateFile FromBytes(byte[] payload)
    {
        using var stream = new MemoryStream(payload, writable: false);
        return Serializer.Deserialize<TemplateFile>(stream);
    }
}

public interface ITemplateStore
{
    byte[]? TryLoad(string key);
    void Save(string key, byte[] payload);
}

public sealed class FileTemplateStore : ITemplateStore
{
    private readonly string _directory;

    public FileTemplateStore(string directory) => _directory = directory;

    public byte[]? TryLoad(string key)
    {
        string path = GetPath(key);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public void Save(string key, byte[] payload)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(GetPath(key), payload);
    }

    private string GetPath(string key) => Path.Combine(_directory, $"{key}.template");
}

public sealed class AppSettings
{
    public string? LastSelectedTemplate { get; init; }
}

public static class StartupTemplateLoader
{
    public static TemplateFile? Load(AppSettings settings, ITemplateStore store)
    {
        if (string.IsNullOrWhiteSpace(settings.LastSelectedTemplate))
        {
            return null;
        }

        byte[]? payload = store.TryLoad(settings.LastSelectedTemplate);
        return payload is null ? null : TemplateSerializer.FromBytes(payload);
    }
}
