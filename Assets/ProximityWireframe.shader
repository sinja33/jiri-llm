Shader "Unlit/ProximityWireframe"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _WireframeFrontColour("Wireframe front colour", color) = (1.0, 1.0, 1.0, 1.0)
        _WireframeBackColour("Wireframe back colour", color) = (0.5, 0.5, 0.5, 1.0)
        _WireframeWidth("Wireframe width threshold", float) = 0.05
        
        // Proximity reveal properties
        _HandPosition1("Hand Position 1", Vector) = (0,0,0,0)
        _HandPosition2("Hand Position 2", Vector) = (0,0,0,0)
        _RevealRadius("Reveal Radius", Float) = 2.0
        _FadeDistance("Fade Distance", Float) = 0.5
        _GlobalReveal("Global Reveal", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent"}
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry geom
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            struct g2f {
                float4 pos : SV_POSITION;
                float3 barycentric : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            // Properties
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _WireframeFrontColour;
            fixed4 _WireframeBackColour;
            float _WireframeWidth;
            
            // Proximity properties
            float4 _HandPosition1; // xyz = position, w = active (0 or 1)
            float4 _HandPosition2; // xyz = position, w = active (0 or 1)  
            float _RevealRadius;
            float _FadeDistance;
            float _GlobalReveal;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle v2f IN[3], inout TriangleStream<g2f> triStream) {
                g2f o;
                
                // Calculate barycentric coordinates for wireframe
                for (int i = 0; i < 3; i++)
                {
                    o.pos = IN[i].vertex;
                    o.worldPos = IN[i].worldPos;
                    o.barycentric = float3(0, 0, 0);
                    o.barycentric[i] = 1.0;
                    triStream.Append(o);
                }
            }

            fixed4 frag(g2f i) : SV_Target
            {
                // Calculate wireframe
                float3 d = fwidth(i.barycentric);
                float3 a3 = smoothstep(float3(0.0, 0.0, 0.0), d * _WireframeWidth * 10, i.barycentric);
                float edgeFactor = 1.0 - min(min(a3.x, a3.y), a3.z);
                
                // Calculate proximity reveal
                float reveal = _GlobalReveal; // Base global reveal
                
                // Check distance to hand 1
                if (_HandPosition1.w > 0.5) // Hand 1 is active
                {
                    float dist1 = distance(i.worldPos, _HandPosition1.xyz);
                    float reveal1 = 1.0 - smoothstep(_RevealRadius - _FadeDistance, _RevealRadius, dist1);
                    reveal = max(reveal, reveal1);
                }
                
                // Check distance to hand 2
                if (_HandPosition2.w > 0.5) // Hand 2 is active
                {
                    float dist2 = distance(i.worldPos, _HandPosition2.xyz);
                    float reveal2 = 1.0 - smoothstep(_RevealRadius - _FadeDistance, _RevealRadius, dist2);
                    reveal = max(reveal, reveal2);
                }
                
                // Determine front/back face
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 worldNormal = normalize(cross(ddx(i.worldPos), ddy(i.worldPos)));
                float facing = dot(worldNormal, viewDir);
                
                // Choose color based on facing
                fixed4 wireColor = facing > 0 ? _WireframeFrontColour : _WireframeBackColour;
                
                // Combine wireframe, proximity, and alpha
                float finalAlpha = edgeFactor * reveal * wireColor.a;
                
                return fixed4(wireColor.rgb, finalAlpha);
            }
            ENDCG
        }
    }
    
    Fallback "Hidden/InternalErrorShader"
}