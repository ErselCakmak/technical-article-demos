using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphAwareClipboard.Tests;

[TestClass]
public sealed class GraphClipboardTests
{
    [TestMethod]
    public void Paste_RemapsReferenceWhenBothNodesWereCopied()
    {
        CardItem source = Card("Source", 10, 20);
        LinkItem dependent = Link(source.Id, 50, 60, 15, 25);
        string payload = GraphClipboard.Copy(Guid.NewGuid(), [source, dependent]);

        IReadOnlyList<CanvasItem> pasted = GraphClipboard.Paste(
            payload,
            destinationItemIds: [],
            offset: new CanvasPoint(10, 10));

        CardItem pastedSource = pasted.OfType<CardItem>().Single();
        LinkItem pastedLink = pasted.OfType<LinkItem>().Single();

        Assert.AreNotEqual(source.Id, pastedSource.Id);
        Assert.AreNotEqual(dependent.Id, pastedLink.Id);
        Assert.AreEqual(pastedSource.Id, pastedLink.TargetItemId);
    }

    [TestMethod]
    public void Paste_PreservesReferenceThatExistsInDestination()
    {
        Guid existingTargetId = Guid.NewGuid();
        LinkItem dependent = Link(existingTargetId, 50, 60, 15, 25);
        string payload = GraphClipboard.Copy(Guid.NewGuid(), [dependent]);

        LinkItem pasted = GraphClipboard.Paste(
                payload,
                destinationItemIds: [existingTargetId],
                offset: default)
            .OfType<LinkItem>()
            .Single();

        Assert.AreEqual(existingTargetId, pasted.TargetItemId);
    }

    [TestMethod]
    public void Paste_ClearsMissingExternalReference()
    {
        LinkItem dependent = Link(Guid.NewGuid(), 50, 60, 15, 25);
        string payload = GraphClipboard.Copy(Guid.NewGuid(), [dependent]);

        LinkItem pasted = GraphClipboard.Paste(payload, [], default)
            .OfType<LinkItem>()
            .Single();

        Assert.AreEqual(Guid.Empty, pasted.TargetItemId);
    }

    [TestMethod]
    public void Paste_TranslatesPositionAndOwnedControlPoint()
    {
        LinkItem dependent = Link(Guid.Empty, 50, 60, 15, 25);
        string payload = GraphClipboard.Copy(Guid.NewGuid(), [dependent]);

        LinkItem pasted = GraphClipboard.Paste(
                payload,
                destinationItemIds: [],
                offset: new CanvasPoint(10, -5))
            .OfType<LinkItem>()
            .Single();

        Assert.AreEqual(new CanvasPoint(60, 55), pasted.Position);
        Assert.AreEqual(new CanvasPoint(25, 20), pasted.LeaderEnd);
    }

    [TestMethod]
    public void Paste_RejectsUnsupportedVersion()
    {
        var envelope = new ClipboardEnvelope(99, Guid.NewGuid(), [Card("Source", 0, 0)]);
        string payload = JsonSerializer.Serialize(envelope);

        JsonException error = Assert.ThrowsException<JsonException>(
            () => GraphClipboard.Paste(payload, [], default));

        StringAssert.Contains(error.Message, "Unsupported clipboard version");
    }

    [TestMethod]
    public void Paste_DoesNotMutateSourceObjects()
    {
        CardItem source = Card("Source", 10, 20);
        LinkItem dependent = Link(source.Id, 50, 60, 15, 25);
        Guid originalSourceId = source.Id;
        Guid originalDependentId = dependent.Id;
        string payload = GraphClipboard.Copy(Guid.NewGuid(), [source, dependent]);

        _ = GraphClipboard.Paste(payload, [], new CanvasPoint(100, 100));

        Assert.AreEqual(originalSourceId, source.Id);
        Assert.AreEqual(originalDependentId, dependent.Id);
        Assert.AreEqual(originalSourceId, dependent.TargetItemId);
        Assert.AreEqual(new CanvasPoint(50, 60), dependent.Position);
        Assert.AreEqual(new CanvasPoint(15, 25), dependent.LeaderEnd);
    }

    private static CardItem Card(string title, double x, double y) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Position = new CanvasPoint(x, y),
    };

    private static LinkItem Link(
        Guid targetId,
        double x,
        double y,
        double leaderX,
        double leaderY) => new()
    {
        Id = Guid.NewGuid(),
        TargetItemId = targetId,
        Position = new CanvasPoint(x, y),
        LeaderEnd = new CanvasPoint(leaderX, leaderY),
    };
}
