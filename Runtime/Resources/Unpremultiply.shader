Shader "Hidden/ParticleUI/Unpremultiply"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader { Cull Off ZWrite Off ZTest Always
        Pass {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 frag(v2f_img i) : SV_Target {
                float4 c = tex2D(_MainTex, i.uv);
                return float4(c.a > 0.0001 ? c.rgb / c.a : 0, c.a);
            }
            ENDCG
        }
    }
}
