using System;
using System.Collections.Generic;
using System.Linq;
using NetworkTest_Yajima;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
namespace NCG.Character.Selection
{
    [RequireComponent(typeof(CharacterSelectionManager))]
    public sealed class LobbyCharacterSelectionBridge : MonoBehaviour, ILobbyCharacterSelection
    {
        private CharacterSelectionManager _selection;
        private RoomManager _rooms;
        private NetworkManager _network;
        private RoomClient _client;
        public event Action<IReadOnlyDictionary<int, string>> Changed;
        private void Awake()
        {
            _selection = GetComponent<CharacterSelectionManager>();
            _rooms = GetComponent<RoomManager>();
            _network = GetComponent<NetworkManager>();
            _client = GetComponent<RoomClient>();
            _selection.OnSelectionChanged += Receive;
            _rooms.MemberLeft += _selection.LeaveRoom;
        }
        private void Start()
        {
            // 選択管理の公開APIを変えずに、ロビー側のReady解除だけを連携する。
            _network.ServerManager.RegisterBroadcast<CharacterSelectRequest>(OnSelectRequested);
        }
        public void Select(int character)
        {
            if (!_client.HasRoom || _client.CurrentRoom.HasStarted) return;
            if (!Enum.IsDefined(typeof(CharacterType), (CharacterType)character)) return;
            _selection.SelectCharacter((CharacterType)character);
        }
        public void RequestList() => _selection.UpdateCharacterSelections();
        private void Receive(CharacterSelectionEntry[] entries) => Changed?.Invoke(entries.ToDictionary(e => e.ClientId, e => e.CharacterType.ToString()));
        private void OnSelectRequested(NetworkConnection sender, CharacterSelectRequest message, Channel channel)
        {
            if (!Enum.IsDefined(typeof(CharacterType), message.CharacterType)) return;
            _rooms.ApplyCharacterSelection(sender.ClientId, (int)message.CharacterType);
        }
        private void OnDestroy()
        {
            if (_rooms != null && _selection != null)
                _rooms.MemberLeft -= _selection.LeaveRoom;
            if (_selection != null)
            {
                _selection.OnSelectionChanged -= Receive;
            }
            if (_network != null && _network.Initialized)
                _network.ServerManager.UnregisterBroadcast<CharacterSelectRequest>(OnSelectRequested);
        }
    }
}
