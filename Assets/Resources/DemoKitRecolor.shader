Shader "BeastSoccer/DemoKitRecolor"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [PerRendererData] _Away ("Away Kit", Float) = 0
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnitySprites.cginc"
            float _Away;

            float RectMask(float2 uv, float2 lo, float2 hi, float feather)
            {
                float2 a = smoothstep(lo, lo + feather, uv);
                float2 b = 1.0 - smoothstep(hi - feather, hi, uv);
                return saturate(a.x * a.y * b.x * b.y);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
                if (c.a <= 0.001) return c;

                float2 uv = IN.texcoord;
                float torso = RectMask(uv, float2(.28,.39), float2(.72,.73), .045);
                float shorts = RectMask(uv, float2(.31,.23), float2(.69,.43), .035);
                float socks = RectMask(uv, float2(.24,.10), float2(.76,.29), .035);
                float shoulder = RectMask(uv, float2(.18,.58), float2(.82,.77), .030);
                float collar = RectMask(uv, float2(.43,.69), float2(.57,.81), .018);
                float badge = RectMask(uv, float2(.54,.49), float2(.67,.64), .020);

                float luma = dot(c.rgb, float3(.299,.587,.114));
                float shade = lerp(.58, 1.12, saturate(luma));

                float3 homeOrange = float3(1.00,.54,.28) * shade;
                float3 homeBlack = float3(.055,.060,.065) * lerp(.72,1.35,saturate(luma));
                float3 awayWhite = float3(.96,.98,1.00) * lerp(.78,1.08,saturate(luma));
                float3 awayBlue = float3(.18,.36,.92) * shade;
                float3 awayRed = float3(1.00,.22,.25) * shade;

                float3 home = c.rgb;
                home = lerp(home, homeOrange, torso * .90);
                home = lerp(home, homeBlack, shorts * .94);
                home = lerp(home, homeOrange, socks * .82);
                home = lerp(home, homeBlack, collar * .86);

                float kitMask = saturate(max(torso, max(shorts, max(socks, shoulder))));
                float3 away = c.rgb;
                away = lerp(away, awayWhite, saturate(torso + shorts + socks) * .92);
                away = lerp(away, awayBlue, shoulder * .68);
                away = lerp(away, awayBlue, collar * .74);
                away = lerp(away, awayRed, badge * .92);

                c.rgb = lerp(home, away, saturate(_Away));
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
