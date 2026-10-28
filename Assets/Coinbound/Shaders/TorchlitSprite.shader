Shader "Coinbound/Torchlit Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [PerRendererData] _StoneTint ("Stone color correction / enabled", Vector) = (1,1,1,0)
        [PerRendererData] _PixelWorldRect ("World bounds for pixel grid", Vector) = (0,0,0,0)
        [PerRendererData] _PixelUVRect ("Sprite UV rectangle", Vector) = (0,0,1,1)
        [PerRendererData] _PixelTexelSize ("Source texture texel size", Vector) = (1,1,1,1)
        [PerRendererData] _GroundShadow ("Ground shadow opacity / softness / unused / enabled", Vector) = (0,0.08,0,0)
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _TorchShadowMap ("Radial wall distances", 2D) = "black" {}
        [HideInInspector] _TorchWallMap ("Wall cells", 2D) = "black" {}
        [HideInInspector] _TorchPosition ("Light position / enabled", Vector) = (0,0,0,0)
        [HideInInspector] _TorchSettings ("Radius / intensity / texel / wall depth", Vector) = (8,1.15,0.00048828125,0.8)
        [HideInInspector] _TorchColor ("Light tint", Color) = (1,0.97,0.94,1)
        [HideInInspector] _DarknessColor ("Darkness in active color space", Vector) = (0.005181517,0.005181517,0.005181517,1)
        [HideInInspector] _LightTuning ("Unlit visibility / shadow softness / wall boost / upper wall boost", Vector) = (0.035,0.35,2.5,1.5)
        [HideInInspector] _DungeonSize ("Map size", Vector) = (117,57,0.008547,0.01754)
        [HideInInspector] _TorchFlicker ("Flicker", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_TorchShadowMap); SAMPLER(sampler_TorchShadowMap);
            TEXTURE2D(_TorchWallMap); SAMPLER(sampler_TorchWallMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _StoneTint;
                float4 _PixelWorldRect;
                float4 _PixelUVRect;
                float4 _PixelTexelSize;
                float4 _GroundShadow;
                float4 _TorchPosition;
                float4 _TorchSettings;
                half4 _TorchColor;
                half4 _DarknessColor;
                float4 _LightTuning;
                float4 _DungeonSize;
                float _TorchFlicker;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 world : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.world = TransformObjectToWorld(input.positionOS).xy;
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            float VisibilityAtDepth(float blocker, float distance, float wall)
            {
                float reach = blocker + wall * _TorchSettings.w;
                float visible = 1 - smoothstep(reach - 0.035, reach + 0.015, distance);
                // Fade only a short way along the ray beyond the first wall, as in the original light.
                float wallFace = 1 - smoothstep(0.05, _TorchSettings.w, max(0, distance - blocker));
                return visible * lerp(1, wallFace, wall);
            }
            float Sight(float angle, float distance, float wall)
            {
                // Interpolate visibility, not wall depths: a distant opening must not move a nearby wall.
                float index = frac(angle) / _TorchSettings.z - 0.5;
                float first = (floor(index) + 0.5) * _TorchSettings.z;
                float a = SAMPLE_TEXTURE2D(_TorchShadowMap, sampler_TorchShadowMap, float2(frac(first), 0.5)).r;
                float b = SAMPLE_TEXTURE2D(_TorchShadowMap, sampler_TorchShadowMap, float2(frac(first + _TorchSettings.z), 0.5)).r;
                return lerp(VisibilityAtDepth(a, distance, wall), VisibilityAtDepth(b, distance, wall), frac(index));
            }
            float SoftSight(float angle, float distance, float wall)
            {
                float spread = max(_TorchSettings.z, _LightTuning.y * 0.15915494309 / max(distance, 1));
                float visibility = 0;
                float totalWeight = 0;
                // Dense Gaussian sampling avoids the visible bands of the previous seven-tap filter.
                // Only the light samples are filtered; the result still belongs to one whole art pixel.
                [unroll]
                for (int tap = 0; tap < 32; tap++)
                {
                    float offset = ((tap + 0.5) / 32.0) * 2 - 1;
                    float weight = exp(-2 * offset * offset);
                    visibility += Sight(angle + spread * offset, distance, wall) * weight;
                    totalWeight += weight;
                }
                return visibility / totalWeight;
            }

            float2 SpriteUV(float2 originalUV, float2 pixelWorld)
            {
                if (_PixelWorldRect.z <= 0) return originalUV;
                float2 localUV = (pixelWorld - _PixelWorldRect.xy) / _PixelWorldRect.zw;
                return _PixelUVRect.xy + localUV * _PixelUVRect.zw;
            }

            float SampleShadowMask(float2 uv)
            {
                // Interpolation is only for soft projected shadows, never for visible sprite colors.
                float2 texel = _PixelTexelSize.xy;
                float2 position = uv / texel - 0.5;
                float2 first = (floor(position) + 0.5) * texel;
                float2 blend = frac(position);
                float a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, first).a;
                float b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, first + float2(texel.x, 0)).a;
                float c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, first + float2(0, texel.y)).a;
                float d = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, first + texel).a;
                return lerp(lerp(a, b, blend.x), lerp(c, d, blend.x), blend.y);
            }

            float GroundShadowAlpha(float2 uv)
            {
                // Filter just the projected silhouette. It remains pixel-stepped on the same world grid.
                float2 spread = _GroundShadow.y * _PixelUVRect.zw / max(_PixelWorldRect.zw, float2(0.001, 0.001));
                float alpha = 0;
                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float weight = (x == 0 ? 2 : 1) * (y == 0 ? 2 : 1);
                        alpha += SampleShadowMask(uv + float2(x, y) * spread) * weight;
                    }
                }
                return alpha / 16;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Evaluate light once per art pixel (32 pixels per tile). Shadow edges must not cut texels diagonally.
                float2 lightWorld = (floor(input.world * 32) + 0.5) / 32;
                float2 uv = SpriteUV(input.uv, lightWorld);
                float2 offset = lightWorld - _TorchPosition.xy;
                float distance = length(offset);
                float angle = atan2(offset.y, offset.x) * 0.15915494309 + 0.5;
                float2 mapUV = float2(lightWorld.x, 1 - lightWorld.y) * _DungeonSize.zw;
                float wall = SAMPLE_TEXTURE2D(_TorchWallMap, sampler_TorchWallMap, mapUV).r;
                float visibility = SoftSight(angle, distance, wall) * _TorchPosition.w;
                float radial = saturate(1 - distance * distance / (_TorchSettings.x * _TorchSettings.x));
                radial *= radial;

                if (_GroundShadow.w > 0)
                {
                    // Low objects only shade nearby floor. They never erase sight or darken the walls/actors.
                    // The floor already contains radial falloff. Attenuate its direct-light share,
                    // rather than multiplying the shadow by that falloff for a second time.
                    float directLight = max(0, _TorchSettings.y * _TorchFlicker) * radial;
                    float directShare = directLight / max(directLight + _LightTuning.x, 0.0001);
                    float opacity = saturate(_GroundShadow.x);
                    #if !defined(UNITY_COLORSPACE_GAMMA)
                        // Keep the opacity control perceptual: 0.35 should be a clearly visible shadow,
                        // not just 35% less linear energy (which looks much weaker on screen).
                        opacity = 1 - pow(1 - opacity, 2.2);
                    #endif
                    float alpha = GroundShadowAlpha(uv) * opacity * directShare * visibility * (1 - wall);
                    return half4(_DarknessColor.rgb, alpha);
                }

                half4 surface = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * input.color;
                // Stone shares the wall palette and light response; blue water keeps its original color.
                float redToBlue = surface.r / max(surface.b, 0.0001);
                #if defined(UNITY_COLORSPACE_GAMMA)
                    float stone = _StoneTint.a * smoothstep(0.3, 0.6, redToBlue);
                #else
                    float stone = _StoneTint.a * smoothstep(0.08, 0.35, redToBlue);
                #endif
                surface.rgb *= lerp(half3(1, 1, 1), _StoneTint.rgb, stone);
                // Boost walls with floor directly below them. Map Y increases downwards.
                float wallBelow = SAMPLE_TEXTURE2D(_TorchWallMap, sampler_TorchWallMap, mapUV + float2(0, _DungeonSize.w)).r;
                float upperWall = wall * (1 - wallBelow);
                float grain = frac(sin(dot(floor(lightWorld * 32), float2(12.9898, 78.233))) * 43758.5453);
                float textureVariation = lerp(0.985, 1.015, grain);
                float wallBoost = lerp(1, _LightTuning.z, max(wall, stone)) * lerp(1, _LightTuning.w, upperWall);
                half3 lighting = _TorchColor.rgb * (_TorchSettings.y * _TorchFlicker * textureVariation
                    * wallBoost * radial * visibility);
                // Dim texture detail has unlimited range along sight lines; the torch still fades out at its radius.
                float ambient = _LightTuning.x * visibility;
                surface.rgb = _DarknessColor.rgb + surface.rgb * (ambient + lighting);
                return surface;
            }
            ENDHLSL
        }
    }
}
