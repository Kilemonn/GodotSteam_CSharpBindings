using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using static Godot.HttpRequest;
using static GodotSteam.Steam;

namespace GodotSteam;

public static partial class Steam
{
    public enum LobbyState : long
    {
        NotConnected = 0,
        HostPending = 1,
        Hosting = 2,
        ClientPending = 3,
        Client = 4
    }

    public record CSteamID
    {
        // TODO, need Equals() method
        public CSteamID()
        {

        }
    }

    public class PingPacket
    {
        public int peer_id = -1;
        public CSteamID steam_id;

        public PingPacket()
        {
            
        }
    }

    public class Packet
    {
        public byte[] data;
        public CSteamID sender = new CSteamID();
        public int channel = 0;
        public int transfer_mode = 1; // Reliable;
        public Packet() { }
        public Packet(byte[] buffer, int transferMode, int packet_channel) 
        {
            data = buffer;
			sender = new CSteamID();
			channel = packet_channel;
			transfer_mode = transferMode;
		}
    }

    public enum ChannelManagement : long
    {
        PingChannel = 0,
        Size = 1
    }

    public partial class ConnectionData : RefCounted
    {
        public int peer_id;
        public CSteamID steam_id;
        public ulong last_msg_timestamp;
        // TODO:
        // public SteamNetworkingIdentity networkIdentity;
        public List<Packet> pending_retry_packets = new();

        public ConnectionData() { }

        public ConnectionData(CSteamID steamId)
        {
            peer_id = -1;
            steam_id = steamId;
            last_msg_timestamp = 0;
            // TODO
            // networkIdentity = SteamNetworkingIdentity();
            // networkIdentity.SetSteamID(steamId);
        }

        public bool Equals(ConnectionData other)
        {
            if (other is null)
            {
                return false;
            }

            return steam_id == other.steam_id;
        }

        ErrorResult RawSend(Packet packet)
        {

            if (packet.channel == ((int)ChannelManagement.PingChannel))
            {
                if (packet.data.Count != sizeof(PingPacket))
                {
                    return ErrorResult.Fail;
                }
            }
            // TODO
            return (ErrorResult)SendMessageToUser(0 /*packet.network*/, packet.data, packet.transfer_mode, packet.channel);
            // return SteamNetworkingMessages()->SendMessageToUser(networkIdentity, packet->data, packet->size, packet->transfer_mode, packet->channel);
        }

    }

    public partial class SteamMultiplayerPeer : Godot.MultiplayerPeerExtension
    {
        public static class Methods
        {
            public static readonly StringName CreateLobby = "create_lobby";
            public static readonly StringName ConnectLobby = "connect_lobby";

            public static readonly StringName GetState = "get_state";
            public static readonly StringName GetLobbyId = "get_lobby_id";
            public static readonly StringName CollectDebugData = "collect_debug_data";

            public static readonly StringName GetNoNangle = "get_no_nagle";
            public static readonly StringName SetNoNagle = "set_no_nagle";
            // ADD_PROPERTY(PropertyInfo(Variant::BOOL, "no_nagle"), "set_no_nagle", "get_no_nagle");

            public static readonly StringName GetNoDelay = "get_no_delay";
            public static readonly StringName SetNoDelay = "set_no_delay";
            // ADD_PROPERTY(PropertyInfo(Variant::BOOL, "no_delay"), "set_no_delay", "get_no_delay");

            public static readonly StringName GetAsRelay = "get_as_relay";
            public static readonly StringName SetAsRelay = "set_as_relay";
            // ADD_PROPERTY(PropertyInfo(Variant::BOOL, "as_relay"), "set_as_relay", "get_as_relay");

            public static readonly StringName GetSteam64FromPeerId = "get_steam64_from_peer_id";
            public static readonly StringName GetPeerIdFromSteam64 = "get_peer_id_from_steam64";
            public static readonly StringName GetPeerMap = "get_peer_map";

            public static readonly StringName SendDirectMessage = "send_direct_message";
            public static readonly StringName GetDirectMessages = "get_direct_messages";

            public static readonly StringName GetLobbyData = "get_lobby_data";
            public static readonly StringName SetLobbyData = "set_lobby_data";
            public static readonly StringName GetAllLobbyData = "get_all_lobby_data";
            public static readonly StringName SetLobbyJoinable = "set_lobby_joinable";
        }

