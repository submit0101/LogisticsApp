using CommunityToolkit.Mvvm.Messaging.Messages;
using LogisticsApp.Services;

namespace LogisticsApp.Messages;

public sealed class SettingsChangedMessage : ValueChangedMessage<AppSettings>
{
    public SettingsChangedMessage(AppSettings settings) : base(settings) { }
}