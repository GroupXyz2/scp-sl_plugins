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
using MEC;
using UnityEngine;

namespace LabAPISeekEffectDisplay
{
    public class EffectDisplay : Plugin<Config>
    {
        public override string Name { get; } = "EffectDisplay";
        public override string Description { get; } = "Effect Display Plugin";
        public override string Author { get; } = "SeekEDstroy, GroupXyz";
        public override Version Version { get; } = new Version(1, 0, 0, 0);
        public override Version RequiredApiVersion { get; } = new Version(LabApiProperties.CompiledVersion);
        
        private static Dictionary<Player, string> _playerEffectTexts = new Dictionary<Player, string>();
        private static EffectDisplay _instance;
        private static CoroutineHandle _updateCoroutine;
        
        public override void Enable()
        {
            _instance = this;
            
            if (Config?.DebugMode == true)
                LabApi.Features.Console.Logger.Info("EffectDisplay Plugin wird aktiviert...");
                
            PlayerEvents.Spawned += OnPlayerSpawn;
            PlayerEvents.Death += OnPlayerDied;
            ServerEvents.RoundRestarted += OnRoundRestart;
            
            _updateCoroutine = Timing.RunCoroutine(UpdateEffectsCoroutine());
            
            if (Config?.DebugMode == true)
                LabApi.Features.Console.Logger.Info("EffectDisplay Plugin erfolgreich aktiviert!");
        }

        public override void Disable()
        {
            if (_instance?.Config?.DebugMode == true)
                LabApi.Features.Console.Logger.Info("EffectDisplay Plugin wird deaktiviert...");
                
            PlayerEvents.Spawned -= OnPlayerSpawn;
            PlayerEvents.Death -= OnPlayerDied;
            ServerEvents.RoundRestarted -= OnRoundRestart;
            
            if (_updateCoroutine.IsValid)
                Timing.KillCoroutines(_updateCoroutine);
            
            if (_instance?.Config?.DebugMode == true)
                LabApi.Features.Console.Logger.Info("EffectDisplay Plugin erfolgreich deaktiviert!");
                
            _instance = null;
        }

        private static IEnumerator<float> UpdateEffectsCoroutine()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(1f); 
                
