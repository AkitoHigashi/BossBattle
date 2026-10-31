# ロビーの操作

1. `Assets/Mock/Scenes/Net/Yajima/NetTest.unity`を再生します。
2. ホストはHOST、参加者はホストのIPと同じPORTでCLIENTを押します。
3. CREATE ROOMまたは一覧のJOINで、RoomLobbyシーンへ移動します。
4. CHARACTER A/B/C/Dから仮キャラクターを選びます。参加者一覧に全員の選択とReady状態が表示されます。
5. 全員がREADYを押したら、[OWNER]の人がSTART GAMEを押します。全員がIngameTestへ移動します。
6. 開始前はLEAVE ROOMで部屋一覧へ戻れます。接続は維持されます。

キャラクターを変更するとReadyは解除されます。退出した所有者の権限は残ったメンバーへ移ります。ホストアプリの終了時は全員切断されます。
選択はCharacterSelectionManagerが管理し、各端末の表示へBroadcastで反映します。Ingameの生成キャラクターへの連携は未実装です。

