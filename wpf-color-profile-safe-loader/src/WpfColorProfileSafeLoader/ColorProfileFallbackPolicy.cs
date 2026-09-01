using System.Windows.Media.Imaging;

namespace WpfColorProfileSafeLoader;

public sealed class ColorProfileFallbackPolicy
{
    private readonly Action<ColorProfileFallbackEvent>? _onFallback;

    public ColorProfileFallbackPolicy(
        Action<ColorProfileFallbackEvent>? onFallback = null)
    {
        _onFallback = onFallback;
    }

    public T Execute<T>(
        Func<BitmapCreateOptions, T> decode,
        string sourceDescription,
        BitmapCreateOptions requestedOptions = BitmapCreateOptions.None)
    {
        ArgumentNullException.ThrowIfNull(decode);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDescription);

        try
        {
            return decode(requestedOptions);
        }
        catch (ArithmeticException error)
            when (!requestedOptions.HasFlag(BitmapCreateOptions.IgnoreColorProfile))
        {
            _onFallback?.Invoke(
                new ColorProfileFallbackEvent(sourceDescription, error));

            return decode(
                requestedOptions | BitmapCreateOptions.IgnoreColorProfile);
        }
    }
}
