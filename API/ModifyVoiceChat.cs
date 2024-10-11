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
                    if (speaker.netId == listener.netId && channel != VoiceChatChannel.Mimicry)
                    {
                        return VoiceChatChannel.None;
                    }
                    if (channel == VoiceChatChannel.Mimicry)
                        return channel;

                    if (listener.GetRoleId() != RoleTypeId.Scientist && speaker.GetRoleId() != RoleTypeId.Scientist)
                    {
                        return VoiceChatChannel.RoundSummary;
                    }
                    else if (speaker.IsSCP())
                    {
                        return VoiceChatChannel.None;
                    }
                    break;
                }
            case DogHideAndSeek:
                {
                    if (speaker.netId == listener.netId && channel != VoiceChatChannel.Mimicry)
                    {
                        return VoiceChatChannel.None;
                    }
                    if (channel == VoiceChatChannel.Mimicry)
                        return channel;

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
