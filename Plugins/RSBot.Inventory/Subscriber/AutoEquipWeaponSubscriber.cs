using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RSBot.Core;
using RSBot.Core.Event;
using RSBot.Core.Objects;

namespace RSBot.Inventory.Subscriber;

/// <summary>
///     Equips the configured weapon after the character has been loaded.
/// </summary>
internal static class AutoEquipWeaponSubscriber
{
    /// <summary>
    ///     The config key that stores the code name of the weapon to equip.
    /// </summary>
    public const string ConfigKey = "RSBot.Inventory.AutoEquipWeapon";

    /// <summary>
    ///     The slot the weapon has to be moved to.
    /// </summary>
    private const byte WeaponSlot = 6;

    private const int RetryCount = 10;
    private const int RetryDelay = 300;

    /// <summary>
    ///     Subscribes the events.
    /// </summary>
    public static void SubscribeEvents()
    {
        EventManager.SubscribeEvent("OnLoadCharacter", OnLoadCharacter);
    }

    private static void OnLoadCharacter()
    {
        var codeName = PlayerConfig.Get(ConfigKey, string.Empty);
        if (string.IsNullOrWhiteSpace(codeName))
            return;

        var characterName = Game.Player?.Name;
        if (string.IsNullOrEmpty(characterName))
            return;

        Task.Run(() => TryEquip(characterName, codeName.Trim()));
    }

    private static void TryEquip(string characterName, string codeName)
    {
        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            var player = Game.Player;
            if (player == null || player.Name != characterName)
                return;

            try
            {
                var item = player.Inventory?.FirstOrDefault(p =>
                    p.Record != null
                    && string.Equals(p.Record.CodeName, codeName, StringComparison.OrdinalIgnoreCase)
                );

                if (item == null)
                {
                    Thread.Sleep(RetryDelay);
                    continue;
                }

                if (item.Slot == WeaponSlot)
                    return;

                if (!item.Record.IsEquip || item.Record.TypeID3 != 6)
                {
                    Log.Warn($"[AutoEquipWeapon] {codeName} is not a weapon");
                    return;
                }

                if (player.Inventory.MoveItem(item.Slot, WeaponSlot))
                {
                    Log.Notify($"[AutoEquipWeapon] Equipped {item.Record.GetRealName()}");
                    return;
                }

                Thread.Sleep(RetryDelay);
            }
            catch (Exception ex)
            {
                Log.Debug($"[AutoEquipWeapon] {ex.Message}");
                return;
            }
        }

        Log.Warn($"[AutoEquipWeapon] Could not equip {codeName}");
    }
}
