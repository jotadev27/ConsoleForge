using ConsoleForge.Core.Models;

namespace ConsoleForge.Core.Services;

public interface IConfigStore
{
    AppConfig Load();
    void Save(AppConfig config);
}
