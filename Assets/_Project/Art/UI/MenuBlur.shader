// Desenfoque gaussiano separable de 5 tomas (con muestreo bilineal) para el fondo del menu. Compatible con URP 2D y WebGL.
Shader "Hidden/DigitatonGame/MenuBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Dir ("Direccion (x,y) y separacion (z)", Vector) = (1,0,1,0)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Dir;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 stepUv = _Dir.xy * _MainTex_TexelSize.xy * _Dir.z;
                fixed4 c = tex2D(_MainTex, i.uv) * 0.2270270270;
                c += tex2D(_MainTex, i.uv + stepUv * 1.3846153846) * 0.3162162162;
                c += tex2D(_MainTex, i.uv - stepUv * 1.3846153846) * 0.3162162162;
                c += tex2D(_MainTex, i.uv + stepUv * 3.2307692308) * 0.0702702703;
                c += tex2D(_MainTex, i.uv - stepUv * 3.2307692308) * 0.0702702703;
                return c;
            }
            ENDCG
        }
    }
}
