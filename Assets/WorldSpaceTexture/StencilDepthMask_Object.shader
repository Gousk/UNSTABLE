Shader "URP/StencilDepthMask_Object"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalRenderPipeline" "Queue"="Geometry-10" "RenderType"="Opaque" }

        Pass
        {
            Name "StencilDepthMask"
            Tags { "LightMode"="SRPDefaultUnlit" }

            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            Stencil { Ref 1 Comp Always Pass Replace }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct A { float4 positionOS: POSITION; };
            struct V { float4 pos: SV_POSITION; };

            V vert (A v)
            {
                V o;
                float3 ws = mul(GetObjectToWorldMatrix(), v.positionOS).xyz;
                o.pos = TransformWorldToHClip(ws);
                return o;
            }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
