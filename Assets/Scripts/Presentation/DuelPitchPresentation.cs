using UnityEngine;
using BeastSoccer.Core;
using BeastSoccer.Data;

namespace BeastSoccer.Presentation
{
    public class DuelPitchPresentation : MonoBehaviour
    {
        private Material lineMaterial;
        private void Start()
        {
            if(!DuelRules.Enabled) return;
            var texture=Resources.Load<Texture2D>("Duel/PitchBird");
            foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if(r.name.StartsWith("Pitch_Artwork") && texture!=null) r.material.mainTexture=texture;
            lineMaterial=new Material(Shader.Find("Sprites/Default"));
            AddZone(1); AddZone(-1);
        }
        private void AddZone(int dir)
        {
            var go=new GameObject(dir==1 ? "HomeShootingArc" : "AwayShootingArc");
            go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();
            line.sharedMaterial=lineMaterial;
            line.useWorldSpace=true; line.positionCount=65; line.widthMultiplier=.055f;
            line.startColor=line.endColor=new Color(.22f,.65f,1f,.44f);
            line.numCornerVertices=3;
            for(int i=0;i<65;i++)
            {
                float a=(90f+180f*i/64f)*Mathf.Deg2Rad;
                line.SetPosition(i,new Vector3(dir*(GameConfig.Instance.pitchLength*.5f+Mathf.Cos(a)*DuelRules.ShotRadius),.065f,Mathf.Sin(a)*DuelRules.ShotRadius));
            }
        }
        private void OnDestroy() { if(lineMaterial!=null) Destroy(lineMaterial); }
    }
}
