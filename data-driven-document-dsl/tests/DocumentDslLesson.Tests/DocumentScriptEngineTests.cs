using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DocumentDslLesson.Tests;

[TestClass]
public sealed class DocumentScriptEngineTests
{
    private readonly DocumentScriptEngine _engine = new();

    [TestMethod]
    public void ReplaysRecordedBlockForEveryValue()
    {
        const string script = """
            for region in North South
              page "<region> summary"
            end
            """;
        var target = new InMemoryDocumentTarget();

        _engine.Execute(script, target);

        CollectionAssert.AreEqual(
            new[] { "North summary", "South summary" },
            target.Pages.ToArray());
    }

    [TestMethod]
    public void NestedLoopsProduceDeterministicCartesianProduct()
    {
        const string script = """
            for region in North South
              for view in Summary Detail
                page "<region> - <view>"
              end
            end
            """;
        var target = new InMemoryDocumentTarget();

        _engine.Execute(script, target);

        CollectionAssert.AreEqual(
            new[]
            {
                "North - Summary",
                "North - Detail",
                "South - Summary",
                "South - Detail",
            },
            target.Pages.ToArray());
    }

    [TestMethod]
    public void InnerAliasCanShadowAndThenRevealOuterValue()
    {
        const string script = """
            for item in Outer
              page "before <item>"
              for item in Inner
                page "inside <item>"
              end
              page "after <item>"
            end
            """;
        var target = new InMemoryDocumentTarget();

        _engine.Execute(script, target);

        CollectionAssert.AreEqual(
            new[] { "before Outer", "inside Inner", "after Outer" },
            target.Pages.ToArray());
    }

    [TestMethod]
    public void PlanningTargetUsesSameEvaluatorWithoutMutatingDocumentTarget()
    {
        const string script = """
            for region in North South
              page "<region> summary"
            end
            """;
        var realDocument = new InMemoryDocumentTarget();
        var planningTarget = new PlanningDocumentTarget();

        _engine.Execute(script, planningTarget);

        Assert.AreEqual(0, realDocument.Pages.Count);
        CollectionAssert.AreEqual(
            new[] { "Page: North summary", "Page: South summary" },
            planningTarget.PlannedOperations.ToArray());
    }

    [TestMethod]
    public void MissingBlockEndIsReportedBeforeExecution()
    {
        const string script = """
            for region in North South
              page "<region> summary"
            """;

        DocumentScriptException error = Assert.ThrowsException<DocumentScriptException>(
            () => _engine.Execute(script, new InMemoryDocumentTarget()));

        StringAssert.Contains(error.Message, "matching 'end'");
    }
}
