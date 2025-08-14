using System;
using System.Linq;
using System.Reflection;
using CommandSystem;
using Footprinting;
using InventorySystem;
using InventorySystem.Items;
using InventorySystem.Items.Pickups;
using InventorySystem.Items.ThrowableProjectiles;
using LabApi.Features;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Plugins;
using Mirror;
using RemoteAdmin;
using ThrowableItem = InventorySystem.Items.ThrowableProjectiles.ThrowableItem;
using UnityEngine;
using Utils;
using Logger = LabApi.Features.Console.Logger;
using ICommand = System.Windows.Input.ICommand;

namespace Explode
{
    public class Class1 : Plugin
    {
        private ExplodeCommand _explodeCommand;
        
        public override void Enable()
        {
            _explodeCommand = new ExplodeCommand(this);
            CommandProcessor.RemoteAdminCommandHandler.RegisterCommand(_explodeCommand);
            Logger.Info("Explode by GroupXyz enabled.");
        }

        public override void Disable()
        {
        }

        public override string Name { get; } = "Explode";
        public override string Description { get; } = "Boom";
        public override string Author { get; } = "GroupXyz";
        public override Version Version { get; } = new Version(1, 0, 0);
        public override Version RequiredApiVersion { get; } = new Version(LabApiProperties.CompiledVersion);
        
        public bool ExplodePlayer(ItemType itemType, Player player, float fuseTime)
        {
            try
            {
                //var player = Player.Get(playerId);
                //if (player == null) 
                //{
                //    Logger.Error($"Player with ID {playerId} not found!");
                //    return false;
                //}

                if (!InventoryItemLoader.TryGetItem(itemType, out ThrowableItem ib))
                {
                    Logger.Error($"Provided item type {itemType} is not a throwable item!");
                    return false;
                }

                ThrownProjectile projectile =
                    UnityEngine.Object.Instantiate(ib.Projectile, player.Position, player.Rotation);

                PickupSyncInfo psi = new PickupSyncInfo(itemType, ib.Weight, ItemSerialGenerator.GenerateNext())
                {
                    Locked = true
                };

                if (projectile is TimeGrenade timeGrenade)
                {
                    var field = typeof(TimeGrenade).GetField("_fuseTime",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                        field.SetValue(timeGrenade, fuseTime);
                    else
                        Logger.Error("Field '_fuseTime' not found!");
                    //timeGrenade.TargetTime = NetworkTime.time + fuseTime;
                }
                else
                {
                    Logger.Info($"Provided item type {itemType} has no fuze time!");
                }

                projectile.Info = psi;
                projectile.PreviousOwner = new Footprint(player.ReferenceHub);
                projectile.ServerActivate();
                NetworkServer.Spawn(projectile.gameObject);
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
                return false;
            }
        }
    }
    
    public class ExplodeCommand : CommandSystem.ICommand
    {
        private readonly Class1 _plugin;

        public ExplodeCommand(Class1 plugin)
        {
            _plugin = plugin;
        }

        public string Command => "explode";
        public string[] Aliases => new[] { "boom" };
        public string Description => "Lasse Spieler explodieren.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count < 1)
            {
                response = "Verwendung: explode|boom <Spielername/ID> (itemType) (fuzeTime)";
                return false;
            }

            string target = arguments.At(0);
            var player = Player.List.FirstOrDefault(p => 
                p.Nickname.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0 || 
                p.PlayerId.ToString() == target);
            
            ItemType itemType = ItemType.GrenadeHE;
            float fuzeTime = 0.5f;

            if (arguments.Count >= 2)
            {
                try
                {
                    itemType = (ItemType)Enum.Parse(typeof(ItemType), arguments.At(1));
                }
                catch (Exception e)
                {
                    response = "Ungültiger ItemType: " + e.Message + "\n Available types: \n ItemType.GrenadeFlash \u2192 Flashbang grenade\n    ItemType.GrenadeHE \u2192 High explosive (frag) grenade\n    ItemType.SCP018 \u2192 SCP-018 (bouncy ball)\n    ItemType.SCP2176 \u2192 SCP-2176 (ghost light)\n";
                    return false;
                }
            }

            if (arguments.Count >= 3)
            {
                try
                {
                    fuzeTime = float.Parse(arguments.At(2));
                }
                catch (Exception e)
                {
                    response = "Ungültige FuzeTime: " + e.Message;
                    return false;
                }
            }

            if (player == null)
            {
                response = $"Spieler '{target}' nicht gefunden.";
                return false;
            }
            
            //uint playerId = (uint)player.PlayerId;
            bool success = _plugin.ExplodePlayer(itemType, player, fuzeTime);

            if (success)
            {
                response = $"{player.Nickname} ({player.PlayerId}) wurde explodiert.";
                return true;
            }
            else
            {
                response = $"Explodieren von {player.Nickname} ({player.PlayerId}) fehlgeschlagen.";
                return false;
            }
        }
    }
}
