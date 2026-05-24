using SmmBot.Core.Interfaces.Settings.Models;

namespace SmmBot.Core.Interfaces.Settings;

public interface ISetting
{
    public BotConfiguration BotConfiguration { get; set; }
}