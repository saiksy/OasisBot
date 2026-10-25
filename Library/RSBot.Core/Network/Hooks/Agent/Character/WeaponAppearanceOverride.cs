using System;
using System.Collections.Generic;
using RSBot.Core.Objects;

namespace RSBot.Core.Network.Hooks.Agent.Character;

/// <summary>
///     Replaces the equipped weapon item id inside the character data that the proxy
///     forwards to the local client. The bot itself keeps parsing the original data,
///     so the substitution is local to this machine and the server never sees it.
/// </summary>
public static class WeaponAppearanceOverride
{
    public const string EnabledConfigKey = "RSBot.Inventory.PreviewWeapon";
    public const string CodeNameConfigKey = "RSBot.Inventory.PreviewWeaponCodeName";
    public const string DefaultCodeName = "ITEM_CH_SWORD_15_C_RARE";

    private const byte WeaponSlot = 6;
    private const ushort ChunkOpcode = 0x3013;

    private static readonly object _sync = new();
    private static readonly List<HeldChunk> _chunks = new();
    private static readonly List<byte> _stream = new();

    private static bool _active;
    private static int _weaponOffset = -1;
    private static uint _targetItemId;

    /// <summary>
    ///     Gets a value indicating whether the local override is enabled.
    ///     Defaults to enabled; it can be switched off at any time.
    /// </summary>
    public static bool Enabled => GlobalConfig.Get(EnabledConfigKey, true);

    /// <summary>
    ///     Gets the item code name that should replace the equipped weapon.
    /// </summary>
    public static string TargetCodeName =>
        GlobalConfig.Get(CodeNameConfigKey, DefaultCodeName) ?? DefaultCodeName;

    internal static bool IsActive
    {
        get
        {
            lock (_sync)
                return _active;
        }
    }

    internal static bool HasChunks
    {
        get
        {
            lock (_sync)
                return _chunks.Count > 0;
        }
    }

    /// <summary>
    ///     Starts collecting a new character data stream.
    /// </summary>
    internal static void Begin()
    {
        lock (_sync)
        {
            _chunks.Clear();
            _stream.Clear();
            _weaponOffset = -1;
            _targetItemId = 0;
            _active = false;

            if (!Enabled)
                return;

            var codeName = TargetCodeName;
            if (string.IsNullOrWhiteSpace(codeName))
                return;

            var record = Game.ReferenceManager.GetRefItem(codeName.Trim());
            if (record == null || record.ID == 0)
            {
                Log.Warn($"[WeaponPreview] Unknown item code: {codeName}");
                return;
            }

            _targetItemId = record.ID;
            _active = true;
        }
    }

    /// <summary>
    ///     Queues a chunk of the character data stream.
    /// </summary>
    /// <returns><c>true</c> if the packet was queued and must not be forwarded by the proxy.</returns>
    internal static bool Hold(Packet packet)
    {
        lock (_sync)
        {
            if (!_active)
                return false;

            var bytes = packet.GetBytes();
            if (bytes == null || bytes.Length == 0)
                return false;

            _chunks.Add(new HeldChunk(bytes, packet.Encrypted, packet.Massive));
            _stream.AddRange(bytes);

            return true;
        }
    }