        public static class Signals
        {
            public static readonly StringName FavoritesListAccountsUpdated = "favorites_list_accounts_updated";
            public static readonly StringName FavoritesListChanged = "favorites_list_changed";

            public static readonly StringName LobbyMessage = "lobby_message";
            public static readonly StringName LobbyChatUpdate = "lobby_chat_update";
            public static readonly StringName LobbyCreated = "lobby_created";
            public static readonly StringName LobbyDataUpdate = "lobby_data_update";
            public static readonly StringName LobbyJoined = "lobby_joined";
            public static readonly StringName LobbyGameCreated = "lobby_game_created";
            public static readonly StringName LobbyInvite = "lobby_invite";
            public static readonly StringName LobbyMatchList = "lobby_match_list";
            public static readonly StringName LobbyKicked = "lobby_kicked";

            public static readonly StringName NetworkSessionFailed = "network_session_failed";

            public static readonly StringName DebugData = "debug_data";
        }

        public LobbyState LobbyState { get; private set; }

        private List<Packet> incomingPackets = new();

        private ulong lobbyId = 0;

        private int uniqueId = -1;

        private static readonly int MAX_SEND_SIZE = 512 * 1024; // Gotten from steamnetworkingtypes.h "k_cbMaxSteamNetworkingSocketsMessageSizeSend"

        private ulong steamId = GetSteamID();

        private int targetPeer = -1;

        public SteamMultiplayerPeer()
        {
            
        }

        public override void _Close()
        {
            LeaveLobby(lobbyId);
            LobbyState = LobbyState.NotConnected;
        }

        public override void _DisconnectPeer(int pPeer, bool pForce)
        {
            throw new NotImplementedException();
        }

        public override int _GetAvailablePacketCount()
        {
            return incomingPackets.Count;
        }

        public override ConnectionStatus _GetConnectionStatus()
        {
            if (LobbyState == LobbyState.NotConnected)
            {
                return ConnectionStatus.Disconnected;
            }
            else if (LobbyState == LobbyState.Client || LobbyState == LobbyState.Hosting)
            {
                return ConnectionStatus.Connected;
            }
            else
            {
                return ConnectionStatus.Connecting;
            }
        }

        public override int _GetMaxPacketSize()
        {
            return MAX_SEND_SIZE;
        }

        public override int _GetPacketChannel()
        {
            return base._GetPacketChannel();
        }

        public override TransferModeEnum _GetPacketMode()
        {
            return base._GetPacketMode();
        }

        public override int _GetPacketPeer()
        {
            return base._GetPacketPeer();
        }
        
        public override byte[] _GetPacketScript()
        {
            return base._GetPacketScript();
        }

        public override Array<Dictionary> _GetPropertyList()
        {
            return base._GetPropertyList();
        }

        public override int _GetTransferChannel()
        {
            return base._GetTransferChannel();
        }

        public override TransferModeEnum _GetTransferMode()
        {
            return base._GetTransferMode();
        }

        public override int _GetUniqueId()
        {
            return base._GetUniqueId();
        }

        public override bool _IsRefusingNewConnections()
        {
            return base._IsRefusingNewConnections();
        }

        public override bool _IsServer()
        {
            return 1 == uniqueId;
        }

        public override bool _IsServerRelaySupported()
        {
            return base._IsServerRelaySupported();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
        }

        public override void _Poll()
        {
            base._Poll();
        }

        public override bool _PropertyCanRevert(StringName property)
        {
            return base._PropertyCanRevert(property);
        }

        public override Variant _PropertyGetRevert(StringName property)
        {
            return base._PropertyGetRevert(property);
        }

        public override Error _PutPacketScript(byte[] pBuffer)
        {
            // int transferMode =
            return base._PutPacketScript(pBuffer);
        }

        public override void _SetRefuseNewConnections(bool pEnable)
        {
            base._SetRefuseNewConnections(pEnable);
        }

        public override void _SetTargetPeer(int pPeer)
        {
            targetPeer = pPeer;
        }

        public override void _SetTransferChannel(int pChannel)
        {
            base._SetTransferChannel(pChannel);
        }

        public override void _SetTransferMode(TransferModeEnum pMode)
        {
            base._SetTransferMode(pMode);
        }

        public override void _ValidateProperty(Dictionary property)
        {
            base._ValidateProperty(property);
        }
    }

}


