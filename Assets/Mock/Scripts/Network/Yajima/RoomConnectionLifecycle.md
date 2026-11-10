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

## クラスの役割

現在はEOSロビーを使わず、独自のRoomManagerで部屋を管理するIP接続テスト。
FishNetの接続・認証、部屋への所属、キャラクター選択、画面表示を別の責務として扱う。
ホストではサーバー側とクライアント側の両方の処理が同じUnityアプリ内で動く。

### 部屋・接続・シーン

配置先：`Assets/Mock/Scripts/Network/Yajima`

| クラス | 担当 | 主な処理 |
| --- | --- | --- |
| RoomConnectionLifecycle | 部屋の寿命とFishNet接続の寿命を合わせる | 作成時にホスト起動・認証・部屋作成を順に進める。参加時に接続・認証・入室を進める。退出は本人の切断、解体はサーバー停止。失敗・キャンセル・タイムアウトも処理する |
| RoomManager | サーバーが保持する正式な部屋情報を管理する | 所属、定員、オーナー、Ready、開始状態を管理。依頼の可否を判定し、RoomStateを配信する。所属確定後にMemberJoined、退出時にMemberLeftを発火する |
| RoomClient | クライアントから部屋を操作する窓口 | RoomRequestを送信し、応答・部屋一覧・現在の所属を受け取る。HasRoomとCurrentRoomを保持し、StateChangedなどで利用側へ知らせる |
| RoomConnectionSceneNavigation | 接続の状態に合わせてシーンを移動する | 入室後はロビー、ゲーム開始時は対象参加者をFishNet経由でIngameTestへ移動。接続終了後は作成・参加画面へ戻す。ローカルのシーンロードを直列化する |

RoomClient.StateChangedは入室専用イベントではなく、Ready変更や他メンバーの参加・退出でも発火する。
サーバーで誰の入退出かを知る用途にはRoomManager.MemberJoined/MemberLeftを使用する。
認証完了はClientManager.OnAuthenticatedで通知されるが、部屋への所属完了とは別。

### キャラクター選択

配置先：`Assets/Mock/Scripts/Character/CharacterSelection`

| クラス・インターフェース | 担当 | 主な処理 |
| --- | --- | --- |
| CharacterSelectionManager | サーバーの正式なキャラクター選択データと、その通信を管理する | `_selections`にClientIdとCharacterTypeの対応を保持。JoinRoomでサーバー側のデフォルト値を登録し、LeaveRoomで削除。選択依頼を受信して値を更新し、一覧を配信する。サーバー停止時に辞書をクリアする |
| LobbyCharacterSelectionBridge | 部屋・キャラクター選択・UIを連携する | MemberJoinedをJoinRoomへ、MemberLeftをLeaveRoomへ渡す。選択結果をRoomManagerの表示・Ready管理にも反映する。受信一覧をUI向けのChangedイベントへ変換する |
| ILobbyCharacterSelection | UIから利用するキャラクター選択の窓口 | Select、RequestList、Changedを定義する。UIは具体的なCharacterSelectionManagerを直接参照せず、このインターフェースを使う。定義ファイルはNetwork/Yajimaにある |
| CharacterSelectionDictionaryDebugUI | ローカルプロセス内の実辞書を観察する | 初期状態は非表示。Inspector の Show Dictionary を有効にすると、配置したシーンがアクティブな間だけ件数、全キー・値、接続状態、変更履歴を表示する |

CharacterTypeは選択できる種類を表す列挙型で、`Assets/Mock/Scripts/Character/CharacterType.cs`にある。

CharacterSelectionManagerは部屋への参加可否やシーン移動を決めない。
入退出の意味は呼び出し側が判断し、サーバー側でJoinRoom/LeaveRoomを呼ぶ。
JoinRoomは重複通知で既存の選択を上書きせず、新規登録した場合に一覧を配信する。
現在はホスト側の`_defaultCharacterType`を全参加者の初期値として使用する。

| メソッド | 実行する側 | 必要な条件と役割 |
| --- | --- | --- |
| SelectCharacter / UpdateCharacterSelections | 各クライアント | 接続・認証完了後、サーバーへ選択変更・一覧取得を依頼する |
| JoinRoom / LeaveRoom | サーバー | 外部から入退出通知を受け、指定ClientIdの登録・削除を行う。ホスト自身のクライアント認証には依存しない |
| OnSelectRequested / OnSelectionReceived | サーバーの受信処理 | ServerManager.RegisterBroadcastへの登録によって呼ばれる。メソッド名自体が実行先を決めるわけではない |
| OnList | 各クライアントの受信処理 | 配信された一覧を受け、OnSelectionChangedを発火する。サーバーの実辞書をクライアントへそのまま持たせる処理ではない |
| TryGetCharacterType | 選択データの参照側 | 指定ClientIdの確定済み選択を取得する。ゲーム中の生成側はサーバー上で使用する |

### UI

| クラス | 担当 |
| --- | --- |
| RoomConnectionBrowserUI | 名前・定員・IP・ポートを入力し、RoomConnectionLifecycleへ作成・参加・キャンセルを依頼する。待機中はFishNetへ接続しない |
| RoomLobbyUI | 部屋名、参加者、選択キャラクター、Readyを表示。キャラクター選択はILobbyCharacterSelection、Readyと開始はRoomClientへ依頼する。退出ボタンはRoomConnectionLifecycleが退出・解体へ接続する |
| IngameRoomUI | ゲーム中の参加者と選択キャラクターを表示。本人・オーナーの区別、退出・解体の確認を表示し、RoomConnectionLifecycleへ操作を渡す |

