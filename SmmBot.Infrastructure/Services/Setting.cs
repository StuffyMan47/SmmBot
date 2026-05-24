using SmmBot.Core.Interfaces.Settings;
using SmmBot.Core.Interfaces.Settings.Models;
using Microsoft.Extensions.Configuration;

namespace SmmBot.Infrastructure.Services;

public class Setting : ISetting
{
    public string ApplicationName { get; init; }
    
    public BotConfiguration BotConfiguration { get; set; }

    public Setting(IConfiguration configuration)
    {
        var applicationSection = configuration.GetSection("Application");

        ApplicationName = applicationSection["Name"] ?? "Unknown application name";
        BotConfiguration = configuration.GetSection("BotConfiguration").Get<BotConfiguration>() ?? throw new Exception("Не заданы настройки аутентификации");
    }

}