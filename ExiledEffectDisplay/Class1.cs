using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Enums;
using MEC;
using Player = Exiled.API.Features.Player;

namespace StatusEffectDisplay
{
    public class Plugin : Plugin<Config>
    {
        private CoroutineHandle coroutineHandle;

        public override string Author => "GroupXyz";
        public override string Name => "ExiledEffectDisplay";
        public override Version Version => new Version(1, 0, 0);

        public override void OnEnabled()
        {
            coroutineHandle = Timing.RunCoroutine(UpdateEffects());
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Timing.KillCoroutines(coroutineHandle);
            base.OnDisabled();
        }

        private IEnumerator<float> UpdateEffects()
        {
            while (true)
            {
                foreach (var player in Player.List)
                {
                    if (!player.IsAlive) continue;

                    var activeEffects = player.ActiveEffects
                        .Where(e => e.IsEnabled && e.name != "FogEffect")
                        .Select(e =>
                        {
                            string durationPart = e.Duration > 0 ? $" ({Math.Round(e.Duration, 1)}s)" : string.Empty;
                            return $"{e.Intensity}x {e.name}{durationPart}";
                        })
                        .ToList();

                    string message;

                    if (activeEffects.Count > 0)
                    {
                        int baseOffset = 1300;
                        int lineSpacing = -150;

                        var lines = new List<string>();
                        for (int i = 0; i < activeEffects.Count; i++)
                        {
                            int offset = baseOffset + (i * lineSpacing);
                            lines.Add($"<pos=-100%><voffset={offset}px><color=black>{activeEffects[i]}</color></voffset>");
                        }

                        message = string.Join("\n", lines);
                    }
                    else
                    {
                        message = "<pos=-100%><voffset=1300px><color=green>Keine Effekte aktiv</color></voffset>";
                    }

                    player.ShowHint(message, 1.5f);
                }

                yield return Timing.WaitForSeconds(1f);
            }
        }



    }

    public class Config : Exiled.API.Interfaces.IConfig
    {
        public bool IsEnabled { get; set; } = true;
        public bool Debug { get; set; } = false;
    }
}