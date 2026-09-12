using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BeastSoccer.Data;
using BeastSoccer.Presentation;

namespace BeastSoccer.UI
{
    public class DuelMenuLayout : MonoBehaviour
    {
        private BeastMenuOverhaul menu;
        private readonly List<Material> materials=new List<Material>();
        private readonly List<ArcGraphic> steps=new List<ArcGraphic>();
        private readonly List<Text> stepNumbers=new List<Text>();
        private static readonly Vector2 Center=Vector2.one*.5f;
        private static readonly Color Gold=new Color(.87f,.70f,.34f,1);
        private static readonly Color Navy=new Color(.025f,.055f,.08f,1);

        public static void Install(BeastMenuOverhaul menu)
        {
            if(menu.GetComponent<DuelMenuLayout>()!=null) return;
            var layout=menu.gameObject.AddComponent<DuelMenuLayout>();
            layout.menu=menu; layout.Build();
        }
        private void Build()
        {
            var canvas=menu.GetComponentInParent<Canvas>();
            SafeAreaRoot.ConfigureCanvas(canvas);
            var safe=DuelHud.Rect("MenuSafeArea",canvas.transform);
            safe.gameObject.AddComponent<SafeAreaRoot>();
            var root=(RectTransform)menu.root.transform;
            root.SetParent(safe,false);
            root.anchorMin=Vector2.zero; root.anchorMax=Vector2.one; root.offsetMin=root.offsetMax=Vector2.zero;
            var aspect=root.GetComponent<AspectRatioFitter>();
            if(aspect==null) aspect=root.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode=AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio=1672f/941f;
            // Work in the original artwork's coordinates within the fitted canvas.
            var content=DuelHud.Rect("MenuDesignSpace",root);
            DuelHud.Place(content,Center,Vector2.zero,new Vector2(1672,941));
            var children=new List<Transform>();
            for(int i=0;i<root.childCount;i++) if(root.GetChild(i)!=content) children.Add(root.GetChild(i));
            foreach(var child in children) child.SetParent(content,false);
            content.gameObject.AddComponent<MenuDesignScale>();
            var version = DuelHud.Label(content,"BuildVersion","DEMO V11",18,new Color(.7f,.75f,.8f,.8f));
            DuelHud.Place(version.rectTransform,Center,new Vector2(0,-440),new Vector2(200,28));
            foreach(var page in new[]{menu.modeScreen,menu.pickScreen,menu.rivalScreen})
            {
                if(page==null) continue;
                var rt=(RectTransform)page.transform;
                rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
            }
            var baseArt=content.Find("BASE") as RectTransform;
            if(baseArt!=null) { baseArt.anchorMin=Vector2.zero;baseArt.anchorMax=Vector2.one;baseArt.offsetMin=baseArt.offsetMax=Vector2.zero; }
            LayoutPage(menu.pickScreen,false);
            LayoutPage(menu.rivalScreen,true);
            Crop(menu.modeScreen,"QUICK_MATCH_ART",new Rect(651,156,348,240),new Vector2(0,-295),new Vector2(380,262));
            Position(menu.modeScreen,"QUICK_MATCH",new Vector2(0,-295),new Vector2(380,262));
            var explanation=DuelHud.Label(menu.modeScreen.transform,"DuelRulesCaption","1 RIVAL · 2 GORO KEEPERS · 75 SECONDS",26,Gold);
            DuelHud.Place(explanation.rectTransform,Center,new Vector2(0,75),new Vector2(820,55));
            if(menu.infoTitle) { DuelHud.Place(menu.infoTitle.rectTransform,Center,new Vector2(-560,220),new Vector2(450,70));menu.infoTitle.fontSize=26;menu.infoTitle.alignment=TextAnchor.MiddleLeft; }
            if(menu.infoBody) { DuelHud.Place(menu.infoBody.rectTransform,Center,new Vector2(-560,25),new Vector2(410,320));menu.infoBody.fontSize=23;menu.infoBody.alignment=TextAnchor.UpperLeft; }
            var strip=DuelHud.Rect("ProgressSteps",content).gameObject.AddComponent<Image>();
            strip.color=Navy;strip.raycastTarget=false;
            DuelHud.Place(strip.rectTransform,Center,new Vector2(15,413),new Vector2(865,115));
            string[] names={"MODE","YOUR BEAST","RIVAL","READY"};
            for(int i=0;i<4;i++)
            {
                var ring=DuelHud.Ring(strip.transform,"Step"+(i+1),52,3,Gold);
                DuelHud.Place(ring.rectTransform,Center,new Vector2(-330+i*220,20),new Vector2(52,52));
                var n=DuelHud.Label(ring.transform,"Number",(i+1).ToString(),28,Gold);
                DuelHud.Place(n.rectTransform,Center,Vector2.zero,new Vector2(50,50));
                var label=DuelHud.Label(ring.transform,"Label",names[i],20,Gold);
                DuelHud.Place(label.rectTransform,Center,new Vector2(0,-46),new Vector2(190,36));
                steps.Add(ring); stepNumbers.Add(n);
            }
            version.transform.SetAsLastSibling();
        }
        private void LayoutPage(GameObject page,bool rival)
        {
            if(page==null) return;
            string[] names={"VOLT","LEO"};
            Rect[] crops={new Rect(1233,559,417,199),new Rect(1230,369,420,193)};
            for(int i=0;i<2;i++)
            {
                var card=Crop(page,names[i]+"_CARD",crops[i],new Vector2(610,225-i*220),new Vector2(320,156));
                Recolor(card,i==0?CharacterType.Volt:CharacterType.Leo,rival?TeamSide.Away:TeamSide.Home);
                Position(page,names[i]+"_PICK",new Vector2(610,225-i*220),new Vector2(320,156));
            }
            SetActive(page,"GORO_PICK",false);
            SetActive(page,"GORO_CARD",false);
            SetActive(page,"YOUR_GORO",false);SetActive(page,"YOUR_GORO_R",false);SetActive(page,"RIVAL_GORO",false);
            var keeperPanel=DuelHud.Rect("KeeperCard",page.transform).gameObject.AddComponent<Image>();
            keeperPanel.color=Gold;keeperPanel.raycastTarget=false;
            DuelHud.Place(keeperPanel.rectTransform,Center,new Vector2(610,-215),new Vector2(390,185));
            var inner=DuelHud.Rect("Inset",keeperPanel.transform).gameObject.AddComponent<Image>();inner.color=Navy;inner.raycastTarget=false;
            DuelHud.Place(inner.rectTransform,Center,Vector2.zero,new Vector2(384,179));
            var ready=DemoCharacterArtV8.First(CharacterType.Goro,rival?TeamSide.Away:TeamSide.Home,"Idle_Ready_ThreeQuarter");
            if(ready!=null)
            {
                var portrait=DuelHud.Rect("GoroKeeperPortrait",inner.transform).gameObject.AddComponent<Image>();
                portrait.sprite=ready;portrait.preserveAspect=true;portrait.raycastTarget=false;
                DuelHud.Place(portrait.rectTransform,Center,new Vector2(-95,0),new Vector2(175,180));
                portrait.material=null;
            }
            var caption=DuelHud.Label(inner.transform,"KeeperCaption","GORO\nBOTH GOALS",25,Gold);
            DuelHud.Place(caption.rectTransform,Center,new Vector2(95,0),new Vector2(180,100));
            foreach(string name in names)
            {
                var your=Crop(page,"YOUR_"+name+(rival?"_R":""),new Rect(624,536,159,183),new Vector2(-134,-92),new Vector2(160,184));
                Recolor(your,name=="VOLT"?CharacterType.Volt:CharacterType.Leo,TeamSide.Home);
                if(your!=null) your.gameObject.AddComponent<SelectionPop>();
                if(rival)
                {
                    var their=Crop(page,"RIVAL_"+name,new Rect(624,536,159,183),new Vector2(127,-92),new Vector2(160,184));
                    Recolor(their,name=="VOLT"?CharacterType.Volt:CharacterType.Leo,TeamSide.Away);
                    if(their!=null) their.gameObject.AddComponent<SelectionPop>();
                }
            }
            if(rival)
            {
                Crop(page,"START_MATCH_ART",new Rect(574,404,513,99),new Vector2(-90,-355),new Vector2(440,85));
                Position(page,"START_MATCH",new Vector2(-90,-355),new Vector2(440,85));
            }
            else
            {
                SetActive(page,"CONTINUE_PICK",false); // One click target, exactly on its artwork.
                Position(page,"CONTINUE",new Vector2(-90,-355),new Vector2(440,72));
            }
            Position(page,rival?"BACK":"BACK_PICK",new Vector2(-705,-365),new Vector2(190,65));
        }
        private RawImage Crop(GameObject page,string name,Rect pixels,Vector2 pos,Vector2 size)
        {
            if(page==null) return null;
            var child=page.transform.Find(name);if(child==null) return null;
            var image=child.GetComponent<RawImage>();if(image==null) return null;
            image.uvRect=new Rect(pixels.x/1672,(941-pixels.y-pixels.height)/941,pixels.width/1672,pixels.height/941);
            image.raycastTarget=false;
            DuelHud.Place(image.rectTransform,Center,pos,size);
            return image;
        }
        private void Recolor(RawImage image,CharacterType character,TeamSide side)
        {
            if(image==null) return;
            var approved=DemoCharacterArtV8.First(character,side,"Run_Side");
            if(approved!=null)
            {
                image.texture=approved.texture;
                image.uvRect=new Rect(0,0,1,1);
                image.material=null;
                image.color=Color.white;
                var fit=image.GetComponent<AspectRatioFitter>();
                if(fit!=null) fit.enabled=false;
                // Fit inside the card's assigned bounds, not its full-screen page parent.
                var rt=image.rectTransform;
                Vector2 bounds=rt.sizeDelta;
                float scale=Mathf.Min(bounds.x/approved.texture.width,bounds.y/approved.texture.height);
                rt.sizeDelta=new Vector2(approved.texture.width*scale,approved.texture.height*scale);
                return;
            }
            var mat=TeamKitVisual.CreateMaterial(character,side);if(mat==null) return;
            Rect r=image.uvRect;mat.SetVector("_SpriteRect",new Vector4(r.x,r.y,r.width,r.height));
            image.material=mat;materials.Add(mat);
        }
        private static void Position(GameObject page,string name,Vector2 pos,Vector2 size)
        {
            if(page==null) return;var t=page.transform.Find(name);if(t!=null) DuelHud.Place(t as RectTransform,Center,pos,size);
        }
        private static void SetActive(GameObject page,string name,bool active)
        { var t=page.transform.Find(name);if(t!=null)t.gameObject.SetActive(active); }
        private void LateUpdate()
        {
            // Legacy page methods still toggle their old invisible click target; keep it off.
            SetActive(menu.pickScreen,"CONTINUE_PICK",false);
            int active=menu.modeScreen.activeSelf?0:menu.pickScreen.activeSelf?1:menu.hasRivalSelection?3:2;
            for(int i=0;i<steps.Count;i++)
            {
                Color c=i<=active?Gold:new Color(.28f,.31f,.34f,1);
                steps[i].color=c;stepNumbers[i].color=c;
            }
        }
        private void OnDestroy(){foreach(var m in materials)if(m!=null)Destroy(m);}
    }

    public class MenuDesignScale : MonoBehaviour
    {
        private void LateUpdate()
        {
            var parent=transform.parent as RectTransform;
            if(parent!=null) transform.localScale=Vector3.one*Mathf.Min(parent.rect.width/1672,parent.rect.height/941);
        }
    }
    public class SelectionPop : MonoBehaviour
    {
        private float started;
        private void OnEnable(){started=Time.unscaledTime;transform.localScale=Vector3.one*.94f;}
        private void Update()
        {
            float t=Mathf.Clamp01((Time.unscaledTime-started)/.16f);
            transform.localScale=Vector3.one*Mathf.Lerp(.94f,1f,t*(2-t));
        }
    }
}
