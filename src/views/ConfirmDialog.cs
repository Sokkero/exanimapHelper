using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ExanimapHelper;

/// <summary>
/// A minimal modal Yes/No dialog, built in code. Avalonia ships no MessageBox, and
/// this app only needs a single confirmation prompt, so a tiny purpose-built dialog
/// avoids taking on a dependency. Inherits the app's dark theme.
/// </summary>
public static class ConfirmDialog
{
    public static async Task<bool> ShowAsync(Window owner, string title, string message)
    {
        bool result = false;

        var yes = new Button { Content = "Yes", Width = 80, IsDefault = true };
        var no = new Button { Content = "No", Width = 80, IsCancel = true };

        var dialog = new Window
        {
            Title = title,
            Width = 320,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = PaletteBrush("WindowBg", 0x25, 0x25, 0x26),
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 14,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = PaletteBrush("TextFg", 0xDC, 0xDC, 0xDC),
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { no, yes },
                    },
                },
            },
        };

        yes.Click += (_, _) => { result = true; dialog.Close(); };
        no.Click += (_, _) => { result = false; dialog.Close(); };

        await dialog.ShowDialog(owner);
        return result;
    }

    // Resolves a palette brush from the app resources, falling back to the literal color
    // so the dialog still renders if the resource is missing.
    private static IBrush PaletteBrush(string key, byte r, byte g, byte b) =>
        Application.Current?.TryGetResource(key, null, out object? value) == true && value is IBrush brush
            ? brush
            : new SolidColorBrush(Color.FromRgb(r, g, b));
}
