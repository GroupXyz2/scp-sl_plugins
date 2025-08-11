using CustomPlayerEffects;
using InventorySystem.Items.Usables;
using InventorySystem.Items.Usables.Scp244.Hypothermia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabApi.Events.Handlers;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Features;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Plugins;
using UnityEngine;

namespace LabAPISeekEffectDisplay
{
    public class EffectDisplay : Plugin
    {
        public override string Name { get; } = "EffectDisplay";
        public override string Description { get; } = "Effect Display Plugin";
        public override string Author { get; } = "SeekEDstroy, GroupXyz";
        public override Version Version { get; } = new Version(1, 0, 0, 0);
        public override Version RequiredApiVersion { get; } = new Version(LabApiProperties.CompiledVersion);
        
        private static Dictionary<Player, string> playerEffectTexts = new Dictionary<Player, string>();
        
        public override void Enable()
        {
        }

        public override void Disable()
        {
        }

        public static void RegisterEvents()
        {
            PlayerEvents.Spawned += OnPlayerSpawn;
            PlayerEvents.Death += OnPlayerDied;
            ServerEvents.RoundRestarted += OnRoundRestart;
        }

        public static void UnregisterEvents()
        {
            PlayerEvents.Spawned -= OnPlayerSpawn;
            PlayerEvents.Death -= OnPlayerDied;
            ServerEvents.RoundRestarted -= OnRoundRestart;
        }

        private static void OnPlayerSpawn(PlayerSpawnedEventArgs ev)
        {
            EffectDisplayHint(ev.Player);
        }

        private static void OnPlayerDied(PlayerDeathEventArgs ev)
        {
            if (ev.Player == null)
                return;

            if (playerEffectTexts.ContainsKey(ev.Player))
            {
                playerEffectTexts.Remove(ev.Player);
            }
        }

        private static void OnRoundRestart()
        {
            playerEffectTexts.Clear();
        }

        private static void EffectDisplayHint(Player player)
        {
            if (player == null)
                return;

            UpdateEffectDisplay(player);
        }

        private static void UpdateEffectDisplay(Player player)
        {
            if (player == null || !player.IsAlive)
            {
                if (playerEffectTexts.ContainsKey(player))
                    playerEffectTexts.Remove(player);
                return;
            }

            var effectText = EffectText(player);
            
            if (string.IsNullOrEmpty(effectText))
            {
                if (playerEffectTexts.ContainsKey(player))
                    playerEffectTexts.Remove(player);
                return;
            }

            playerEffectTexts[player] = effectText;
            
            player.SendHint(effectText, 1);
        }

        private static string EffectText(Player player)
        {
            if (player == null || !player.IsAlive)
                return string.Empty;

            var effects = GetPlayerEffects(player);
            if (!effects.Any())
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("<b><size=20><color=#4E1717>A</color><color=#541615>K</color><color=#5B1413>T</color><color=#61120F>I</color><color=#65110D>V</color><color=#6C0D09>E</color><color=#700A07> </color><color=#770403>E</color><color=#7C0000>F</color><color=#870000>F</color><color=#9D0000>E</color><color=#B80000>K</color><color=#C01600>T</color><color=#C72B00>E</color></b>");

            foreach (var effect in effects)
            {
                var color = GetEffectColor(effect.Key);
                var intensity = effect.Value.Intensity;
                var duration = effect.Value.Duration;

                var displayText = new StringBuilder();
                displayText.Append($"<color={color}>{effect.Key.Name}");

                if (intensity > 1)
                {
                    displayText.Append($" <color=#FF4B00>{intensity}x</color>");
                }

                if (duration > 0)
                {
                    if (duration >= 60)
                    {
                        int minutes = Mathf.FloorToInt(duration / 60);
                        int seconds = Mathf.FloorToInt(duration % 60);
                        displayText.Append($" <color=#FF4B00>({minutes}:{seconds:00})</color>");
                    }
                    else
                    {
                        displayText.Append($" <color=#FF4B00>({Mathf.CeilToInt(duration)}s)</color>");
                    }
                }

                displayText.Append("</color>");
                sb.AppendLine(displayText.ToString());
            }

            return sb.ToString();
        }

        private static Dictionary<Type, EffectInfo> GetPlayerEffects(Player player)
        {
            var effectInfo = new Dictionary<Type, EffectInfo>();

            if (player?.ReferenceHub?.playerEffectsController == null)
                return effectInfo;

            foreach (var effect in player.ReferenceHub.playerEffectsController.AllEffects)
            {
                if (effect.IsEnabled)
                {
                    var effectType = effect.GetType();

                    byte intensity = 1;
                    float duration = 0f;

                    if (effect is StatusEffectBase statusEffect)
                    {
                        intensity = statusEffect.Intensity;
                        duration = statusEffect.TimeLeft;

                        if (effectType == typeof(Scp1853))
                        {
                            intensity = 1;
                        }
                    }

                    if (effectInfo.ContainsKey(effectType))
                    {
                        effectInfo[effectType] = new EffectInfo
                        {
                            Intensity = intensity,
                            Duration = Math.Max(effectInfo[effectType].Duration, duration)
                        };
                    }
                    else
                    {
                        effectInfo[effectType] = new EffectInfo
                        {
                            Intensity = intensity,
                            Duration = duration
                        };
                    }
                }
            }

            return effectInfo;
        }

        private static string GetEffectColor(Type effectType)
        {
            var positiveEffects = new[]
            {
                typeof(CustomPlayerEffects.Scp207), typeof(MovementBoost), typeof(RainbowTaste),
                typeof(BodyshotReduction), typeof(Invisible), typeof(SpawnProtected),
                typeof(Vitality), typeof(DamageReduction), typeof(Scp268)
            };

            var negativeEffects = new[]
            {
                typeof(Bleeding), typeof(Burned), typeof(Corroding),
                typeof(Deafened), typeof(Disabled), typeof(Exhausted), typeof(Flashed),
                typeof(Hemorrhage), typeof(Hypothermia), typeof(PocketCorroding),
                typeof(Poisoned), typeof(SeveredHands), typeof(Sinkhole), typeof(Ensnared)
            };

            if (positiveEffects.Contains(effectType))
                return "#00FF00";
            else if (negativeEffects.Contains(effectType))
                return "#FF0000";
            else
                return "#FFFF00";
        }

        private struct EffectInfo
        {
            public byte Intensity { get; set; }
            public float Duration { get; set; }
        }
    }
}
