using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Plugins;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp079;
using PlayerRoles.PlayableScps.Scp096;
using PlayerRoles.PlayableScps.Scp106;
using UGNHintSystem.Core.Enum;
using UGNHintSystem.Core.Extension;
using UGNHintSystem.Core.Models.Arguments;
using UnityEngine;
using Player = LabApi.Features.Wrappers.Player;

namespace LabAPISCPOverlay
{
    public class SCPOverlayLabAPI : Plugin
    {
        public override string Name { get; } = "SCPOverlay";
        public override string Description { get; } = "SCP Overlay for LabAPI";
        public override string Author { get; } = "SeekEDstroy, GroupXyz";
        public override Version Version { get; } = new Version(1, 0, 0, 0);
        public override Version RequiredApiVersion { get; } = new Version(LabApiProperties.CompiledVersion);

        public override void Enable()
        {
            LabApi.Events.Handlers.ServerEvents.RoundStarted += OnRoundStarted;
            LabApi.Events.Handlers.PlayerEvents.ChangingRole += OnPlayerChangingRole;
        }

        public override void Disable()
        {
            LabApi.Events.Handlers.ServerEvents.RoundStarted -= OnRoundStarted;
            LabApi.Events.Handlers.PlayerEvents.ChangingRole -= OnPlayerChangingRole;
        }
        
        private static readonly Dictionary<int, int> scp079XpRequirements = new Dictionary<int, int>
        {
            { 1, 80 },
            { 2, 130 },
            { 3, 250 },
            { 4, 500 },
            { 5, int.MaxValue },
        };

        private static void OnRoundStarted()
        {
            foreach (Player player in Player.List)
            {
                if (!ShouldDisplayForPlayer(player))
                    continue;
                SetupPlayerHint(player);
            }
        }

        private static void OnPlayerChangingRole(PlayerChangingRoleEventArgs ev)
        {
            MEC.Timing.CallDelayed(1f, () =>
            {
                if (!ShouldDisplayForPlayer(ev.Player))
                    return;
                SetupPlayerHint(ev.Player);
            });
        }

        private static void SetupPlayerHint(Player player)
        {
            if (player == null)
                return;
            var playerDisplay = player.GetPlayerDisplay();
            if (playerDisplay == null)
                return;
            var scpHint = new UGNHintSystem.Core.Models.Hints.Hint
            {
                AutoText = (UGNHintSystem.Core.Models.Arguments.AutoContentUpdateArg arg) => GenerateScpInfoText(player),
                XCoordinate = GetRightXPosition(player.ReferenceHub.aspectRatioSync.AspectRatio),
                YCoordinate = 300,
                SyncSpeed = HintSyncSpeed.Fast,
                Alignment = HintAlignment.Right
            };
            playerDisplay.AddHint(scpHint);
        }

        private static string GenerateScpInfoText(Player player)
        {
            if (!ShouldDisplayForPlayer(player))
                return string.Empty;
            var realScps = Player.List.Where(p =>
                p != null &&
                p.Role.GetTeam() == Team.SCPs &&
                p.Role != RoleTypeId.Scp0492 &&
                p != player &&
                DisplayStrings.ContainsKey(p.Role)
            ).ToList();
            if (realScps.Count == 0)
                return string.Empty;
            StringBuilder builder = new StringBuilder();
            builder.Append("<b><size=20><color=#5D0000>S</color><color=#5D0000>C</color><color=#5D0000>P</color><color=#680000>-</color><color=#870000>O</color><color=#9D0000>V</color><color=#B80000>E</color><color=#C01600>R</color><color=#C72B00>L</color><color=#CF4600>A</color><color=#D75F00>Y</color></b>\n");
            foreach (Player scp in realScps)
            {
                var roleId = scp.Role;
                if (DisplayStrings.ContainsKey(roleId))
                {
                    if (player.Role == RoleTypeId.Scp079)
                    {
                        builder.Append(ProcessStringVariablesForPc(PCDisplayStrings.TryGetValue(roleId, out var format) ? format : DisplayStrings[roleId], player, scp)).Append('\n');
                    }
                    else
                    {
                        builder.Append(ProcessStringVariables(DisplayStrings[roleId], player, scp)).Append('\n');
                    }
                }
            }
            return builder.ToString();
        }

