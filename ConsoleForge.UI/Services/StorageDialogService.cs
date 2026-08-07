using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ConsoleForge.UI.Views;

namespace ConsoleForge.UI.Services;

public sealed class StorageDialogService : IDialogService
{
    private readonly TopLevel _topLevel;

    public StorageDialogService(TopLevel topLevel) => _topLevel = topLevel;

    public async Task<IReadOnlyList<string>> PickVideoFilesAsync(string? startFolder)
    {
        var files = await _topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select video files",
            AllowMultiple = true,
            SuggestedStartLocation = await TryGetFolderAsync(startFolder),
            FileTypeFilter =
            [
                new FilePickerFileType("Video files")
                {
                    Patterns =
                    [
                        "*.mp4", "*.mkv", "*.avi", "*.mov", "*.wmv", "*.flv", "*.webm", "*.m4v",
                        "*.mpg", "*.mpeg", "*.ts", "*.m2ts", "*.mts", "*.vob", "*.ogv", "*.3gp",
                        "*.rmvb", "*.divx"
                    ]
                },
                FilePickerFileTypes.All
            ]
        });

        return files.Select(file => file.Path.LocalPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .ToList();
    }

    public async Task<string?> PickFolderAsync(string? startFolder)
    {
        var folders = await _topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select folder",
            AllowMultiple = false,
            SuggestedStartLocation = await TryGetFolderAsync(startFolder)
        });

        var path = folders.FirstOrDefault()?.Path.LocalPath;
        return string.IsNullOrEmpty(path) ? null : path;
    }

    public async Task<string?> PickImageFileAsync(string? startFolder)
    {
        var files = await _topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select cover image",
            AllowMultiple = false,
            SuggestedStartLocation = await TryGetFolderAsync(startFolder),
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.jpg", "*.jpeg", "*.png", "*.bmp", "*.webp", "*.gif", "*.tif", "*.tiff"]
                },
                FilePickerFileTypes.All
            ]
        });

        var path = files.FirstOrDefault()?.Path.LocalPath;
        return string.IsNullOrEmpty(path) ? null : path;
    }

    public async Task<string?> SaveLogFileAsync(string suggestedName)
    {
        var file = await _topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export conversion log",
            SuggestedFileName = suggestedName,
            DefaultExtension = "log",
            FileTypeChoices =
            [
                new FilePickerFileType("Log file") { Patterns = ["*.log", "*.txt"] }
            ]
        });

        var path = file?.Path.LocalPath;
        return string.IsNullOrEmpty(path) ? null : path;
    }

    public async Task<bool> ConfirmAsync(string heading, string message)
    {
        if (_topLevel is not Window owner) return false;

        var dialog = new ConfirmDialog(heading, message);
        return await dialog.ShowDialog<bool>(owner);
    }

    private async Task<IStorageFolder?> TryGetFolderAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return null;

        try
        {
            return await _topLevel.StorageProvider.TryGetFolderFromPathAsync(path);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
