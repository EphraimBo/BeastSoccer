using UnityEngine;
using BeastSoccer.Core;
using BeastSoccer.Ball;
using BeastSoccer.Data;

namespace BeastSoccer.Presentation
{
    [DefaultExecutionOrder(1200)]
    public class GoroKeeperVisual : MonoBehaviour
    {
        public PlayerVisualProxy visual;
        private static Sprite[] poses;
        private bool held;
        private float caughtAt;
        public static Sprite ReadySprite
        {
            get
            {
                var approved = DemoCharacterArtV8.First(CharacterType.Goro, TeamSide.Home, "Idle_Ready_ThreeQuarter");
                if (approved != null) return approved;
                Load();
                return poses != null ? poses[0] : null;
            }
        }
        private static void Load()
        {
            if(poses != null) return;
            var tex=Resources.Load<Texture2D>("Duel/GoroKeeper");
            if(tex==null) return;
            // Pixel bounds measured on the authored 1536x1024 key-pose atlas, top-origin.
            var bounds=new Rect[] {new Rect(0,0,412,512),new Rect(412,0,372,512),new Rect(784,0,390,512),new Rect(1174,0,362,512),
                new Rect(0,512,360,512),new Rect(360,512,424,512),new Rect(764,512,456,512),new Rect(1122,512,414,512)};
            poses=new Sprite[8];
            for(int i=0;i<8;i++)
            {
                Rect b=bounds[i];
                Rect r=new Rect(b.x/1536*tex.width,(1024-b.y-b.height)/1024*tex.height,b.width/1536*tex.width,b.height/1024*tex.height);
                poses[i]=Sprite.Create(tex,r,new Vector2(.5f,.12f),tex.height/1024f*200,0,SpriteMeshType.FullRect);
                poses[i].name=i.ToString();
            }
        }
        private void LateUpdate()
        {
            if(visual==null || visual.source==null || visual.spriteRenderer==null) return;
            var p=visual.source;
            if(p.Role!=FieldRole.Goalkeeper || p.Character!=CharacterType.Goro || !DuelRules.Enabled) return;
            Load(); if(poses==null) return;
            if(p.HasBall && !held) caughtAt=Time.time;
            held=p.HasBall;
            int pose=0;
            var ball=BallControl.Instance;
            if(p.HasBall)
                pose=ball != null && ball.Mode==BallControl.BallMode.ActionSetup ? 5 : Time.time-caughtAt<.18f ? 3 : 4;
            else if(ball!=null && ball.LastKicker==p && Time.time-ball.LastKickTime<.26f) pose=6;
            else if(p.CurrentVelocity.magnitude>.15f) pose=1+(Mathf.FloorToInt(Time.time*8)%2);
            visual.spriteRenderer.sprite=poses[pose];
            visual.spriteRenderer.flipX=TeamManager.Instance.AttackDirFor(p.Side)<0;
            visual.spriteRenderer.transform.localScale=Vector3.one * (DuelRules.Enabled ? DemoMatchRules.PlayerScale : 1f);
        }
    }
}
