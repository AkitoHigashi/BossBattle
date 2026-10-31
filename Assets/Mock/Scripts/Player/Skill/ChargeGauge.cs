using UnityEngine;

/// <summary>
/// 溜めゲージの表示用データ（キャラクターごとに1つ）。
/// ChargeAction が書き込み、ChargeGaugeView が毎フレーム読んで描画する。
/// </summary>
/// <remarks>
/// 表示（uGUI）とデータを分けているのは、Action 側を UI の作りに依存させないため。
/// UI の見た目を作り直しても ChargeAction は変更不要で、逆に UI なしでも溜め処理は動く。
/// 値はすべてゲージ上の位置として 0〜1 に正規化して渡し、秒数の解釈は ChargeAction に閉じ込めている。
/// </remarks>
public sealed class ChargeGauge
{
    /// <summary>ゲージを表示中か。溜めの開始で true、終了・中断で false になる。</summary>
    public bool IsVisible { get; private set; }
    /// <summary>現在の溜め量（0〜1）。</summary>
    public float Progress { get; private set; }
    /// <summary>ジャストゾーンを表示するか（溜め斬りつけのみ）。</summary>
    public bool HasJustZone { get; private set; }
    /// <summary>ジャストゾーンの開始位置（0〜1）。</summary>
    public float JustZoneStart { get; private set; }
    /// <summary>ジャストゾーンの終了位置（0〜1）。</summary>
    public float JustZoneEnd { get; private set; }
    /// <summary>今離せばジャスト成功になる位置にいるか。表示の色分けに使う。</summary>
    public bool IsInJustZone => HasJustZone && Progress >= JustZoneStart && Progress <= JustZoneEnd;

    /// <summary>
    /// ゲージを表示する。ジャストゾーンがない場合は zoneStart / zoneEnd を無視する。
    /// </summary>
    public void Show(bool hasJustZone, float zoneStart = 0f, float zoneEnd = 0f)
    {
        HasJustZone = hasJustZone;
        JustZoneStart = Mathf.Clamp01(zoneStart);
        JustZoneEnd = Mathf.Clamp(zoneEnd, JustZoneStart, 1f);
        Progress = 0f;
        IsVisible = true;
    }

    /// <summary>溜め量を更新する。</summary>
    public void SetProgress(float progress)
    {
        Progress = Mathf.Clamp01(progress);
    }

    /// <summary>ゲージを隠す。何度呼んでもよい。</summary>
    public void Hide()
    {
        IsVisible = false;
        Progress = 0f;
        HasJustZone = false;
    }
}
