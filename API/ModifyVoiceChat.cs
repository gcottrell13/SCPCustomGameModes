using CustomGameModes.GameModes;
using PlayerRoles;
using PlayerRoles.Spectating;
using VoiceChat;

namespace CustomGameModes.API;

internal static class ModifyVoiceChat
{
    public static VoiceChatChannel SCPChat(VoiceChatChannel channel, ReferenceHub speaker, ReferenceHub listener)
    {
        switch (EventHandlers.CurrentGame)
        {
            case TroubleInLC:
                {
                    if (listener.IsAlive() && listener.GetRoleId() != RoleTypeId.Scientist && listener.IsSpectatedBy(speaker))
                    {
                        return VoiceChatChannel.RoundSummary;
                    }
                    else if (speaker.IsSCP())
                    {
                        return VoiceChatChannel.Proximity;
                    }
                    break;
                }
            case DogHideAndSeek:
                {
                    if (listener.IsSCP() && !speaker.IsAlive())
                    {
                        return VoiceChatChannel.RoundSummary;
                    }
                    else if (speaker.IsSCP() && !listener.IsAlive())
                    {
                        return VoiceChatChannel.Spectator;
                    }
                    break;
                }
        }

        return channel;
    }
}