        private static bool ShouldDisplayForPlayer(Player player)
        {
            return player != null && player.Role.GetTeam() == Team.SCPs;
        }

        private static string ProcessStringVariables(string raw, Player player, Player target)
        {
            int zombieCount = Player.List.Count(p => p.Role == RoleTypeId.Scp0492);
            string zombieInfo = zombieCount > 0 ? $"\n<color=#FF19FF>🧟 {zombieCount}</color>" : "";
            string targetsInfo = "";
            int scp096Targets = 0;
            // SCP-096 Targets
            if (target.Role == RoleTypeId.Scp096 && target.RoleBase is Scp096Role scp096)
            {
                if (scp096.SubroutineModule.TryGetSubroutine(out Scp096TargetsTracker targetsTracker))
                {
                    scp096Targets = targetsTracker.Targets.Count;
                }
                
                if (scp096Targets > 0)
                    targetsInfo = $"\n<color=#FF0000>👤 {scp096Targets}</color>";
            }
            // SCP-079
            int scp079Level = 0;
            int scp079Xp = 0;
            float scp079Energy = 0;
            if (target.Role == RoleTypeId.Scp079 && target.RoleBase is Scp079Role scp079)
            {
                if (scp079.SubroutineModule.TryGetSubroutine(out Scp079TierManager tierManager))
                {
                    scp079Level = tierManager.AccessTierLevel;
                    scp079Xp = tierManager.TotalExp;
                }

                if (scp079.SubroutineModule.TryGetSubroutine(out Scp079AuxManager auxManager))
                {
                    scp079Energy = auxManager.CurrentAux;
                }
            }
            // SCP-106 Vigor
            float scp106Vigor = 0;
            if (target.Role == RoleTypeId.Scp106 && target.RoleBase is Scp106Role scp106)
            {
                if (scp106.SubroutineModule.TryGetSubroutine(out Scp106VigorAbilityBase abilityBase))
                {
                    scp106Vigor = abilityBase.VigorAmount;
                }
            }
            return raw
                .Replace("%arhealth%", target.HumeShield > 0 ? Math.Floor(target.HumeShield).ToString() : "0")
                .Replace("%maxarhealth%", target.HumeShield > 0 ? Math.Floor(target.MaxHumeShield).ToString() : "0")
                .Replace("%maxhealth%", target.MaxHealth > 0 ? Math.Floor(target.MaxHealth).ToString() : "100")
                .Replace("%healthpercent%", target.MaxHealth > 0 ? Math.Floor(target.Health / target.MaxHealth * 100).ToString() : "0")
                .Replace("%health%", target.Health > 0 ? Math.Floor(target.Health).ToString() : "0")
                .Replace("%generators%", Generator.List.Count(gen => gen.Engaged).ToString())
                .Replace("%engaging%", Generator.List.Count(gen => gen.Activating) > 0 ? $"/{Generator.List.Count(gen => gen.Activating)}" : string.Empty)
                .Replace("%distance%", target != player ? Math.Floor(Vector3.Distance(player.Position, target.Position)) + "m" : string.Empty)
                .Replace("%room%", GetRoomName(target))
                .Replace("%zombies%", zombieCount.ToString())
                .Replace("%zombiesinfo%", zombieInfo)
                .Replace("%079level%", scp079Level > 0 ? scp079Level.ToString() + "/5" : string.Empty)
                .Replace("%079xpn%", scp079Level > 0 ? GetScp079XpProgress(scp079Level, scp079Xp) : "N/A")
                .Replace("%079energy%", scp079Level > 0 ? Math.Floor(scp079Energy).ToString() : string.Empty)
                .Replace("%079experience%", scp079Level > 0 ? scp079Xp.ToString() : string.Empty)
                .Replace("%106vigor%", scp106Vigor > 0 ? Math.Floor(scp106Vigor * 100).ToString() : string.Empty)
                .Replace("%096targets%", scp096Targets > 0 ? scp096Targets.ToString() : string.Empty)
                .Replace("%096targetsinfo%", targetsInfo)
                .Replace("%playername%", target.Nickname);
        }

