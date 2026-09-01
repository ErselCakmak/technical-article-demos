using System.IO;
using System.Windows.Media.Imaging;

namespace WpfColorProfileSafeLoader;

public sealed class SafeBitmapLoader
{
    private readonly ColorProfileFallbackPolicy _fallbackPolicy;

    public SafeBitmapLoader(
        Action<ColorProfileFallbackEvent>? onFallback = null)
    {
        _fallbackPolicy = new ColorProfileFallbackPolicy(onFallback);
    }

    public BitmapImage LoadFile(
        string path,
        int decodePixelWidth = 0,
        int decodePixelHeight = 0,
        BitmapCreateOptions requestedOptions = BitmapCreateOptions.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return _fallbackPolicy.Execute(
            options => DecodeStream(
                () => new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete),
                options,
                decodePixelWidth,
                decodePixelHeight),
            path,
            requestedOptions);
    }

    public BitmapImage LoadBytes(
        byte[] bytes,
        string sourceDescription = "memory image",
        int decodePixelWidth = 0,
        int decodePixelHeight = 0,
        BitmapCreateOptions requestedOptions = BitmapCreateOptions.None)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        return _fallbackPolicy.Execute(
            options => DecodeStream(
                () => new MemoryStream(bytes, writable: false),
                options,
                decodePixelWidth,
                decodePixelHeight),
            sourceDescription,
            requestedOptions);
    }

    public BitmapImage LoadStream(
        Stream source,
        string sourceDescription = "stream image",
        int decodePixelWidth = 0,
        int decodePixelHeight = 0,
        BitmapCreateOptions requestedOptions = BitmapCreateOptions.None)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var ownedCopy = new MemoryStream();
        source.CopyTo(ownedCopy);

        return LoadBytes(
            ownedCopy.ToArray(),
            sourceDescription,
            decodePixelWidth,
            decodePixelHeight,
            requestedOptions);
    }

    public BitmapImage LoadUri(
        Uri uri,
        int decodePixelWidth = 0,
        int decodePixelHeight = 0,
        BitmapCreateOptions requestedOptions = BitmapCreateOptions.None)
    {
        ArgumentNullException.ThrowIfNull(uri);

        return _fallbackPolicy.Execute(
            options =>
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = options;
                image.DecodePixelWidth = decodePixelWidth;
                image.DecodePixelHeight = decodePixelHeight;
                image.UriSource = uri;
                image.EndInit();
                image.Freeze();
                return image;
            },
            uri.ToString(),
            requestedOptions);
    }

    private static BitmapImage DecodeStream(
        Func<Stream> openSource,
        BitmapCreateOptions options,
        int decodePixelWidth,
        int decodePixelHeight)
    {
        using Stream source = openSource();

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = options;
        image.DecodePixelWidth = decodePixelWidth;
        image.DecodePixelHeight = decodePixelHeight;
        image.StreamSource = source;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
