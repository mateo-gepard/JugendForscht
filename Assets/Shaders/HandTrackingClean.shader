// Clean hand tracking shader for Meta Quest 3.
// Fixes: the stock OculusSampleAlphaHandOutline has an unshaded depth pre-pass
// that breaks single-pass stereo → glitchy/flickery hand mesh.
//
// This shader runs a proper stereo-aware depth pre-pass and uses camera-relative
// view direction for rim/fresnel instead of a scene light, so the hands always
// look consistent regardless of scene lighting.

Shader "Custom/HandTrackingClean"
{
    Properties
    {
        _ColorPrimary ("Color Primary", Color) = (0.55, 0.55, 0.55, 1)
        _ColorTop ("Color Top", Color) = (1, 1, 1, 1)
        _ColorBottom ("Color Bottom", Color) = (0.36, 0.56, 0.81, 1)
        _RimFactor ("Rim Factor", Range(0.01, 1.0)) = 0.75
        _FresnelPower ("Fresnel Power", Range(0.01, 1.0)) = 0.22
        _HandAlpha ("Hand Alpha", Range(0, 1)) = 1.0
        _MinVisibleAlpha ("Minimum Visible Alpha", Range(0, 1)) = 0.15
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    struct appdata
    {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct v2f
    {
        float4 vertex : SV_POSITION;
        float3 worldNormal : TEXCOORD0;
        float3 viewDir : TEXCOORD1;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    fixed4 _ColorPrimary;
    fixed4 _ColorTop;
    fixed4 _ColorBottom;
    float _RimFactor;
    float _FresnelPower;
    float _HandAlpha;
    float _MinVisibleAlpha;

    v2f vert(appdata v)
    {
        v2f o;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_OUTPUT(v2f, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

        o.vertex = UnityObjectToClipPos(v.vertex);
        o.worldNormal = UnityObjectToWorldNormal(v.normal);

        float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
        o.viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos);

        return o;
    }

    fixed4 fragShaded(v2f i) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

        float3 normal = normalize(i.worldNormal);
        float3 viewDir = normalize(i.viewDir);

        // Rim lighting based on view angle (not scene light)
        float NdotV = saturate(dot(normal, viewDir));
        float rim = pow(1.0 - NdotV, 0.5) * (1.0 - _RimFactor) + _RimFactor;
        rim = saturate(rim);

        // Color via fresnel
        float fresnel = saturate(pow(1.0 - NdotV, _FresnelPower));
        fixed3 color = lerp(_ColorTop.rgb, _ColorBottom.rgb, fresnel);

        // Rim emission
        float3 emission = lerp(float3(0, 0, 0), _ColorPrimary.rgb, rim);
        emission += rim * 0.5;
        emission *= 0.95;
        color *= emission;

        // Alpha
        float alphaValue = step(_MinVisibleAlpha, _HandAlpha) * _HandAlpha;

        return float4(color, alphaValue);
    }
    ENDCG

    // ── URP SubShader ──────────────────────────────────────────
    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal" }
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        // Pass 0: Stereo-aware depth pre-pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask 0

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth
            #pragma multi_compile_instancing

            fixed4 fragDepth(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return 0;
            }
            ENDCG
        }

        // Pass 1: Shading
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha, Zero One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragShaded
            #pragma multi_compile_instancing
            ENDCG
        }
    }

    // ── BiRP SubShader ─────────────────────────────────────────
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        // Pass 0: Stereo-aware depth pre-pass
        Pass
        {
            ZWrite On
            ColorMask 0

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth
            #pragma multi_compile_instancing

            fixed4 fragDepth(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return 0;
            }
            ENDCG
        }

        // Pass 1: Shading
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha, Zero One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragShaded
            #pragma multi_compile_instancing
            ENDCG
        }
    }
}
