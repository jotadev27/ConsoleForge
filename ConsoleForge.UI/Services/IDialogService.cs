namespace ConsoleForge.UI.Services;

public interface IDialogService
{
    Task<IReadOnlyList<string>> PickVideoFilesAsync(string? startFolder);

    Task<string?> PickFolderAsync(string? startFolder);

    Task<string?> PickImageFileAsync(string? startFolder);

    Task<string?> SaveLogFileAsync(string suggestedName);

    Task<bool> ConfirmAsync(string heading, string message);
}
