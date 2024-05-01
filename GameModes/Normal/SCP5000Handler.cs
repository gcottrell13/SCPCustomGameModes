using Exiled.API.Features;
using PlayerRoles;
using System;
using System.Collections.Generic;
using PlayerEvent = Exiled.Events.Handlers.Player;
using Scp173Event = Exiled.Events.Handlers.Scp173;
using Scp914Event = Exiled.Events.Handlers.Scp914;
using Scp106Event = Exiled.Events.Handlers.Scp106;
using Scp939Event = Exiled.Events.Handlers.Scp939;
using Scp049Event = Exiled.Events.Handlers.Scp049;
using Scp096Event = Exiled.Events.Handlers.Scp096;
using Scp3114Event = Exiled.Events.Handlers.Scp3114;
using Exiled.Events.EventArgs.Player;
using MEC;
using Exiled.Events.EventArgs.Scp914;
using Exiled.API.Extensions;
using Exiled.Events.EventArgs.Interfaces;
using Exiled.Events.EventArgs.Scp096;

namespace CustomGameModes.GameModes.Normal
{
    internal class SCP5000Handler
    {
        public static List<SCP5000Handler> Instances { get; } = new();

        public static void UnsubscribeAll()
        {
            foreach (var instance in Instances)
            {
                instance.UnsubscribeEventHandlers();
            }
            Instances.Clear();
        }

        bool HasGivenScp5000 = false;
        Player? Scp5000Owner = null;
        RoleTypeId Scp5000OwnerRole;
        int Scp5000Chance;

        CoroutineHandle scp5000Coroutine;
        DateTime LastNoisyAction;

        public SCP5000Handler()
        {
            Scp5000Chance = CustomGameModes.Singleton?.Config.Scp5000Chance ?? 0;
        }

        ~SCP5000Handler()
        {
            UnsubscribeEventHandlers();
        }

        // ----------------------------------------------------------------------------------------------------
        // ----------------------------------------------------------------------------------------------------

        public void SubscribeEventHandlers()
        {
            Scp914Event.UpgradingPlayer += UpgradingPlayer;
        }

        public void UnsubscribeEventHandlers()
        {
            PlayerEvent.ReceivingEffect -= ReceivingEffect;
            PlayerEvent.UsingMicroHIDEnergy -= IsNoisy;
            PlayerEvent.ChangingMicroHIDState -= IsNoisy;
            PlayerEvent.InteractingDoor -= IsNoisy;
            PlayerEvent.InteractingElevator -= IsNoisy;
            PlayerEvent.VoiceChatting -= IsNoisy;
            PlayerEvent.Hurt -= onHurt;
            PlayerEvent.OpeningGenerator -= IsNoisy;
            PlayerEvent.UnlockingGenerator -= IsNoisy;
            PlayerEvent.ClosingGenerator -= IsNoisy;
            PlayerEvent.StoppingGenerator -= IsNoisy;

            Scp914Event.Activating -= IsNoisy;
            Scp914Event.ChangingKnobSetting -= IsNoisy;

            Scp939Event.Lunging -= IsNoisy;
            Scp939Event.Clawed -= IsNoisy;

            Scp173Event.Blinking -= IsNoisy;
            Scp173Event.PlacingTantrum -= IsNoisy;

            Scp106Event.Attacking -= IsNoisy;
            Scp106Event.ExitStalking -= IsNoisy;
            Scp106Event.Teleporting -= IsNoisy;

            Scp049Event.Attacking -= IsNoisy;

            Scp096Event.StartPryingGate -= IsNoisy;
            Scp096Event.Enraging -= Scp096SuperNoisy;
            Scp096Event.CalmingDown -= ResetNoisy; // will become invisible soon after calming down
            Scp096Event.AddingTarget -= ShouldAdd096Target;

            Scp3114Event.Revealed -= IsNoisy;
            Scp3114Event.VoiceLines -= IsNoisy;

            Scp914Event.UpgradingPlayer -= UpgradingPlayer;
            PlayerEvent.Shooting -= IsNoisy;

            if (scp5000Coroutine.IsRunning)
            {
                Log.Debug($"Killing SCP 5000 coroutine for player {Scp5000Owner}");
                Timing.KillCoroutines(scp5000Coroutine);
            }
        }

