using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using ConsoleForge.UI.ViewModels;

namespace ConsoleForge.UI.Views;

public partial class MainWindow : Window
{
    private INotifyCollectionChanged? _observedLog;
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        QueueList.ContainerPrepared += OnQueueContainerPrepared;
        QueueList.ContainerClearing += OnQueueContainerClearing;
        Closing += OnClosing;
    }

    private void OnQueueContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && ItemAt(viewModel, e.Index) is { } item)
        {
            viewModel.NotifyRowRealized(item);
        }
    }

    private void OnQueueContainerClearing(object? sender, ContainerClearingEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            e.Container.DataContext is QueueItemViewModel item)
        {
            viewModel.NotifyRowUnrealized(item);
        }
    }

    private static QueueItemViewModel? ItemAt(MainWindowViewModel viewModel, int index) =>
        index >= 0 && index < viewModel.Queue.Count ? viewModel.Queue[index] : null;

    private void OnFileNameDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: QueueItemViewModel item }) return;

        item.BeginNameEdit();

        Dispatcher.UIThread.Post(() =>
        {
            if (sender is Control { Parent: Panel panel } &&
                panel.Children.OfType<TextBox>().FirstOrDefault() is { } editor)
            {
                editor.Focus();
                editor.SelectAll();
            }
        }, DispatcherPriority.Background);
    }

    private void OnNameEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: QueueItemViewModel item } editor) return;

        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                if (item.CommitNameEdit())
                {
                    editor.ClearValue(TextBox.BorderBrushProperty);
                }
                else
                {
                    editor.BorderBrush = Brushes.IndianRed;
                }
                break;

            case Key.Escape:
                e.Handled = true;
                editor.ClearValue(TextBox.BorderBrushProperty);
                item.CancelNameEdit();
                break;
        }
    }

    private void OnNameEditorLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: QueueItemViewModel item } editor) return;

        editor.ClearValue(TextBox.BorderBrushProperty);
        item.CancelNameEdit();
    }

    public static WindowEdge? ResolveWindowEdge(Control? grip) =>
        grip?.Tag is string tag && Enum.TryParse<WindowEdge>(tag, ignoreCase: false, out var edge)
            ? edge
            : null;

    private void OnResizeGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ResolveWindowEdge(sender as Control) is not { } edge) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        e.Handled = true;
        BeginResizeDrag(edge, e);
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Button or Shape) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.ClickCount > 1) return;

        BeginMoveDrag(e);
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e) => e.Handled = true;

    private void OnMinimize(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnCloseRequested(object? sender, RoutedEventArgs e) => Close();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != WindowStateProperty) return;
        if (WindowState is not (WindowState.Maximized or WindowState.FullScreen)) return;

        WindowState = WindowState.Normal;
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed || DataContext is not MainWindowViewModel viewModel) return;

        e.Cancel = true;

        if (!await viewModel.ConfirmExitAsync()) return;

        _closeConfirmed = true;
        Close();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_observedLog is not null)
        {
            _observedLog.CollectionChanged -= OnLogChanged;
            _observedLog = null;
        }

        if (DataContext is MainWindowViewModel viewModel)
        {
            _observedLog = viewModel.LogEntries;
            _observedLog.CollectionChanged += OnLogChanged;
        }
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;

        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is not MainWindowViewModel viewModel || viewModel.LogEntries.Count == 0) return;
            LogList.ScrollIntoView(viewModel.LogEntries.Count - 1);
        }, DispatcherPriority.Background);
    }
}
