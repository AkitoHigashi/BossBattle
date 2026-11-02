# IP接続テストの部屋と接続の寿命

NetTest.unityから実行する。NetTestConnection.prefabは1ホスト1部屋の構成。

| 操作 | 動作 |
| --- | --- |
| 部屋を作成 | 名前・定員・ポートを検証 → サーバー起動 → ホスト自身のクライアント接続・認証 → 部屋作成 → ロビー |
| 部屋に参加 | 指定IP・ポートへクライアント接続・認証 → 参加可能な部屋を確認 → 入室 → ロビー |
| 参加者が退出 | 退出依頼 → クライアント切断 → 作成・参加画面へ戻る。他の参加者は維持 |
| ホストが解体 | ホスト自身の接続とサーバーを停止 → 全参加者の接続終了 → 各端末が作成・参加画面へ戻る |
| 接続失敗・拒否・キャンセル | 開始した接続を片付ける。ホスト作成失敗ならサーバーも停止 |

同一PCのPlayer 2は127.0.0.1へ接続する。別PCではホストPCのLAN内IPと同じポートを指定する。
一覧画面にいるだけではFishNetに接続しない。接続済みでの部屋切り替えは行わない。
接続・認証・入室・退出待ちは15秒でタイムアウト。再試行はTransportの停止完了後に可能。
EOSのロビー検索やPUID接続は今回の変更対象外。

## 実装

- RoomConnectionLifecycle: 状態遷移、接続・停止、作成・参加の連続処理。
- RoomConnectionSceneNavigation: ロビー・ゲーム・作成画面の遷移。ローカルロードを直列化。
- RoomConnectionBrowserUI: 既存Canvasの部屋作成・参加操作を接続寿命へ連携。
- RoomClient/RoomManager/RoomLobbyUI/NetTestUI/RoomSceneTransitionのクラス本体は変更しない。
  プレハブのUI・シーン遷移スクリプトの参照を新クラスへ差し替える。
- IngameRoomUIとIngameRoomExitCoordinatorは今回までに追加したクラス。
  新構成では旧来の接続維持用退出処理を無効にする。
- 旧NetTestSceneBuilderの再生成メニューは従来の接続テストを生成するため、この構成の再生成には使わない。

## 確認

Multiplayer Play ModeのPlayer 2を起動して、Play停止中に
`Tools > Yajima > Check Room Connection Lifecycle (Player 2)` を実行。
結果は `Temp/RoomLifecycleCheck.result` に保存する。

確認対象：未接続での初期表示、不正な部屋名で起動しないこと、UIからの作成と参加、
ロビー・ゲームそれぞれで参加者の退出とホスト解体、解体後の再作成、接続キャンセル。
既存の「接続維持」を前提とする旧スモークチェックとは期待する動作が異なる。
