using System.Text.Json;
using System.Text.Json.Serialization;

namespace GraphAwareClipboard;

public readonly record struct CanvasPoint(double X, double Y)
{
    public static CanvasPoint operator +(CanvasPoint point, CanvasPoint offset) =>
        new(point.X + offset.X, point.Y + offset.Y);
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(CardItem), "card")]
[JsonDerivedType(typeof(LinkItem), "link")]
public abstract class CanvasItem
{
    public Guid Id { get; set; }
    public CanvasPoint Position { get; set; }

    public virtual void Translate(CanvasPoint offset) => Position += offset;
}

public sealed class CardItem : CanvasItem
{
    public string Title { get; set; } = string.Empty;
}

public sealed class LinkItem : CanvasItem
{
    public Guid TargetItemId { get; set; }
    public CanvasPoint LeaderEnd { get; set; }

    public override void Translate(CanvasPoint offset)
    {
        base.Translate(offset);
        LeaderEnd += offset;
    }
}

public sealed record ClipboardEnvelope(
    int Version,
    Guid SourceDocumentId,
    List<CanvasItem> Items);

public static class GraphClipboard
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static string Copy(Guid sourceDocumentId, IReadOnlyCollection<CanvasItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var envelope = new ClipboardEnvelope(
            CurrentVersion,
            sourceDocumentId,
            items.ToList());

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    public static IReadOnlyList<CanvasItem> Paste(
        string json,
        IEnumerable<Guid> destinationItemIds,
        CanvasPoint offset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentNullException.ThrowIfNull(destinationItemIds);

        ClipboardEnvelope envelope = JsonSerializer.Deserialize<ClipboardEnvelope>(json, JsonOptions)
            ?? throw new JsonException("Clipboard payload is empty.");

        Validate(envelope);

        HashSet<Guid> destinationIds = destinationItemIds.ToHashSet();
        Dictionary<Guid, Guid> pastedIds = envelope.Items
            .ToDictionary(item => item.Id, _ => Guid.NewGuid());

        foreach (CanvasItem item in envelope.Items)
        {
            item.Id = pastedIds[item.Id];
        }

        foreach (CanvasItem item in envelope.Items)
        {
            if (item is LinkItem link)
            {
                link.TargetItemId = RemapTarget(
                    link.TargetItemId,
                    pastedIds,
                    destinationIds);
            }

            item.Translate(offset);
        }

        return envelope.Items;
    }

    private static void Validate(ClipboardEnvelope envelope)
    {
        if (envelope.Version != CurrentVersion)
        {
            throw new JsonException($"Unsupported clipboard version: {envelope.Version}.");
        }

        if (envelope.Items.GroupBy(item => item.Id).Any(group => group.Count() > 1))
        {
            throw new JsonException("Clipboard payload contains duplicate identifiers.");
        }
    }

    private static Guid RemapTarget(
        Guid oldTargetId,
        IReadOnlyDictionary<Guid, Guid> pastedIds,
        IReadOnlySet<Guid> destinationIds)
    {
        if (oldTargetId == Guid.Empty)
        {
            return Guid.Empty;
        }

        if (pastedIds.TryGetValue(oldTargetId, out Guid pastedTargetId))
        {
            return pastedTargetId;
        }

        return destinationIds.Contains(oldTargetId)
            ? oldTargetId
            : Guid.Empty;
    }
}