UIは操作の入力と表示を担当し、正式な部屋情報や選択辞書の更新は各管理クラスに任せる。

### 通信でやり取りするデータ

以下は処理を管理するクラスではなく、Broadcastで運ぶ構造体。

| 型 | 内容 |
| --- | --- |
| RoomRequest / RoomReply | 部屋の操作依頼と、その成功・失敗の応答 |
| RoomInfo / RoomMember | 部屋情報と参加者情報。RoomMemberには表示・Ready管理用のCharacterも含む |
| RoomList / RoomState | 部屋一覧と、受信者が所属する部屋の状態 |
| CharacterSelectRequest | クライアントが選んだCharacterType。送信者のClientIdはサーバー側で接続情報から取得する |
| CharacterSelectionListRequest | 選択一覧の取得依頼 |
| CharacterSelectionEntry | 1人分のClientIdとCharacterType |
| CharacterSelectionList | サーバーが配信するCharacterSelectionEntryの一覧 |

### プレイヤー生成側

配置先：`Assets/Mock/Scripts/Player/Spawn`

| クラス・型 | 担当 |
| --- | --- |
| NCG.Player.Spawn.PlayerSpawnManager | サーバーでシーンへの参加確定を受け、CharacterSelectionManagerからその人の選択を取得。対応するプレハブを生成し、FishNetで所有権を付けてSpawnする |
| CharacterDataList | CharacterTypeとNetworkObjectプレハブの対応を持つScriptableObject |
| INetworkObjectSpawnable | 生成処理の共通窓口。Spawn(prefab)を定義する |
| PlayerSpawner | プレハブをInstantiateしてNetworkObjectを返す。FishNetへのSpawn登録はPlayerSpawnManagerが行う |
| PlayerInfo | 生成対象のNetworkConnectionを保持する構造体 |

生成側には、CharacterDataListへのプレハブ対応と生成コンポーネントの設定が必要。
NetworkTest_Yajima名前空間にも同名のPlayerSpawnManagerがあるが、そちらは固定プレハブを生成する旧テスト用。

## 主な処理の流れ

### 入室と初期選択

```text
部屋作成／参加の操作
  → RoomConnectionLifecycleが接続・認証後にRoomClientへ依頼
  → RoomManagerがサーバー上で所属を確定
  → MemberJoined(clientId)
  → LobbyCharacterSelectionBridge
  → CharacterSelectionManager.JoinRoom(clientId)
  → デフォルト選択を登録し、CharacterSelectionListを配信
  → 各クライアントでOnSelectionChanged → Bridge.Changed → UI更新
```

Bridgeは同じ初期選択をRoomManagerにも反映し、RoomInfoと選択一覧を一致させる。

### 手動のキャラクター変更

```text
RoomLobbyUI → ILobbyCharacterSelection.Select
  → Bridge → CharacterSelectionManager.SelectCharacter
  → ClientManager.Broadcast(CharacterSelectRequest)
  → サーバーのOnSelectRequested → 選択辞書を更新 → 一覧を配信
  → 各クライアントのOnList → UI更新
```

Bridgeもサーバーで選択依頼を受け、RoomManagerへ反映して対象者のReadyを解除する。

### 退出・解体

参加者の通常退出と突然の切断はRoomManagerが所属を削除し、MemberLeftを通知する。
BridgeがCharacterSelectionManager.LeaveRoomへ渡し、選択辞書から対象IDを削除する。
ホストが解体した場合はサーバーが停止し、CharacterSelectionManagerの停止通知で辞書全体をクリアする。

## 従来構成とEOSへの移行

- NetTestUI / RoomSceneTransition：従来の接続維持方式。現在のNetTestConnection.prefabでは、RoomConnectionBrowserUI / RoomConnectionSceneNavigationへ参照を差し替えている。
- IngameRoomExitCoordinator：従来のゲーム中退出用。RoomConnectionLifecycleがある場合は動作しない。
- 旧NetTestSceneBuilderの再生成メニューは従来構成を生成するため、現在の構成の再生成には使わない。
- 将来EOSロビーを使う場合は、EOSの入退出とFishNetの準備完了を連携する部分を用意する。EOSロビー参加だけではFishNet認証完了を意味しない。
- CharacterSelectionManagerは入退出通知の呼び出し元をEOSへ置き換えても、サーバー上の選択管理を引き続き担当できる。EOSのPUIDとFishNetのClientIdは別の識別子なので、対応付けが必要。

## 確認

Multiplayer Play ModeのPlayer 2を起動して、Play停止中に
`Tools > Yajima > Check Room Connection Lifecycle (Player 2)` を実行。
結果は `Temp/RoomLifecycleCheck.result` に保存する。

確認対象：未接続での初期表示、不正な部屋名で起動しないこと、UIからの作成と参加、
ロビー・ゲームそれぞれで参加者の退出とホスト解体、解体後の再作成、接続キャンセル。
既存の「接続維持」を前提とする旧スモークチェックとは期待する動作が異なる。
