Shader "Custom/Point Shader GPU"
{
    // Serialized properties
    Properties
    {
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface ConfigureSurface Standard fullforwardshadows addshadow
        // Procedural instancing
        #pragma instancing_options assumeuniformscaling procedural:ConfigureProcedural
        // Turn off async compilation
        #pragma editor_sync_compilation
        // Use shader model 4.5 target to enable features of at least OpenGL ES 3.1
        #pragma target 4.5

        #include "PointGPU.hlsl"
        
        struct Input
        {
            float3 worldPos;
        };

        float _Smoothness;

        void ConfigureSurface(Input input, inout SurfaceOutputStandard surface) {
            surface.Albedo = saturate(input.worldPos * 0.5f + 0.5f);
            surface.Smoothness = _Smoothness;
        }

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        //UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        //UNITY_INSTANCING_BUFFER_END(Props)
        ENDCG
    }
    FallBack "Diffuse"
}
