# キャラクター移動の構成

## 確認した既存コード

- Character は通常の C# クラスで、IMovement.Move に委譲していた。
- Character の既存コンストラクタは3引数。IAttackSystem のフィールドは未初期化だった。
- MovementData.Direction は readonly フィールドで、値を渡すコンストラクタがなかった。
- CharacterMover に方向の加工、速度計算、Rigidbody 操作がまとまっていた。
- PlayerController は入力イベント方式だが、CharacterMover の初期化や Unity のライフサイクルを直接呼んでいた。
- Character の生成を担当する既存クラスはなかった。
- CharacterMover の使用箇所は PlayerController のみ。検索したシーン・PrefabにはスクリプトGUID参照がなかった。

## ファイルと責務

| ファイル | 変更 | 責務 |
| --- | --- | --- |
| CharacterComposition.cs | 追加 | Player/Enemy共通の組み立て役。CharacterDataとMotorを取得して依存を注入する |
| CharacterMovement.cs | 追加 | IMovement実装。方向の水平化・長さ制限・MoveSpeedによる速度計算 |
| CharacterMotor.cs | CharacterMoverを分割・置換 | Unity物理との境界。速度要求を保持し、FixedUpdateでRigidbodyへ適用 |
| PlayerController.cs | 変更 | 入力イベントをMovementDataに変換し、Character.Moveを呼ぶ |
| MovementData.cs | 変更 | readonly Directionを維持し、コンストラクタを追加 |
| Character.cs | 変更 | 既存3引数コンストラクタを保持し、IAttackSystemを注入できる4引数版を追加 |
| MovementSetup.md | 追加 | 構成・Inspector設定・速度APIの仕様 |

CharacterMover.csの重複実装は残さず、既存metaのGUIDをCharacterMotor.cs.metaへ引き継いだ。
InputBuffer、IMovement、CharacterData、HealthEntity、Statusは変更していない。
新規スクリプトにはmetaを追加した。

## 生成と依存注入

CharacterCompositionがCharacterMovement、HealthEntity、Status、Characterを生成する。
Characterはプロパティ経由の初回取得時にも生成でき、Awake順序に依存せず同じインスタンスを返す。
PlayerControllerに生成処理やCharacterData参照は持たせていない。

HealthEntityはInspectorのMax Health（初期値1）で生成する。
既存Statusの各値は現段階では0で初期化し、移動速度には使用しない。
移動速度はCharacterData.MoveSpeedだけから取得する。
攻撃システムは今回組み立てない。未設定のCharacter.Attack呼び出しは明示的な例外になる。
将来はCharacterの4引数コンストラクタにIAttackSystemを渡せる。

## 処理フロー

```text
Input System → InputBuffer.MoveAction
  → PlayerController (performed / canceled)
  → MovementData → Character.Move → IMovement.Move
  → CharacterMovement → CharacterMotor.SetLocomotionVelocity
  → CharacterMotor.FixedUpdate → Rigidbody.linearVelocity
```

毎Updateの入力ポーリングはない。OnEnable時だけ現在の入力を同期し、
Controllerだけを再有効化したときの押しっぱなし入力にも対応する。
OnDisableはイベント購読を解除し、Character経由で通常移動の停止を要求する。

CharacterMovementはUnityライフサイクルやGameObject検索を必要とせず、
コンストラクタで渡されたデータとMotorだけを使うためMonoBehaviourではない。
UnityのVector3・Mathfは利用するが、Rigidbody・入力・Player/Enemy固有クラスには依存しない。
CharacterもMonoBehaviour、GameObject、Rigidbody、入力クラスには依存しない。

## 速度APIと将来の接続

Rigidbodyの取得・速度書き込みを行うキャラクター移動クラスはCharacterMotorだけ。
通常移動はワールド座標のXZ平面。Yを除いてから方向をClampMagnitude(1)する。
これにより斜め移動の速度を制限し、1未満のアナログ入力は保持する。

| API | 仕様・接続先 |
| --- | --- |
| SetLocomotionVelocity | CharacterMovementから通常移動のXZ速度を設定 |
| AddExternalVelocity | 将来Knockback/Recoilから加算。XZは保持、Yは次のFixedUpdateで一度だけ加算 |
| ClearExternalVelocity | 保持中の外部XZ速度と未適用のY加算を解除。適用済みのY速度は変更しない |
| SetOverrideVelocity | 将来Dashから設定。XZの通常移動を置換し、外部速度は引き続き加算する |
| ClearOverrideVelocity | Dash終了時に解除。最後に要求された通常移動に戻る |

合成は (OverrideがあればOverride、それ以外はLocomotion) + External。
通常移動・OverrideのY入力は無視する。通常時はRigidbodyの現在のY速度を維持する。
外部速度の減衰やDashの時間管理は、将来の要求元が担当する。今回そのComponent本体は作成しない。
Motorを無効化すると保持中の要求をクリアする。Rigidbodyへの書き込みはFixedUpdateだけで行う。

Enemy側もCharacterComposition.Character.Move(new MovementData(direction))を呼べる。
Character、IMovement、CharacterMovement、CharacterMotor、CharacterDataは共通利用でき、
Enemy用に入力Componentや移動クラスの継承を追加する必要はない。

## Inspector設定

1. PlayerにPlayerControllerを追加する。依存ComponentとしてInputBuffer、PlayerInput、CharacterComposition、CharacterMotor、Rigidbodyが必要。
2. CharacterDataアセットのMove Speedを設定し、CharacterCompositionのCharacter Dataへ割り当てる。古いController/Moverにあったデータ参照はこのフィールドに設定する。
3. CharacterCompositionのMax Healthを設定する。攻撃・防御のパラメータ追加は今回行っていない。
4. PlayerInputへ既存InputActionアセットを割り当てる。MoveはVector2を返すValueアクションとする。
5. InputBufferの既存仕様に従い、Move、Interact、Dodge、Skill1～Skill6、Ultimate、Uniqueを用意する。
6. RigidbodyはIs Kinematicを無効にし、必要なColliderと回転制限を設定する。Y方向の重力を利用する場合、位置Yを固定しない。
7. EnemyにはCharacterCompositionとCharacterMotorを配置し、CharacterDataを割り当てる。PlayerController・InputBufferは不要。

## 検証

Unityの生成済みプロジェクトと実際の参照DLLを使用して全ランタイムスクリプトをコンパイルし、警告0件・エラー0件。
Rigidbody書き込みはCharacterMotorだけであること、旧CharacterMover参照が残っていないことを検索で確認した。
Unityバッチ起動は検証メソッド開始前に終了コード1で終了したため、実行時の入力・物理動作は未検証。
