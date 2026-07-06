Shader "Hidden/RuntimeGizmos/SDFFont"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "Queue"      = "Overlay"
            "RenderType" = "Transparent"
        }

        ZWrite Off
        ZTest  Always
        Cull   Off
        Blend  SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM

            #pragma vertex   vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;

                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = v.uv;
                o.color = v.color;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float d         = tex2D(_MainTex, i.uv).a;
                float smoothing = fwidth(d) * 0.5;
                float alpha     = smoothstep(0.5 - smoothing, 0.5 + smoothing, d);

                return fixed4(i.color.rgb, i.color.a * alpha);
            }

            ENDCG
        }
    }
}