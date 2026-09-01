using UnityEngine;
using UnityEngine.UI;
using BeastSoccer.Data;
using BeastSoccer.Core;
using BeastSoccer.Player;

namespace BeastSoccer.UI
{
    public class MinimapUI:MonoBehaviour
    {
        public RectTransform mapArea,ballDot;
        [System.Serializable]public class TrackedDot{public Transform target;public RectTransform dot;}
        public TrackedDot[] dots;
        private void Update()
        {
            if(mapArea==null)return;
            if(dots!=null)
            {
                foreach(var d in dots)
                {
                    if(d==null||d.target==null||d.dot==null)continue;
                    d.dot.anchoredPosition=Map(d.target.position);
                    var p=d.target.GetComponent<PlayerController>();
                    var img=d.dot.GetComponent<Image>();
                    if(img!=null && p!=null)
                    {
                        if(p.IsHuman) img.color=new Color(1f,.88f,.18f,1f);
                        else if(p.Role==FieldRole.Goalkeeper) img.color=p.Side==TeamSide.Home?new Color(.95f,.78f,.12f):new Color(1f,.52f,.12f);
                        else img.color=p.Side==TeamSide.Home?Color.cyan:Color.red;
                    }
                }
            }
            if(ballDot!=null&&TeamManager.Instance!=null&&TeamManager.Instance.Ball!=null)ballDot.anchoredPosition=Map(TeamManager.Instance.Ball.position);
        }
        private Vector2 Map(Vector3 w)
        {
            float nx=Mathf.Clamp01((w.x+GameConfig.Instance.pitchLength*.5f)/GameConfig.Instance.pitchLength);
            float ny=Mathf.Clamp01((w.y+GameConfig.Instance.pitchWidth*.5f)/GameConfig.Instance.pitchWidth);
            return new Vector2((nx-.5f)*mapArea.rect.width,(ny-.5f)*mapArea.rect.height);
        }
    }
}