                foreach (var player in Player.List)
                {
                    if (player != null && player.IsAlive)
                    {
                        UpdateEffectDisplay(player);
                    }
                }
            }
        }

        private static void OnPlayerSpawn(PlayerSpawnedEventArgs ev)
        {
            if (_instance?.Config?.EventDebug == true)
                LabApi.Features.Console.Logger.Info($"Player {ev.Player?.Nickname ?? "Unknown"} ist gespawnt - starte Effect Display");
                
            EffectDisplayHint(ev.Player);
        }

        private static void OnPlayerDied(PlayerDeathEventArgs ev)
        {
            if (ev?.Player == null)
                return;

            if (_instance?.Config?.EventDebug == true)
                LabApi.Features.Console.Logger.Info($"Player {ev.Player.Nickname} ist gestorben - entferne Effect Display");

            if (_playerEffectTexts.ContainsKey(ev.Player))
            {
                if (_instance?.Config?.PlayerTrackingDebug == true)
                    LabApi.Features.Console.Logger.Info($"Entferne Effect Display für Player {ev.Player.Nickname}");
                    
                _playerEffectTexts.Remove(ev.Player);
            }
        }

        private static void OnRoundRestart()
        {
            if (_instance?.Config?.EventDebug == true)
                LabApi.Features.Console.Logger.Info($"Runde wird neugestartet - lösche alle Effect Displays ({_playerEffectTexts.Count} Einträge)");
                
            _playerEffectTexts.Clear();
        }

        private static void EffectDisplayHint(Player player)
        {
            if (player == null)
            {
                if (_instance?.Config?.DebugMode == true)
                    LabApi.Features.Console.Logger.Warn("EffectDisplayHint: Player ist null");
                return;
            }

            if (_instance?.Config?.PlayerTrackingDebug == true)
                LabApi.Features.Console.Logger.Info($"Starte Effect Display Update für Player {player.Nickname}");

            UpdateEffectDisplay(player);
        }

        private static void UpdateEffectDisplay(Player player)
        {
            if (player == null || !player.IsAlive)
            {
                if (_instance?.Config?.VerboseEffectDebug == true && player != null)
                    LabApi.Features.Console.Logger.Info($"Player {player.Nickname} ist tot oder null - entferne Effect Display");
                    
                if (player != null && _playerEffectTexts.ContainsKey(player))
                    _playerEffectTexts.Remove(player);
                return;
            }

            var effectText = EffectText(player);
            
            if (string.IsNullOrEmpty(effectText))
            {
                if (_instance?.Config?.VerboseEffectDebug == true)
                    LabApi.Features.Console.Logger.Info($"Keine Effekte für Player {player.Nickname} - entferne Display");
                    
                if (_playerEffectTexts.ContainsKey(player))
                    _playerEffectTexts.Remove(player);
                return;
            }

            if (_instance?.Config?.VerboseEffectDebug == true)
                LabApi.Features.Console.Logger.Info($"Aktualisiere Effect Display für Player {player.Nickname} mit {effectText.Split('\n').Length - 1} Effekten");

            _playerEffectTexts[player] = effectText;
            
            player.SendHint(effectText, 1);
        }

        private static string EffectText(Player player)
        {
            if (player == null || !player.IsAlive)
                return string.Empty;

            var effects = GetPlayerEffects(player);
            if (!effects.Any())
            {
                if (_instance?.Config?.VerboseEffectDebug == true)
                    LabApi.Features.Console.Logger.Info($"Keine aktiven Effekte für Player {player.Nickname}");
                return string.Empty;
            }

            if (_instance?.Config?.VerboseEffectDebug == true)
                LabApi.Features.Console.Logger.Info($"Player {player.Nickname} hat {effects.Count} aktive Effekte: {string.Join(", ", effects.Keys.Select(k => k.Name))}");

            var sb = new StringBuilder();
            sb.AppendLine("<b><size=20><color=#4E1717>A</color><color=#541615>K</color><color=#5B1413>T</color><color=#61120F>I</color><color=#65110D>V</color><color=#6C0D09>E</color><color=#700A07> </color><color=#770403>E</color><color=#7C0000>F</color><color=#870000>F</color><color=#9D0000>E</color><color=#B80000>K</color><color=#C01600>T</color><color=#C72B00>E</color></b>");

            foreach (var effect in effects)
            {
                var color = GetEffectColor(effect.Key);
                var intensity = effect.Value.Intensity;
                var duration = effect.Value.Duration;

                if (_instance?.Config?.VerboseEffectDebug == true)
                    LabApi.Features.Console.Logger.Info($"  - {effect.Key.Name}: Intensität {intensity}, Dauer {duration:F1}s, Farbe {color}");

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
            {
                if (_instance?.Config?.DebugMode == true)
                    LabApi.Features.Console.Logger.Warn($"Player {player?.Nickname ?? "null"} hat keinen playerEffectsController");
                return effectInfo;
            }

            try
            {
                var allEffects = player.ReferenceHub.playerEffectsController.AllEffects;
                
                if (allEffects == null)
                {
                    if (_instance?.Config?.DebugMode == true)
                        LabApi.Features.Console.Logger.Warn($"AllEffects ist null für Player {player.Nickname}");
                    return effectInfo;
                }

                int enabledCount = 0;
                foreach (var effect in allEffects)
                {
                    if (effect.IsEnabled)
                    {
                        enabledCount++;
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
                        
                        if (_instance?.Config?.VerboseEffectDebug == true)
                            LabApi.Features.Console.Logger.Info($"    Aktiver Effekt: {effectType.Name} (Intensität: {intensity}, Dauer: {duration:F1}s)");
                    }
                }
                
                if (_instance?.Config?.DebugMode == true && enabledCount > 0)
                    LabApi.Features.Console.Logger.Info($"Player {player.Nickname}: {enabledCount} aktive Effekte gefunden");
            }
            catch (Exception ex)
            {
                if (_instance?.Config?.DebugMode == true)
                    LabApi.Features.Console.Logger.Error($"Fehler beim Abrufen der Effekte für Player {player.Nickname}: {ex.Message}");
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
