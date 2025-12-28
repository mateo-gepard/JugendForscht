Shader "Custom/ChromaKeyBlackToAlpha"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _KeyColor ("Key Color", Color) = (0,1,0,1)
        _Threshold ("Threshold", Range(0, 1)) = 0.4
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            Tags { "LightMode" = "Always" }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _KeyColor;
            float _Threshold;
            float _Smoothness;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Calculate how green the pixel is
                float greenness = col.g - max(col.r, col.b);
                
                // If pixel is predominantly green, make it transparent
                float alpha = 1.0 - smoothstep(_Threshold, _Threshold + _Smoothness, greenness);
                
                col.a = alpha;
                
                return col;
            }
            ENDCG
        }
    }
}