    /// <summary>
    ///     Feeds the original character data to the bot handlers and then forwards
    ///     the (patched) packets to the client followed by the end packet.
    /// </summary>
    internal static void Finish(Packet endPacket)
    {
        List<HeldChunk> chunks;
        byte[] patched;
        uint targetItemId;
        int offset;

        lock (_sync)
        {
            _active = false;
            chunks = new List<HeldChunk>(_chunks);
            _chunks.Clear();
            patched = _stream.ToArray();
            _stream.Clear();
            targetItemId = _targetItemId;
            offset = _weaponOffset;
            _weaponOffset = -1;
            _targetItemId = 0;
        }

        try
        {
            if (chunks.Count == 0)
                return;

            // The bot must keep the truthful data so its own inventory stays correct.
            try
            {
                if (Game.ChunkedPacket != null)
                {
                    InventoryItem.ItemIdOffsetCallback = OnItemIdOffset;
                    PacketManager.CallHandler(endPacket, PacketDestination.Client);

                    lock (_sync)
                    {
                        offset = _weaponOffset;
                        _weaponOffset = -1;
                    }
                }
                else
                {
                    Log.Warn("[WeaponPreview] Character data begin packet was missed.");
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[WeaponPreview] Failed to parse character data: {ex.Message}");
            }
            finally
            {
                InventoryItem.ItemIdOffsetCallback = null;
            }

            if (targetItemId != 0 && offset >= 0 && offset + 4 <= patched.Length)
            {
                patched[offset] = (byte)(targetItemId & 0xFF);
                patched[offset + 1] = (byte)((targetItemId >> 8) & 0xFF);
                patched[offset + 2] = (byte)((targetItemId >> 16) & 0xFF);
                patched[offset + 3] = (byte)((targetItemId >> 24) & 0xFF);

                Log.Notify($"[WeaponPreview] Local weapon appearance replaced with {TargetCodeName}.");
            }
            else
            {
                Log.Warn("[WeaponPreview] Could not locate the equipped weapon, forwarding original data.");
            }

            var cursor = 0;
            foreach (var chunk in chunks)
            {
                var buffer = new byte[chunk.Bytes.Length];
                if (cursor + buffer.Length <= patched.Length)
                    Array.Copy(patched, cursor, buffer, 0, buffer.Length);
                else
                    Array.Copy(chunk.Bytes, buffer, buffer.Length);

                cursor += buffer.Length;

                PacketManager.SendPacket(
                    new Packet(ChunkOpcode, chunk.Encrypted, chunk.Massive, buffer),
                    PacketDestination.Client
                );
            }
        }
        finally
        {
            PacketManager.SendPacket(endPacket, PacketDestination.Client);

            try
            {
                PacketManager.CallCallback(endPacket);
            }
            catch (Exception ex)
            {
                Log.Warn($"[WeaponPreview] Callback failed: {ex.Message}");
            }
        }
    }

    private static void OnItemIdOffset(byte slot, int offset)
    {
        if (slot != WeaponSlot)
            return;

        lock (_sync)
        {
            if (_weaponOffset < 0)
                _weaponOffset = offset;
        }
    }

    private sealed class HeldChunk
    {
        public HeldChunk(byte[] bytes, bool encrypted, bool massive)
        {
            Bytes = bytes;
            Encrypted = encrypted;
            Massive = massive;
        }

        public byte[] Bytes { get; }
        public bool Encrypted { get; }
        public bool Massive { get; }
    }
}

/// <summary>
///     Resets the appearance override when a new character data stream begins.
/// </summary>
internal class WeaponAppearanceBeginHook : IPacketHook
{
    public ushort Opcode => 0x34A5;

    public PacketDestination Destination => PacketDestination.Client;

    public Packet ReplacePacket(Packet packet)
    {
        WeaponAppearanceOverride.Begin();

        return packet;
    }
}

/// <summary>
///     Holds the character data chunks so they can be patched before reaching the client.
/// </summary>
internal class WeaponAppearanceChunkHook : IPacketHook
{
    public ushort Opcode => 0x3013;

    public PacketDestination Destination => PacketDestination.Client;

    public Packet ReplacePacket(Packet packet)
    {
        if (!WeaponAppearanceOverride.Hold(packet))
            return packet;

        // The proxy will skip send/handler because we return null. Feed the original
        // data to the bot handlers now and forward the patched copy at the end.
        PacketManager.CallHandler(packet, PacketDestination.Client);
        PacketManager.CallCallback(packet);

        return null;
    }
}

/// <summary>
///     Patches the collected chunks and releases them to the client.
/// </summary>
internal class WeaponAppearanceEndHook : IPacketHook
{
    public ushort Opcode => 0x34A6;

    public PacketDestination Destination => PacketDestination.Client;

    public Packet ReplacePacket(Packet packet)
    {
        if (!WeaponAppearanceOverride.IsActive || !WeaponAppearanceOverride.HasChunks)
            return packet;

        WeaponAppearanceOverride.Finish(packet);

        return null;
    }
}
