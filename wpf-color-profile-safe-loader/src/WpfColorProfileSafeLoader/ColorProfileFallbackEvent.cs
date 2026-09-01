namespace WpfColorProfileSafeLoader;

public sealed record ColorProfileFallbackEvent(
    string SourceDescription,
    ArithmeticException Error);