        private static string ProcessStringVariablesForPc(string raw, Player player, Player target)
        {
            int zombieCount = Player.List.Count(p => p.Role == RoleTypeId.Scp0492);
            string zombieInfo = zombieCount > 0 ? $"\n<color=#FF19FF>🧟 {zombieCount}</color>" : "";
            string targetsInfo = "";
            int scp096Targets = 0;
            if (target.Role == RoleTypeId.Scp096 && target.RoleBase is Scp096Role scp096)
            {
                if (scp096.SubroutineModule.TryGetSubroutine(out Scp096TargetsTracker targetsTracker))
                {
                    scp096Targets = targetsTracker.Targets.Count;
                }
                
                if (scp096Targets > 0)
                    targetsInfo = $"\n<color=#FF0000>👤 {scp096Targets}</color>";
            }
            int scp079Level = 0;
            int scp079Xp = 0;
            float scp079Energy = 0;
            if (target.Role == RoleTypeId.Scp079 && target.RoleBase is Scp079Role scp079)
            {
                if (scp079.SubroutineModule.TryGetSubroutine(out Scp079TierManager tierManager))
                {
                    scp079Level = tierManager.AccessTierLevel;
                    scp079Xp = tierManager.TotalExp;
                }
                if (scp079.SubroutineModule.TryGetSubroutine(out Scp079AuxManager auxManager))
                {
                    scp079Energy = auxManager.CurrentAux;
                }
            }
            float scp106Vigor = 0;
            if (target.Role == RoleTypeId.Scp106 && target.RoleBase is Scp106Role scp106)
            {
                if (scp106.SubroutineModule.TryGetSubroutine(out Scp106VigorAbilityBase abilityBase))
                {
                    scp106Vigor = abilityBase.VigorAmount;
                }
            }
            return raw
                .Replace("%arhealth%", target.HumeShield > 0 ? Math.Floor(target.HumeShield).ToString() : "0")
                .Replace("%maxarhealth%", target.HumeShield > 0 ? Math.Floor(target.MaxHumeShield).ToString() : "0")
                .Replace("%maxhealth%", target.MaxHealth > 0 ? Math.Floor(target.MaxHealth).ToString() : "100")
                .Replace("%healthpercent%", target.MaxHealth > 0 ? Math.Floor(target.Health / target.MaxHealth * 100).ToString() : "0")
                .Replace("%health%", target.Health > 0 ? Math.Floor(target.Health).ToString() : "0")
                .Replace("%generators%", Generator.List.Count(gen => gen.Engaged).ToString())
                .Replace("%engaging%", Generator.List.Count(gen => gen.Activating) > 0 ? $"/{Generator.List.Count(gen => gen.Activating)}" : string.Empty)
                .Replace("%room%", GetRoomName(target))
                .Replace("%zombies%", zombieCount.ToString())
                .Replace("%zombiesinfo%", zombieInfo)
                .Replace("%079level%", scp079Level > 0 ? scp079Level.ToString() + "/5" : string.Empty)
                .Replace("%079xpn%", scp079Level > 0 ? GetScp079XpProgress(scp079Level, scp079Xp) : "N/A")
                .Replace("%079energy%", scp079Level > 0 ? Math.Floor(scp079Energy).ToString() : string.Empty)
                .Replace("%079experience%", scp079Level > 0 ? scp079Xp.ToString() : string.Empty)
                .Replace("%106vigor%", scp106Vigor > 0 ? Math.Floor(scp106Vigor * 100).ToString() : string.Empty)
                .Replace("%096targets%", scp096Targets > 0 ? scp096Targets.ToString() : string.Empty)
                .Replace("%096targetsinfo%", targetsInfo)
                .Replace("%playername%", target.Nickname);
        }
        
        private static string GetRoomName(Player player)
        {
            if (player == null || player.Room == null)
                return "Unbekannt";

            return player.Room.ToString();
        }

        private static string GetScp079XpProgress(int level, int xp)
        {
            if (level >= 5)
                return "MAX";
            int xpForCurrentLevel = level == 1 ? 0 : scp079XpRequirements.Take(level - 1).Sum(x => x.Value);
            int xpNeededForNextLevel = scp079XpRequirements[level];
            int xpProgress = xp - xpForCurrentLevel;
            return $"{xpProgress}/{xpNeededForNextLevel}";
        }

