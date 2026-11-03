using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NCG.Character.Selection
{
    /// <summary>キャラクター選択を管理するクラス</summary>
    public class CharacterSelectionManager : MonoBehaviour
    {
        [SerializeField] CharacterType _defaultCharacterType = CharacterType.CharacterA;
        /// <summary>サーバーが保持する正式なキャラクター選択状況</summary>
        readonly Dictionary<int, CharacterType> _selections = new();
        NetworkManager _network;
        bool _registered;

        /// <summary>キャラクター選択状況が更新されたときに発火するイベント</summary>
        public event Action<CharacterSelectionEntry[]> OnSelectionChanged;

        /// <summary>クライアントの状態を判定して入力送信ができるかを返すプロパティ</summary>
        bool CanSend => _registered && _network != null && _network.ClientManager.Started && _network.ClientManager.Connection.IsAuthenticated;

        private void Start()
        {
            _network = GetComponent<NetworkManager>();
            if (_network == null) _network = InstanceFinder.NetworkManager;
            if (_network == null)
            {
                Debug.LogError("NetworkManagerがありません", this);
                return;
            }

            _registered = true;
            // 購読
            // サーバーでは更新リクエストを受ける
            _network.ServerManager.RegisterBroadcast<CharacterSelectRequest>(OnSelectRequested);
            // キャラクター選択状況一覧を取得する
            _network.ServerManager.RegisterBroadcast<CharacterSelectionListRequest>(OnSelectionReceived);
            // クライアントでは更新通知を受ける
            _network.ClientManager.RegisterBroadcast<CharacterSelectionList>(OnList);
            // サーバーの状況が変わったときの通知を受ける
            _network.ServerManager.OnServerConnectionState += OnServerConnectionState;
        }

        private void OnDestroy()
        {
            if (!_registered || _network == null) return;
            // 解除
            _network.ServerManager.UnregisterBroadcast<CharacterSelectRequest>(OnSelectRequested);
            _network.ServerManager.UnregisterBroadcast<CharacterSelectionListRequest>(OnSelectionReceived);
            _network.ClientManager.UnregisterBroadcast<CharacterSelectionList>(OnList);
            _network.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        }

        // クライアントからサーバーへ
        #region Client => Server
        /// <summary>
        /// キャラクター選択をリクエストするメソッド
        /// </summary>
        /// <param name="characterType">選択したキャラクター</param>
        public void SelectCharacter(CharacterType characterType)
        {
            if (!CanSend) return;

            // サーバーに選択更新リクエスト
            _network.ClientManager.Broadcast(new CharacterSelectRequest(characterType));
        }

        /// <summary>
        /// 表示を更新をリクエストするメソッド
        /// </summary>
        public void UpdateCharacterSelections()
        {
            if (!CanSend) return;

            // サーバーに表示更新リクエスト
            _network.ClientManager.Broadcast(new CharacterSelectionListRequest());
        }

        /// <summary>
        /// 各クライアントでUIを更新するメソッド
        /// </summary>
        /// <param name="characterSelectionList">キャラクター選択状況一覧</param>
        /// <param name="channel">デリゲートを合わせるためだけの不要な引数</param>
        void OnList(CharacterSelectionList characterSelectionList, Channel channel)
        {
            // イベント発火でUI更新
            OnSelectionChanged?.Invoke(characterSelectionList.Entries);
        }
        #endregion

        // サーバーからクライアントへ
        #region Server => Client
        /// <summary>
        /// 部屋に参加した時に呼ばれるメソッド
        /// </summary>
        /// <param name="clientId">参加したクライアントのID</param>
        public void JoinRoom(int clientId)
        {
            // 誰かが抜けたことによる通知のためIsAuthenticatedによる認証判定は不要
            // とはいえ準備が整っていなければreturn
            if (!_registered || _network == null || !_network.ServerManager.Started) return;

            // 入室の重複通知の場合はreturn
            if (_selections.ContainsKey(clientId)) return;

            // 選択処理を正常に実行できなかったらreturn
            if (!TryUpdateSelection(_defaultCharacterType, clientId)) return;

            // 各クライアントに表示更新通知
            UpdateCharacterSelectionsRequested();
        }

        /// <summary>
        /// 選択更新リクエストを処理するメソッド
        /// <para>Broadcastによってサーバーでのみ実行される</para>
        /// </summary>
        /// <param name="sender">選択更新を行ったプレイヤーの情報</param>
        /// <param name="message">選択更新リクエスト</param>
        /// <param name="channel">デリゲートを合わせるためだけの不要な引数</param>
        void OnSelectRequested(NetworkConnection sender,
            CharacterSelectRequest message,
            Channel channel)
        {
            var characterType = message.CharacterType;
            var clientId = sender.ClientId;

            // 選択処理を正常に実行できなかったらreturn
            if (!TryUpdateSelection(characterType, clientId)) return;

            // 各クライアントに表示更新通知
            UpdateCharacterSelectionsRequested();
        }

        /// <summary>
        /// 選択処理を実行するメソッド
        /// </summary>
        /// <param name="characterType">選択したキャラクターの種類</param>
        /// <param name="clientId">キャラクターを選択したプレイヤー</param>
        /// <returns>選択処理を正常に実行できたか</returns>
        bool TryUpdateSelection(CharacterType characterType, int clientId)
        {
            if (!Enum.IsDefined(typeof(CharacterType), characterType)) return false;

            // 現在の選択と同じなら更新をスルー
            if (_selections.TryGetValue(clientId, out var current)
                && current == characterType)
            {
                return false;
            }

            // 最新状態に更新
            _selections[clientId] = characterType;
            return true;
        }

        /// <summary>
        /// 選択更新リクエストを行ったプレイヤーに更新通知を出すメソッド
        /// </summary>
        /// <param name="sender">選択更新リクエストを行ったプレイヤーの情報</param>
        /// <param name="message">選択更新通知</param>
        /// <param name="channel">デリゲートを合わせるためだけの不要な引数</param>
        void OnSelectionReceived(NetworkConnection sender, CharacterSelectionListRequest message, Channel channel)
        {
            // 更新リクエストを行ったプレイヤーにのみ更新通知
            _network.ServerManager.Broadcast(sender, CreateList());
        }

        /// <summary>
        /// キャラクター選択状況一覧の更新リクエストをするメソッド
        /// </summary>
        void UpdateCharacterSelectionsRequested()
        {
            // 各クライアントに表示更新通知
            _network.ServerManager.Broadcast(CreateList());
        }

        /// <summary>
        /// 更新データ一覧を作成するメソッド
        /// </summary>
        /// <returns>更新データ一覧</returns>
        CharacterSelectionList CreateList()
        {
            return new CharacterSelectionList(_selections
                .Select(element => new CharacterSelectionEntry(element.Key, element.Value))
                .ToArray());
        }

        /// <summary>
        /// 部屋から退出した時に呼ばれるメソッド
        /// </summary>
        /// <param name="clientId">退出したクライアントのID</param>
        public void LeaveRoom(int clientId)
        {
            // 誰かが抜けたことによる通知のためIsAuthenticatedによる認証判定は不要
            // とはいえ準備が整っていなければreturn
            if (!_registered || _network == null || !_network.ServerManager.Started) return;

            // 辞書から削除
            if (_selections.Remove(clientId))
                UpdateCharacterSelectionsRequested();
        }
        #endregion

        // 接続状況に応じたコールバック
        #region ConnectionCallBack
        /// <summary>
        /// サーバーの状態が変わったときに辞書の情報を更新するメソッド
        /// </summary>
        /// <param name="args">サーバー状態</param>
        void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            // サーバーが停止していなかったらreturn
            if (args.ConnectionState != LocalConnectionState.Stopped
                || _network.ServerManager.Started) return;

            // サーバー停止時に辞書をリセット
            _selections.Clear();
        }
        #endregion

        /// <summary>
        /// クライアントに合わせたキャラクターの種類を取得するメソッド
        /// </summary>
        /// <param name="clientId">クライアントID</param>
        /// <param name="characterType">キャラクターの種類</param>
        /// <returns>キャラクターの種類を取得できたかどうか</returns>
        public bool TryGetCharacterType(int clientId, out CharacterType characterType)
        {
            return _selections.TryGetValue(clientId, out characterType);
        }
    }
}

