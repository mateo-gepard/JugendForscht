Shader "Custom/RiemannSurface"
{
    // Double-sided, unlit shader with vertex colors for Riemann surface visualization
    Properties
    {
        _Alpha ("Transparency", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100
        
        // Render both sides
        Cull Off
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            float _Alpha;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Simple diffuse lighting from camera direction
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 normal = normalize(i.worldNormal);
                
                // Flip normal if back-facing
                float facing = dot(normal, viewDir);
                if (facing < 0) normal = -normal;
                facing = abs(facing);
                
                // Ambient + diffuse
                float light = 0.35 + 0.65 * facing;
                
                // Apply vertex color with lighting
                fixed4 col = i.color;
                col.rgb *= light;
                col.a *= _Alpha;
                
                // Slight rim glow effect
                float rim = 1.0 - saturate(facing);
                col.rgb += col.rgb * rim * 0.3;
                
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    FallBack "Unlit/Color"
}
