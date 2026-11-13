Shader "YuDian/WorldText"
{
    Properties { _MainTex ("Font Atlas", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Lighting Off Cull Back ZWrite Off ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct input { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            output vert(input v) { output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            fixed4 frag(output i) : SV_Target { return fixed4(i.color.rgb, i.color.a * tex2D(_MainTex,i.uv).a); }
            ENDCG
        }
    }
}
