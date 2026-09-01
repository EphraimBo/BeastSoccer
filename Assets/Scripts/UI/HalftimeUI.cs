using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Core;

namespace BeastSoccer.UI
{
    public class HalftimeUI:MonoBehaviour
    {
        public GameObject panel;
        private bool bound;
        private void OnEnable()=>Bind();
        private void Start()=>Bind();
        private void Bind()
        {
            if(bound||GameManager.Instance==null)return;
            GameManager.Instance.OnPhaseChanged+=Handle;bound=true;
        }
        private void OnDisable(){if(bound&&GameManager.Instance!=null)GameManager.Instance.OnPhaseChanged-=Handle;bound=false;}
        private void Handle(MatchPhase p){if(panel!=null)panel.SetActive(p==MatchPhase.HalfTime);}
        public void OnContinue()=>GameManager.Instance?.ContinueFromHalfTime();
    }
}