        public void SubscribeOnPlayerGive()
        {
            PlayerEvent.ReceivingEffect += ReceivingEffect;
            PlayerEvent.InteractingDoor += IsNoisy;
            PlayerEvent.InteractingElevator += IsNoisy;
            PlayerEvent.OpeningGenerator += IsNoisy;
            PlayerEvent.UnlockingGenerator += IsNoisy;
            PlayerEvent.ClosingGenerator += IsNoisy;
            PlayerEvent.StoppingGenerator += IsNoisy;

            Scp914Event.Activating += IsNoisy;
            Scp914Event.ChangingKnobSetting += IsNoisy;

            switch (Scp5000OwnerRole)
            {
                case RoleTypeId.Scp939:
                    {
                        Scp939Event.Lunging += IsNoisy;
                        Scp939Event.Clawed += IsNoisy;
                        break;
                    }
                case RoleTypeId.Scp173:
                    {
                        Scp173Event.Blinking += IsNoisy;
                        Scp173Event.PlacingTantrum += IsNoisy;
                        break;
                    }
                case RoleTypeId.Scp106:
                    {
                        Scp106Event.Attacking += IsNoisy;
                        Scp106Event.ExitStalking += IsNoisy;
                        Scp106Event.Teleporting += IsNoisy;
                        break;
                    }
                case RoleTypeId.Scp049:
                    {
                        Scp049Event.Attacking += IsNoisy;
                        break;
                    }
                case RoleTypeId.Scp0492:
                    {
                        PlayerEvent.Hurt += onHurt;
                        break;
                    }
                case RoleTypeId.Scp096:
                    {
                        Scp096Event.StartPryingGate += IsNoisy;
                        Scp096Event.Enraging += Scp096SuperNoisy;
                        // Scp096Event.Charging is not "noisy" in that we don't want it to overwrite the super noisy time
                        Scp096Event.CalmingDown += ResetNoisy; // overwrite the super noisy time, will become invisible soon after calming down
                        Scp096Event.AddingTarget += ShouldAdd096Target;
                        break;
                    }
                case RoleTypeId.Scp3114:
                    {
                        Scp3114Event.Revealed += IsNoisy;
                        Scp3114Event.VoiceLines += IsNoisy; // lol
                        break;
                    }
                default:
                    {
                        PlayerEvent.UsingMicroHIDEnergy += IsNoisy;
                        PlayerEvent.ChangingMicroHIDState += IsNoisy;
                        PlayerEvent.VoiceChatting += IsNoisy;
                        PlayerEvent.Shooting += IsNoisy;
                        break;
                    }
            }
        }

        void UpgradingPlayer(UpgradingPlayerEventArgs ev)
        {
            if (!HasGivenScp5000 && (
                ev.KnobSetting == Scp914.Scp914KnobSetting.VeryFine
                || ev.KnobSetting == Scp914.Scp914KnobSetting.Fine
                ) && UnityEngine.Random.Range(1, 100) <= Scp5000Chance)
            {
                SetupScp5000(ev.Player);
                PlayIntroCassie();
                ev.Player.Position = ev.OutputPosition;
                ev.IsAllowed = false;
            }
        }

        void ReceivingEffect(ReceivingEffectEventArgs ev)
        {
            if (ev.Player != Scp5000Owner) return;

            if (ev.Effect.GetEffectType() == Exiled.API.Enums.EffectType.Invisible && ev.Intensity == 0)
            {
                if (LastNoisyAction > DateTime.Now) return;
                LastNoisyAction = DateTime.Now;
            }
        }

        void IsNoisy(IPlayerEvent ev)
        {
            if (ev.Player != Scp5000Owner) return;
            noisy();
        }

        void onHurt(HurtEventArgs ev)
        {
            if (ev.Attacker != Scp5000Owner) return;
            if (ev.Attacker?.Role == RoleTypeId.Scp0492) noisy();
        }

        void Scp096SuperNoisy(EnragingEventArgs ev)
        {
            if (ev.Player != Scp5000Owner) return;
            LastNoisyAction = DateTime.Now + TimeSpan.FromMinutes(5);
        }

        void ResetNoisy(IPlayerEvent ev)
        {
            if (ev.Player != Scp5000Owner) return;
            LastNoisyAction = DateTime.Now;
        }

        void noisy()
        {
            Scp5000Owner.DisableEffect(Exiled.API.Enums.EffectType.Invisible);
            if (LastNoisyAction > DateTime.Now) return;
            LastNoisyAction = DateTime.Now;
        }


        void ShouldAdd096Target(AddingTargetEventArgs ev)
        {
            if (ev.Player != Scp5000Owner) return;
            if (Scp5000Owner.TryGetEffect(Exiled.API.Enums.EffectType.Invisible, out var status) && status.Intensity > 0 && status.Duration > 0)
            {
                ev.IsAllowed = false;
            }
        }

        // ----------------------------------------------------------------------------------------------------
        // ----------------------------------------------------------------------------------------------------

        public void SetupScp5000(Player player)
        {
            if (Scp5000Owner != null || HasGivenScp5000) throw new Exception("Someone already has SCP 5000");

            HasGivenScp5000 = true;
            Instances.Add(this);
            Scp5000Owner = player;
            player.EnableEffect(Exiled.API.Enums.EffectType.Invisible, 4f);
            Scp5000OwnerRole = player.Role;
            SubscribeOnPlayerGive();
            ensureScp5000Thread();
            Log.Info($"Gave SCP 5000 to player {Scp5000Owner.NetId} - {Scp5000Owner.Nickname}");
        }

        public void PlayIntroCassie()
        {
            if (Scp5000Owner == null) return;

            Scp5000Owner.PlayCassieAnnouncement(CustomGameModes.Singleton.Config.Scp5000CassieIntro, makeNoise: false);
            Scp5000Owner.ShowHint("WELCOME NEW USER to SCP-5000.\nOthers cannot see you until you make noise", 7);
        }

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

            UnsubscribeEventHandlers();
            Instances.Remove(this);
        }
    }
}
