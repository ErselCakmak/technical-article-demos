using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace WpfColorProfileSafeLoader.Demo;

public partial class MainWindow : Window
{
    private readonly SafeBitmapLoader _safeLoader;
    private string? _selectedPath;
    private ColorProfileFallbackEvent? _lastFallback;

    public MainWindow()
    {
        InitializeComponent();
        _safeLoader = new SafeBitmapLoader(fallback => _lastFallback = fallback);
    }

    private void ChooseImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PNG images (*.png)|*.png|All images|*.png;*.jpg;*.jpeg;*.bmp",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _selectedPath = dialog.FileName;
        SelectedPathText.Text = _selectedPath;
        NormalImage.Source = null;
        SafeImage.Source = null;
        StatusText.Text = "Image selected. Compare the two decode paths.";
    }

    private void LoadNormally_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedPath(out string path))
        {
            return;
        }

        try
        {
            NormalImage.Source = DecodeNormally(path);
            StatusText.Text = "Normal color-managed decoding succeeded.";
        }
        catch (Exception error)
        {
            NormalImage.Source = null;
            StatusText.Text = $"Normal decoding failed: {error.GetType().Name}: {error.Message}";
        }
    }

    private void LoadSafely_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedPath(out string path))
        {
            return;
        }

        _lastFallback = null;

        try
        {
            SafeImage.Source = _safeLoader.LoadFile(path);
            StatusText.Text = _lastFallback is null
                ? "Safe loader succeeded on the normal color-managed attempt."
                : $"Recovered with IgnoreColorProfile after {_lastFallback.Error.GetType().Name}.";
        }
        catch (Exception error)
        {
            SafeImage.Source = null;
            StatusText.Text = $"Safe loader propagated an unrelated error: {error.GetType().Name}: {error.Message}";
        }
    }

    private bool TryGetSelectedPath(out string path)
    {
        path = _selectedPath ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        StatusText.Text = "Choose a PNG first.";
        return false;
    }

    private static BitmapImage DecodeNormally(string path)
    {
        using var source = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = source;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
