using System;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Plugins;
using System.Collections.Immutable;
using InventorySystem.Items;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using PlayerRoles;
using CommandSystem;
using System.Linq;
using System.Windows.Input;
using RemoteAdmin;
using ICommand = CommandSystem.ICommand;

namespace coinflip
{
    public class Class1 : Plugin
    {
        public override string Name => "Coinflip";
        public override string Description => "Allows the gambling with coins";
        public override string Author => "GroupXyz";
        public override Version Version => new Version(1, 0, 0, 0);
        public override Version RequiredApiVersion => new Version(LabApiProperties.CompiledVersion);

        private ImmutableList<ItemType> _configItems = ImmutableList<ItemType>.Empty;
        private readonly ConcurrentDictionary<uint, DateTime> _lastFlipTimes = new ConcurrentDictionary<uint, DateTime>();
        private readonly ConcurrentDictionary<uint, DateTime> _lastRoleHintTimes = new ConcurrentDictionary<uint, DateTime>();
        private readonly TimeSpan _flipCooldown = TimeSpan.FromMinutes(5);
        
        private CoinflipResetCommand _resetCommand;

        public override void Enable()
        {
            Logger.Info("Coinflip by GroupXyz enabled.");
            EnsureConfigFileExists();
            LoadConfigItems();
            PlayerEvents.FlippedCoin += OnPlayerFlipCoin;
            _resetCommand = new CoinflipResetCommand(this);
            CommandProcessor.RemoteAdminCommandHandler.RegisterCommand(_resetCommand);
        }

        private void EnsureConfigFileExists()
        {
            const string configPath = "items.yml";
            if (!File.Exists(configPath))
            {
                var defaultConfig = "items:\n  - Medkit\n  - Adrenaline\n  - SCP500\n  - Radio\n  - GrenadeFlash\n  - GrenadeHE\n  - ArmorCombat\n  - ArmorHeavy\n  - SurfaceAccessPass\n  - KeycardGuard\n  - KeycardCustomManagement\n  - GunCrossvec\n  - GunCom45\n  - GunA7\n";
                File.WriteAllText(configPath, defaultConfig);
                Logger.Info($"items.yml wurde automatisch erstellt.");
            }
        }

        private void LoadConfigItems()
        {
            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                var yaml = File.ReadAllText("items.yml");
                var config = deserializer.Deserialize<ItemsConfig>(yaml);
                var itemTypes = new List<ItemType>();
                foreach (var itemName in config.Items)
                {
                    if (Enum.TryParse<ItemType>(itemName, out var itemType))
                        itemTypes.Add(itemType);
                    else
                        Logger.Warn($"Unbekannter ItemType in items.yml: {itemName}");
                }
                _configItems = itemTypes.ToImmutableList();
            }
            catch (Exception ex)
            {
                Logger.Error($"Fehler beim Laden der items.yml: {ex.Message}");
                _configItems = ImmutableList<ItemType>.Empty;
            }
        }

        public override void Disable()
        {
            Logger.Info("Coinflip disabled.");
            PlayerEvents.FlippedCoin -= OnPlayerFlipCoin;
            if (_resetCommand != null)
                CommandProcessor.RemoteAdminCommandHandler.UnregisterCommand(_resetCommand);
        }

        public void OnPlayerFlipCoin(PlayerFlippedCoinEventArgs ev) {
            Player player = ev.Player;
            var now = DateTime.UtcNow;
            uint playerId = (uint)player.PlayerId;
            if (player.Role != RoleTypeId.ClassD && player.Role != RoleTypeId.Scientist)
            {
                if (!_lastRoleHintTimes.TryGetValue(playerId, out var lastHint) || (now - lastHint) > _flipCooldown)
                {
                    player.SendHint("<color=red>Du musst ein Class-D oder Scientist sein, um Coinflip zu benutzen!</color>", 10F);
                    _lastRoleHintTimes[playerId] = now;
                }
                return;
            }

            if (_lastFlipTimes.TryGetValue(playerId, out var lastFlip))
            {
                var remaining = _flipCooldown - (now - lastFlip);
                if (remaining > TimeSpan.Zero)
                {
                    player.SendHint($"<color=orange>Coinflip Cooldown: Noch {remaining.Minutes:D2}:{remaining.Seconds:D2} Minuten.</color>", 8F);
                    return;
                }
            }
            
            if (player.Items.Count() >= 8)
            {
                player.SendHint("<color=red>Dein Inventar ist voll! Entferne Items bevor du Coinflip benutzt.</color>", 8F);
                return;
            }
            
            _lastFlipTimes[playerId] = now;
            if (!ev.IsTails)
            {
                var items = _configItems;
                if (items.Count == 0)
                {
                    player.SendHint("<color=red>Keine Items konfiguriert!</color>", 10F);
                    return;
                }
                var random = new Random();
                var randomItem = items[random.Next(items.Count)];
                player.AddItem(randomItem);
                player.SendHint($"<color=green>Du hast Zahl geflippt und ein Item erhalten: <color=yellow>{randomItem.GetName()}</color></color>", 8F);
            } else {
                player.SendHint("<color=blue>Du hast Kopf geflippt, probiere es später erneut!</color>", 8F);
            }
        }

        public bool ResetPlayerCooldown(uint playerId)
        {
            bool flipRemoved = _lastFlipTimes.TryRemove(playerId, out _);
            bool hintRemoved = _lastRoleHintTimes.TryRemove(playerId, out _);
            return flipRemoved || hintRemoved;
        }
    }

    public class ItemsConfig
    {
        public List<string> Items { get; set; }
    }

    public class CoinflipResetCommand : CommandSystem.ICommand
    {
        private readonly Class1 _plugin;

        public CoinflipResetCommand(Class1 plugin)
        {
            _plugin = plugin;
        }

        public string Command => "coinflip_reset";
        public string[] Aliases => new[] { "cfreset" };
        public string Description => "Setzt den Coinflip-Cooldown für einen Spieler zurück";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count < 1)
            {
                response = "Verwendung: coinflip_reset <Spielername/ID>";
                return false;
            }

            string target = arguments.At(0);
            var player = Player.List.FirstOrDefault(p => 
                p.Nickname.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0 || 
                p.PlayerId.ToString() == target);

            if (player == null)
            {
                response = $"Spieler '{target}' nicht gefunden.";
                return false;
            }

            uint playerId = (uint)player.PlayerId;
            bool success = _plugin.ResetPlayerCooldown(playerId);

            if (success)
            {
                response = $"Coinflip-Cooldown für {player.Nickname} ({player.PlayerId}) wurde zurückgesetzt.";
                return true;
            }
            else
            {
                response = $"Kein aktiver Cooldown für {player.Nickname} ({player.PlayerId}) gefunden.";
                return true;
            }
        }
    }
}
