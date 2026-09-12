Shader "BeastSoccer/TeamKit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _RendererColor ("Renderer Tint", Color) = (1,1,1,1)
        _Flip ("Flip", Vector) = (1,1,1,1)
        _AlphaTex ("External Alpha", 2D) = "white" {}
        _EnableExternalAlpha ("External Alpha Enabled", Float) = 0
        _Away ("White Away Kit", Float) = 0
        _Character ("Leo 0 Goro 1 Volt 2", Float) = 0
        _SpriteRect ("Texture Rect", Vector) = (0,0,1,1)
        _KeyMagenta ("Key Generated Atlas", Float) = 0
        _Pose ("Keeper Pose", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            float _Away, _Character, _KeyMagenta, _Pose;
            float4 _SpriteRect;
            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord);
                float2 p = (IN.texcoord - _SpriteRect.xy) / max(_SpriteRect.zw, .0001);
                float key = smoothstep(.22,.55,min(c.r,c.b)-c.g);
                c.a *= 1 - key * _KeyMagenta;
                // The source poses have uneven margins. Reject the adjacent pose's corner
                // where two wide throwing silhouettes share an atlas bounding rectangle.
                if (_KeyMagenta > .5 && _Pose > 4.5 && _Pose < 5.5 && p.x > .95 && p.y < .58) c.a=0;
                if (_KeyMagenta > .5 && _Pose > 5.5 && _Pose < 6.5 && p.x < .06 && p.y > .58) c.a=0;
                float light = max(c.r,max(c.g,c.b));
                float red = smoothstep(.10,.25,c.r-c.g) * smoothstep(.08,.20,c.r-c.b);
                float cloth = red;
                float shorts = step(.25,p.y) * (1-step(.35,p.y));
                if (_Character > 1.5)
                {
                    // Keep Volt's purple-black feathers, white head and red crest untouched.
                    float torso = step(.27,p.y)*(1-step(.53,p.y))*step(.29,p.x)*(1-step(.65,p.x));
                    float neutral = 1-smoothstep(.025,.08,abs(c.r-c.g)+abs(c.b-c.g));
                    float trim = smoothstep(.15,.35,c.r-c.b)*step(c.g,c.r);
                    shorts = step(.22,p.y)*(1-step(.32,p.y));
                    float shortRegion=shorts*step(.28,p.x)*(1-step(.66,p.x));
                    cloth = max(torso * max(neutral, trim),shortRegion*neutral);
                }
                else if (_Character > .5)
                {
                    cloth = smoothstep(.16,.35,c.r-c.b) * smoothstep(.08,.22,c.r-c.g) * (1-step(.76,c.g));
                    shorts = step(.30,p.y)*(1-step(.44,p.y))*step(.24,p.x)*(1-step(.75,p.x));
                    float neutral = 1-smoothstep(.04,.13,abs(c.r-c.g)+abs(c.b-c.g));
                    cloth = max(cloth, shorts * neutral * smoothstep(.04,.13,light));
                }
                else cloth *= 1-step(.57,p.y); // Leo's mane/face never enters the kit mask.
                float3 orange = float3(1,.38,.055) * (.4 + .65*light);
                float3 black = float3(.055,.06,.065) * (.4 + light);
                float3 white = float3(.93,.97,1) * (.62 + .36*light);
                float3 kit = lerp(lerp(orange,black,shorts),white,_Away);
                c.rgb = lerp(c.rgb,kit,cloth);
                c *= IN.color;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
