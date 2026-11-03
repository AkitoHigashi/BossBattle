# IP接続テストの操作とクラスの役割

1. `Assets/Mock/Scenes/Net/Yajima/NetTest.unity`を再生します。
2. ホストは部屋名・定員・PORTを入力し「部屋を作成」を押します。サーバー起動と自身の接続・認証の後、RoomLobbyへ移動します。
3. 参加者はホストのIPと同じPORTを指定し「部屋に参加」を押します。接続・認証・入室の後、RoomLobbyへ移動します。同一PCのPlayer 2は127.0.0.1を使用できます。
4. 入室時にホスト側のデフォルトCharacterTypeが設定されます。CHARACTER A/B/C/Dで変更でき、参加者一覧に全員の選択とReady状態が表示されます。
5. 全員がREADYを押したら、[OWNER]の人がSTART GAMEを押します。全員がIngameTestへ移動します。
6. 参加者は退出するとホストとの接続を切って作成・参加画面へ戻ります。ホストは部屋を解体するとサーバーを停止し、全員を切断します。ゲーム中も右上のUIから操作できます。

キャラクターを変更するとReadyは解除されます。現在は1ホスト1部屋で、ホスト退出時の引き継ぎは行いません。

選択辞書はCharacterSelectionManager、入退出・UIとの連携はLobbyCharacterSelectionBridgeが担当します。
生成側のNCG.Player.Spawn.PlayerSpawnManagerは選択辞書からCharacterTypeを取得し、CharacterDataListで対応プレハブを選びます。利用には生成側の参照設定が必要です。

左上の常設デバッグUIでは実辞書と接続状態を確認できます。見出しをドラッグすると移動できます。
現在の部屋は独自のRoomManagerで管理しており、EOSロビーの検索・参加はまだ連携していません。

接続管理、部屋管理、キャラクター選択、UI、プレイヤー生成の詳しい役割と処理の流れは、
`Assets/Mock/Scripts/Network/Yajima/RoomConnectionLifecycle.md` を参照してください。

