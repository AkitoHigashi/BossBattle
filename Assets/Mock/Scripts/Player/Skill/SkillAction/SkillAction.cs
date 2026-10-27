using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

/// <summary>
/// 個々のスキル動作の共通基底クラス。
/// </summary>
public abstract class SkillAction : ScriptableObject
{
    /// <summary>
    /// スキル動作を実行する。
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public abstract UniTask ExecuteAsync(SkillContext context, SkillExecution execution);
}
