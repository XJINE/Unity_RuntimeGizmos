Shader "Hidden/RuntimeGizmos/SolidColor"
{
    Properties
    {
        _OccludedAlpha ("Occluded Alpha", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "Queue"      = "Overlay"
            "RenderType" = "Transparent"
        }

        Cull  Off
        Blend SrcAlpha OneMinusSrcAlpha

        // Pass 0: Occluded — faint behind scene objects
        Pass
        {
            ZTest  Greater
            ZWrite Off

            CGPROGRAM

            #pragma vertex   vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float _OccludedAlpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;

                o.pos   = UnityObjectToClipPos(v.vertex);
                o.color = v.color;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(i.color.rgb, i.color.a * _OccludedAlpha);
            }

            ENDCG
        }

        // Pass 1: Normal — standard depth-tested rendering
        Pass
        {
            ZTest  LEqual
            ZWrite On

            CGPROGRAM

            #pragma vertex   vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;

                o.pos   = UnityObjectToClipPos(v.vertex);
                o.color = v.color;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return i.color;
            }

            ENDCG
        }
    }
}