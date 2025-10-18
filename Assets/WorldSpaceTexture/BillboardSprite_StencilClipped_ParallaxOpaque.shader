Shader "URP/BillboardSprite_StencilClipped_DeltaWorld"
{
    Properties
    {
        _MainTex ("Image (repeats in shader)", 2D) = "white" {}
        _Tint    ("Tint", Color) = (1,1,1,1)
        _Tiling  ("Tiling (X,Y)", Vector) = (3,3,0,0)
        _Offset  ("Offset (X,Y)", Vector) = (0,0,0,0)
        _SensitivityU ("Horizontal Sensitivity", Float) = 1.0
        _SensitivityV ("Vertical Sensitivity",   Float) = 1.0
    }

    SubShader
    {
        // *** OPAQUE, depth-tested ***
        Tags { "RenderPipeline"="UniversalRenderPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        Cull Back
        ZWrite On
        ZTest LEqual
        Blend One Zero

        // (Optional) You do NOT need stencil anymore. If you keep the mask material, and still want
        // to gate by stencil, uncomment this block:
        // Stencil { Ref 1 Comp Equal Pass Keep Fail Keep ZFail Keep }

        Pass
        {
            Name "ScreenLockedParallaxOnMesh"
            Tags { "LightMode"="SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _Tint, _Tiling, _Offset;
            float  _SensitivityU, _SensitivityV;

            // from your binder: (ΔUmeters, ΔVmeters)
            float2 _DeltaUVMeters;

            struct Attributes { float4 positionOS: POSITION; };
            struct Varyings   { float4 posHCS: SV_POSITION; float4 screenPos: TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 ws = mul(GetObjectToWorldMatrix(), v.positionOS).xyz;
                o.posHCS    = TransformWorldToHClip(ws);
                o.screenPos = ComputeScreenPos(o.posHCS); // 0..w
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // Screen-locked base UV → camera motion/rotation doesn’t scroll
                float2 uv = (i.screenPos.xy / i.screenPos.w);

                // Inverse scroll so image feels fixed while object moves
                uv.x += -_DeltaUVMeters.x * _SensitivityU;
                uv.y += -_DeltaUVMeters.y * _SensitivityV;

                // Tiling/offset + force repeat even if texture import is Clamp
                uv = uv * _Tiling.xy + _Offset.xy;
                uv = frac(uv);

                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Tint; // opaque
            }
            ENDHLSL
        }
    }
    FallBack Off
}
