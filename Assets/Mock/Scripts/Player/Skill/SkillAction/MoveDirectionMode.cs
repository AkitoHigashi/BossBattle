using System;
using UnityEngine;

/// <summary>
/// スキルの移動・攻撃・飛び道具の向きの決め方。
/// </summary>
public enum MoveDirectionMode
{
    /// <summary>キャラクターの現在の正面。</summary>
    Forward,
    /// <summary>現在のマウス照準の方向。</summary>
    AimDirection,
    /// <summary>キャラクターの現在の背面。</summary>
    Back,
    /// <summary>発動時に渡された方向（回避なら WASD の入力方向）。</summary>
    InputDirection
}

/// <summary>
/// MoveDirectionMode から実際のワールド方向を求める。
/// </summary>
/// <remarks>
/// 以前は BlinkAction・ComboAttackAction・HoldRepeatAttackAction・ProjectileAttackAction の4か所に
/// 同じ switch 文があり、向きの種類を増やすと4か所すべてを直す必要があった。ここに1つにまとめている。
/// </remarks>
public static class MoveDirectionResolver
{
    /// <summary>
    /// 向きを水平（Y=0）の方向として返す。正規化はしないので、使う側で必要に応じて行う。
    /// 方向が定まらない場合（マウスがキャラクターの真上など）は長さ 0 のベクトルになり得る。
    /// </summary>
    public static Vector3 Resolve(MoveDirectionMode mode, CharacterMotor motor, SkillExecution execution)
    {
        Vector3 forward = motor.transform.forward;
        forward.y = 0f;

        Vector3 direction = mode switch
        {
            MoveDirectionMode.Forward => forward,
            // 発動時ではなく現在の照準を使う。長押し中や連撃中にマウスを動かしても追従できる。
            MoveDirectionMode.AimDirection => execution.CurrentAimDirection,
            MoveDirectionMode.Back => -forward,
            // 発動時の方向。回避では PlayerController が WASD の方向を渡しているので、その方向になる。
            MoveDirectionMode.InputDirection => execution.Direction,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

        direction.y = 0f;
        return direction;
    }
}
