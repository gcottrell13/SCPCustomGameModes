using Exiled.API.Features;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlayerEvent = Exiled.Events.Handlers.Player;
using MapEvent = Exiled.Events.Handlers.Map;
using Scp914Event = Exiled.Events.Handlers.Scp914;
using ServerEvent = Exiled.Events.Handlers.Server;
using Exiled.Events.EventArgs.Player;
using MEC;
using Exiled.Events.EventArgs.Scp914;
using Exiled.API.Extensions;

namespace CustomGameModes.GameModes.Normal
{
    internal class SCP5000Handler
    {
        bool HasGivenScp5000 = false;
        Player Scp5000Owner = null;
        RoleTypeId Scp5000OwnerRole;
        int Scp5000Chance;

        public SCP5000Handler()
        {
            Scp5000Chance = CustomGameModes.Singleton.Config.Scp5000Chance;
        }

        ~SCP5000Handler()
        {
            UnsubscribeEventHandlers();
        }

        // ----------------------------------------------------------------------------------------------------
        // ----------------------------------------------------------------------------------------------------

        public void SubscribeEventHandlers()
        {
            PlayerEvent.VoiceChatting += OnVoiceChat;
            PlayerEvent.Shooting += OnShoot;
            PlayerEvent.MakingNoise += OnMakingNoise;
            PlayerEvent.ReceivingEffect += ReceivingEffect;

            Scp914Event.UpgradingPlayer += UpgradingPlayer;
        }

        public void UnsubscribeEventHandlers()
        {
            PlayerEvent.VoiceChatting -= OnVoiceChat;
            PlayerEvent.Shooting -= OnShoot;
            PlayerEvent.MakingNoise -= OnMakingNoise;
            PlayerEvent.ReceivingEffect -= ReceivingEffect;

            Scp914Event.UpgradingPlayer -= UpgradingPlayer;

            if (scp5000Coroutine.IsRunning)
                Timing.KillCoroutines(scp5000Coroutine);
        }

        void OnVoiceChat(VoiceChattingEventArgs ev)
        {
            if (PlayerIsHidden(ev.Player))
            {
                ev.IsAllowed = false;
            }
        }

        void OnShoot(ShootingEventArgs ev)
        {
            if (PlayerIsHidden(ev.Player))
            {
                LastNoisyAction = DateTime.Now;
                ev.Player.DisableEffect(Exiled.API.Enums.EffectType.Invisible);
            }
        }

        void OnMakingNoise(MakingNoiseEventArgs ev)
        {
            if (PlayerIsHidden(ev.Player))
            {
                ev.IsAllowed = false;
            }
        }

        void UpgradingPlayer(UpgradingPlayerEventArgs ev)
        {
            if (!HasGivenScp5000 && (
                ev.KnobSetting == Scp914.Scp914KnobSetting.VeryFine
                || ev.KnobSetting == Scp914.Scp914KnobSetting.Fine
                ) && UnityEngine.Random.Range(1, 100) <= Scp5000Chance)
            {
                HasGivenScp5000 = true;
                SetupScp5000(ev.Player);
                ev.Player.Position = ev.OutputPosition;
                ev.IsAllowed = false;
            }
        }

        void ReceivingEffect(ReceivingEffectEventArgs ev)
        {
            if (PlayerIsHidden(ev.Player) && !WasRecentlyNoisy && ev.Effect.GetEffectType() == Exiled.API.Enums.EffectType.Invisible && ev.Intensity == 0)
            {
                ev.IsAllowed = false;
            }
        }

        // ----------------------------------------------------------------------------------------------------
        // ----------------------------------------------------------------------------------------------------

        bool PlayerIsHidden(Player player) => player == Scp5000Owner && player.TryGetEffect(Exiled.API.Enums.EffectType.Invisible, out var effect) && effect.Intensity > 0;

        void SetupScp5000(Player player)
        {
            if (Scp5000Owner != null) throw new Exception("Someone already has SCP 5000");

            Scp5000Owner = player;
            player.EnableEffect(Exiled.API.Enums.EffectType.Invisible, 999f);
            Scp5000OwnerRole = player.Role;
            ensureScp5000Thread();
        }

        CoroutineHandle scp5000Coroutine;
        DateTime LastNoisyAction;

        bool WasRecentlyNoisy => (DateTime.Now - LastNoisyAction).TotalSeconds < 4;

        void ensureScp5000Thread()
        {
            if (scp5000Coroutine.IsRunning == false)
            {
                scp5000Coroutine = Timing.RunCoroutine(scp5000Loop());
            }
        }

        private IEnumerator<float> scp5000Loop()
        {
            while (Scp5000Owner != null && Scp5000Owner.IsConnected && Scp5000Owner.Role.Type == Scp5000OwnerRole)
            {
                if (!WasRecentlyNoisy)
                {
                    Scp5000Owner.EnableEffect(Exiled.API.Enums.EffectType.Invisible, 2f);
                }

                yield return Timing.WaitForSeconds(1);
            }
        }
    }
}
