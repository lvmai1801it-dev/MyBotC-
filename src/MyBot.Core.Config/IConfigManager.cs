using MyBot.Core.Config.Models;

namespace MyBot.Core.Config;

public interface IConfigManager
{
    AppConfig Load();
    void Save(AppConfig config);
    void Validate(string jsonContent);
    AppConfig Migrate(string jsonContent);
}
