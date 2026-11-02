/// <summary>
/// 攻撃予測
/// </summary>
Shader "BossBattle/AttackPrediction"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            //背景色と色を混ぜている
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            //表面だけ描画
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            //メッシュから頂点の位置と色を受け取る構造体
            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            //頂点シェーダーから次の描画処理へデータを渡すための構造体
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
            };

            //変換した頂点位置と色を次の処理へ渡す
            Varyings Vert(Attributes input)
            {
                Varyings output;
                //ローカル座標を描画用のクリップ座標へ変換
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }

            //色を出力
            half4 Frag(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }
}
