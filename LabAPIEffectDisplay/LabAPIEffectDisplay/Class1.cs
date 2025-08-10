using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Plugins;
using MEC;

namespace LabAPIEffectDisplay
{
    public class Class1 : Plugin
    {
        public override string Name { get; } = "LabAPIEffectDisplay";
        
        public override string Description { get; } = "This plugin does effect displays!";
        
        public override string Author { get; } = "GroupXyz";

        public override Version Version { get; } = new Version(1, 0, 0, 0);
        
        public override Version RequiredApiVersion { get; } = new Version(LabApiProperties.CompiledVersion);
        
        private readonly Dictionary<Player, CoroutineHandle> _playerCoroutines = new Dictionary<Player, CoroutineHandle>();
        
        public override void Enable()
        {
            PlayerEvents.Joined += OnPlayerJoined;
            PlayerEvents.Left += OnPlayerLeft;
        }

        public override void Disable()
        {
            PlayerEvents.Joined -= OnPlayerJoined;
            PlayerEvents.Left -= OnPlayerLeft;
            
            foreach (var coroutine in _playerCoroutines.Values)
            {
                Timing.KillCoroutines(coroutine);
            }
            _playerCoroutines.Clear();
        }

        public void OnPlayerJoined(PlayerJoinedEventArgs ev)
        {
            Player player = ev.Player;
            
            var coroutineHandle = Timing.RunCoroutine(ShowEffectsCoroutine(player));
            _playerCoroutines[player] = coroutineHandle;
        }

        public void OnPlayerLeft(PlayerLeftEventArgs ev)
        {
            Player player = ev.Player;
            
            if (_playerCoroutines.TryGetValue(player, out var coroutineHandle))
            {
                Timing.KillCoroutines(coroutineHandle);
                _playerCoroutines.Remove(player);
            }
        }

        private IEnumerator<float> ShowEffectsCoroutine(Player player)
        {
            while (player != null && player.GameObject != null)
            {
                if (CheckConditions(player))
                {
                    var effectList = ImmutableList<string>.Empty;
                    
                    foreach (var effect in player.ActiveEffects)
                    {
                        if (effect != null)
                        {
                            effectList = effectList.Add(effect.name + "\n");
                        }
                    }
                    
                    if (effectList.Count > 0)
                    {
                        player.SendHint($"<pos=-100%><voffset=10px><color=black>{string.Join("", effectList)}</color></voffset>");
                    }
                    else
                    {
                        player.SendHint("<pos=-100%><voffset=10px><color=green>Keine Effekte aktiv</color></voffset>");
                    }
                }
                
                yield return Timing.WaitForSeconds(1.0f);
            }
        }

        public bool CheckConditions(Player player)
        {
            if (Round.IsRoundStarted && player != null && player.IsAlive) {
                return true;
            } else {
                return false;
            }
        }
    }
    
}