        private static float GetRightXPosition(float aspectRatio)
        {
            return -1 * ((622.27444f * Mathf.Pow(aspectRatio, 3f)) + (-2869.08991f * Mathf.Pow(aspectRatio, 2f)) + (3827.03102f * aspectRatio) - 1580.21554f);
        }

        private static Dictionary<RoleTypeId, string> DisplayStrings { get; set; } = new Dictionary<RoleTypeId, string>()
        {
            { RoleTypeId.Scp049, "<size=15><color=#FF0000>SCP-049</color> | <color=#19FF40>💚 %health%/%maxhealth%</color> | <color=#19B2FF>💙 %arhealth%/%maxarhealth%</color> | <color=#6f00ff>📏 %distance%</color>%zombiesinfo%</size>" },
            { RoleTypeId.Scp079, "<size=15><color=#FF0000>SCP-079</color> | <color=#19B2FF>⭐%079level%</color> | <color=#4400ff>✨%079xpn%</color> | <color=#FF19FF>🔋 %079energy%</color> | <color=#19FF40>⚡%generators%%engaging%/3</color></size>" },
            { RoleTypeId.Scp096, "<size=15><color=#FF0000>SCP-096</color> | <color=#19FF40>💚 %health%/%maxhealth%</color> | <color=#19B2FF>💙 %arhealth%/%maxarhealth%</color> | <color=#6f00ff>📏 %distance%</color>%096targetsinfo%</size>" },
            { RoleTypeId.Scp106, "<size=15><color=#FF0000>SCP-106</color> | <color=#19FF40>💚 %health%/%maxhealth%</color> | <color=#19B2FF>💙 %arhealth%/%maxarhealth%</color> | <color=#6f00ff>📏 %distance%</color></size>" },
            { RoleTypeId.Scp173, "<size=15><color=#FF0000>SCP-173</color> | <color=#19FF40>💚 %health%/%maxhealth%</color> | <color=#19B2FF>💙 %arhealth%/%maxarhealth%</color> | <color=#6f00ff>📏 %distance%</color></size>" },
            { RoleTypeId.Scp939, "<size=15><color=#FF0000>SCP-939</color> | <color=#19FF40>💚 %health%/%maxhealth%</color> | <color=#19B2FF>💙 %arhealth%/%maxarhealth%</color> | <color=#6f00ff>📏 %distance%</color></size>" },
        };

        private static Dictionary<RoleTypeId, string> PCDisplayStrings { get; set; } = new Dictionary<RoleTypeId, string>()
        {
            { RoleTypeId.Scp049, "<size=15><color=#FF0000>SCP-049</color> | <color=#19FF40>💚 %health%/%maxhealth%</color> | <color=#19B2FF>💙 %arhealth%/%maxarhealth%</color> | <color=#6f00ff>🚪 %room%</color>%zombiesinfo%</size>" },
            { RoleTypeId.Scp096, "<size=15><color=#FF0000>SCP-096</color> | <color=#19FF40>💚 %health%/%maxhealth%</color> | <color=#19B2FF>💙 %arhealth%/%maxarhealth%</color> | <color=#6f00ff>🚪 %room%</color>%096targetsinfo%</size>" },
            { RoleTypeId.Scp106, "<size=15><color=#FF0000>SCP-106</color> | <color=#19FF40>💚 %health%/%maxhealth%</color> | <color=#19B2FF>💙 %arhealth%/%maxarhealth%</color> | <color=#6f00ff>🚪 %room%</color></size>" },
            { RoleTypeId.Scp173, "<size=15><color=#FF0000>SCP-173</color> | <color=#19FF40>💚 %health%/%maxhealth%</color> | <color=#19B2FF>💙 %arhealth%/%maxarhealth%</color> | <color=#6f00ff>🚪 %room%</color></size>" },
            { RoleTypeId.Scp939, "<size=15><color=#FF0000>SCP-939</color> | <color=#19FF40>💚 %health%/%maxhealth%</color> | <color=#19B2FF>💙 %arhealth%/%maxarhealth%</color> | <color=#6f00ff>🚪 %room%</color></size>" },
        };
    }
}
