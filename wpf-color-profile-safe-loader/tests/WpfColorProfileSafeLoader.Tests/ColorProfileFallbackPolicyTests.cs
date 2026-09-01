using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Media.Imaging;

namespace WpfColorProfileSafeLoader.Tests;

[TestClass]
public sealed class ColorProfileFallbackPolicyTests
{
    [TestMethod]
    public void SuccessfulDecodeDoesNotRetry()
    {
        var attempts = new List<BitmapCreateOptions>();
        var policy = new ColorProfileFallbackPolicy();

        string result = policy.Execute(
            options =>
            {
                attempts.Add(options);
                return "decoded";
            },
            "sample.png");

        Assert.AreEqual("decoded", result);
        CollectionAssert.AreEqual(
            new[] { BitmapCreateOptions.None },
            attempts);
    }

    [TestMethod]
    public void ArithmeticFailureRetriesWithIgnoreColorProfile()
    {
        var attempts = new List<BitmapCreateOptions>();
        var policy = new ColorProfileFallbackPolicy();

        string result = policy.Execute(
            options =>
            {
                attempts.Add(options);
                if (attempts.Count == 1)
                {
                    throw new OverflowException("Color conversion overflow.");
                }

                return "fallback decoded";
            },
            "profiled.png");

        Assert.AreEqual("fallback decoded", result);
        CollectionAssert.AreEqual(
            new[]
            {
                BitmapCreateOptions.None,
                BitmapCreateOptions.IgnoreColorProfile,
            },
            attempts);
    }

    [TestMethod]
    public void FallbackPublishesDiagnosticContext()
    {
        ColorProfileFallbackEvent? observed = null;
        int attempt = 0;
        var policy = new ColorProfileFallbackPolicy(item => observed = item);

        policy.Execute(
            _ =>
            {
                if (attempt++ == 0)
                {
                    throw new ArithmeticException("Conversion failed.");
                }

                return true;
            },
            "memory image (128 bytes)");

        Assert.IsNotNull(observed);
        Assert.AreEqual("memory image (128 bytes)", observed.SourceDescription);
        Assert.IsInstanceOfType<ArithmeticException>(observed.Error);
    }

    [TestMethod]
    public void UnrelatedFailurePropagatesWithoutRetry()
    {
        int attempts = 0;
        var policy = new ColorProfileFallbackPolicy();

        Assert.ThrowsException<InvalidDataException>(() =>
            policy.Execute<string>(
                _ =>
                {
                    attempts++;
                    throw new InvalidDataException("Not an image.");
                },
                "corrupt.png"));

        Assert.AreEqual(1, attempts);
    }

    [TestMethod]
    public void ExistingIgnoreColorProfileOptionDoesNotLoop()
    {
        int attempts = 0;
        var policy = new ColorProfileFallbackPolicy();

        Assert.ThrowsException<OverflowException>(() =>
            policy.Execute<string>(
                _ =>
                {
                    attempts++;
                    throw new OverflowException("Still failing.");
                },
                "profiled.png",
                BitmapCreateOptions.IgnoreColorProfile));

        Assert.AreEqual(1, attempts);
    }
}
