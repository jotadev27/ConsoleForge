using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ConsoleForge.UI.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    public ConfirmDialog(string heading, string message) : this()
    {
        HeadingText.Text = heading.ToUpperInvariant();
        MessageText.Text = message;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close(false);
        if (e.Key == Key.Enter) Close(true);
    }

    private void OnYes(object? sender, RoutedEventArgs e) => Close(true);

    private void OnNo(object? sender, RoutedEventArgs e) => Close(false);
}
