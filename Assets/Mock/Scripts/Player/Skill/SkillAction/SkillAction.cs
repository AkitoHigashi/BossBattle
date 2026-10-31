using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 個々のスキル動作の共通基底クラス。
/// 移動・攻撃・溜めなどの動作を1つずつ派生クラスにし、SkillDefinition の Actions に並べて組み合わせる。
/// </summary>
/// <remarks>
/// 派生クラスを書くときの約束：
/// ・ScriptableObject は共有されるため、実行中の状態をフィールドに保存しない（ローカル変数か SkillExecution に持つ）。
/// ・await には execution.Token を渡し、中断されたら速やかに OperationCanceledException で抜ける。
/// ・攻撃判定や移動の Override など自分が変えた状態は、try/finally で必ず元に戻す。
/// </remarks>
public abstract class SkillAction : ScriptableObject
{
    /// <summary>
    /// スキル動作を実行する。動作が終わったら完了する。
    /// </summary>
    /// <param name="context">キャラクターごとの依存先（移動・Animator・攻撃イベント）。</param>
    /// <param name="execution">この1回の実行の状態（中断用Token・入力・照準・倍率）。</param>
    /// <returns>動作の完了。中断時は OperationCanceledException で終わる。</returns>
    public abstract UniTask ExecuteAsync(SkillContext context, SkillExecution execution);
}
