using UnityEngine;
using UnityEngine.UI;
using BeastSoccer.Data;

namespace BeastSoccer.UI
{
    // FIX28 scene-safe menu skin. Builds on top of the existing MainMenu canvas at runtime,
    // so the Match scene / custom pitch / ball / goals are never touched.
    public class BeastMenuOverhaul : MonoBehaviour
    {
        private MainMenuUI menu;
        private RectTransform root;
        private GameObject baseScreen, modeScreen, pickScreen, rivalScreen;
        private Text infoTitle, infoBody;
        private CharacterType selectedPlayer = CharacterType.Volt;
        private CharacterType selectedRival = CharacterType.Leo;

        public void Build(MainMenuUI owner)
        {
            menu = owner;
            if (transform.Find("FIX28_MENU_OVERHAUL") != null) return;

            if (menu.modePanel) menu.modePanel.SetActive(false);
            if (menu.characterPanel) menu.characterPanel.SetActive(false);
            if (menu.opponentPanel) menu.opponentPanel.SetActive(false);

            var go = new GameObject("FIX28_MENU_OVERHAUL", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            root = go.GetComponent<RectTransform>();
            Stretch(root);

            baseScreen = ImageFull(root, "BASE", "MenuBase");
            modeScreen = Group(root, "MODE_SCREEN");
            pickScreen = Group(root, "YOUR_BEAST_SCREEN");
            rivalScreen = Group(root, "RIVAL_SCREEN");

            // MODE: only Quick Match is active. Existing Defending mode is intentionally absent/unusable.
            ImageFull(modeScreen.transform, "QUICK_MATCH_ART", "QuickMatch");
            InvisibleButton(modeScreen.transform, "QUICK_MATCH", new Rect(.37f,.53f,.23f,.25f), () => ShowPick());

            // Character cards on the right. Order: Volt, Leo, Goro.
            AddCharacterChoiceButtons(pickScreen.transform, false);
            AddCharacterChoiceButtons(rivalScreen.transform, true);

            // Selected cards in the central VS slots.
            var yourVolt = ImageFull(pickScreen.transform,"YOUR_VOLT","YourVolt");
            var yourLeo  = ImageFull(pickScreen.transform,"YOUR_LEO","YourLeo");
            var yourGoro = ImageFull(pickScreen.transform,"YOUR_GORO","YourGoro");
            yourVolt.SetActive(false); yourLeo.SetActive(false); yourGoro.SetActive(false);

            // info panel text occupies the already-designed empty left panel.
            infoTitle = CreateText(pickScreen.transform,"INFO_TITLE", new Vector2(.055f,.72f), new Vector2(.22f,.08f), 34, TextAnchor.MiddleLeft);
            infoBody  = CreateText(pickScreen.transform,"INFO_BODY",  new Vector2(.055f,.31f), new Vector2(.22f,.41f), 22, TextAnchor.UpperLeft);

            // Click card -> update selection; second click/Continue advances.
            InvisibleButton(pickScreen.transform,"CONTINUE_PICK",new Rect(.345f,.035f,.31f,.10f),() => ShowRival());
            AddSimpleButton(pickScreen.transform,"CONTINUE",new Vector2(0,-455),new Vector2(460,78),() => ShowRival(),26);

            // Rival central card + start button.
            ImageFull(rivalScreen.transform,"YOUR_VOLT_R","YourVolt").SetActive(false);
            ImageFull(rivalScreen.transform,"YOUR_LEO_R","YourLeo").SetActive(false);
            ImageFull(rivalScreen.transform,"YOUR_GORO_R","YourGoro").SetActive(false);
            ImageFull(rivalScreen.transform,"RIVAL_VOLT","RivalVolt").SetActive(false);
            ImageFull(rivalScreen.transform,"RIVAL_LEO","RivalLeo").SetActive(false);
            ImageFull(rivalScreen.transform,"RIVAL_GORO","RivalGoro").SetActive(false);
            ImageFull(rivalScreen.transform,"START_MATCH_ART","StartMatch");
            InvisibleButton(rivalScreen.transform,"START_MATCH",new Rect(.36f,.69f,.30f,.12f),() => StartMatch());
            AddSimpleButton(rivalScreen.transform,"BACK",new Vector2(-690,-430),new Vector2(180,60),() => ShowPick(),22);

            ShowMode();
        }

        private void ShowMode(){ modeScreen.SetActive(true); pickScreen.SetActive(false); rivalScreen.SetActive(false); }
        private void ShowPick(){ modeScreen.SetActive(false); pickScreen.SetActive(true); rivalScreen.SetActive(false); SelectPlayer(selectedPlayer); }
        private void ShowRival(){ modeScreen.SetActive(false); pickScreen.SetActive(false); rivalScreen.SetActive(true); SelectRival(selectedRival); RefreshYourCardOnRival(); }

        private void AddCharacterChoiceButtons(Transform parent, bool rival)
        {
            ImageFull(parent,"VOLT_CARD","VoltCard");
            ImageFull(parent,"LEO_CARD","LeoCard");
            ImageFull(parent,"GORO_CARD","GoroCard");
            // Cards are authored on the right at three vertical positions.
            InvisibleButton(parent,"VOLT_PICK",new Rect(.735f,.58f,.25f,.22f),()=> { if(rival) SelectRival(CharacterType.Volt); else SelectPlayer(CharacterType.Volt); });
            InvisibleButton(parent,"LEO_PICK",new Rect(.735f,.36f,.25f,.22f),()=> { if(rival) SelectRival(CharacterType.Leo); else SelectPlayer(CharacterType.Leo); });
            InvisibleButton(parent,"GORO_PICK",new Rect(.735f,.14f,.25f,.22f),()=> { if(rival) SelectRival(CharacterType.Goro); else SelectPlayer(CharacterType.Goro); });
        }

        private void SelectPlayer(CharacterType c)
        {
            selectedPlayer = c;
            MatchSetup.PlayerCharacter = c;
            Toggle(pickScreen,"YOUR_VOLT",c==CharacterType.Volt);
            Toggle(pickScreen,"YOUR_LEO",c==CharacterType.Leo);
            Toggle(pickScreen,"YOUR_GORO",c==CharacterType.Goro);
            if(infoTitle) infoTitle.text = c==CharacterType.Volt ? "VOLT — THE SKY STRIKER" : c==CharacterType.Leo ? "LEO — THE PRIDE" : "GORO — THE TITAN";
            if(infoBody) infoBody.text = InfoFor(c);
        }

        private void SelectRival(CharacterType c)
        {
            selectedRival = c;
            MatchSetup.OpponentCharacter = c;
            MatchSetup.OpponentRandom = false;
            Toggle(rivalScreen,"RIVAL_VOLT",c==CharacterType.Volt);
            Toggle(rivalScreen,"RIVAL_LEO",c==CharacterType.Leo);
            Toggle(rivalScreen,"RIVAL_GORO",c==CharacterType.Goro);
        }

        private void RefreshYourCardOnRival()
        {
            Toggle(rivalScreen,"YOUR_VOLT_R",selectedPlayer==CharacterType.Volt);
            Toggle(rivalScreen,"YOUR_LEO_R",selectedPlayer==CharacterType.Leo);
            Toggle(rivalScreen,"YOUR_GORO_R",selectedPlayer==CharacterType.Goro);
        }

        private void StartMatch()
        {
            MatchSetup.Mode = GameMode.Regular;
            MatchSetup.PlayerCharacter = selectedPlayer;
            MatchSetup.OpponentCharacter = selectedRival;
            MatchSetup.OpponentRandom = false;
            // Use existing public callback so scene loading stays centralized.
            if(selectedRival==CharacterType.Volt) menu.OnOpponentVolt();
            else if(selectedRival==CharacterType.Goro) menu.OnOpponentGoro();
            else menu.OnOpponentLeo();
        }

        private string InfoFor(CharacterType c)
        {
            if(c==CharacterType.Volt) return "ROLE: ATTACKER\n\nSPEED      ■■■■■\nSHOOT      ■■■■□\nSTRENGTH   ■■□□□\nPASS       ■■■□□\nDEFENSE    ■■□□□\n\nSPECIAL ABILITY\nFLY / SKY BURST\nMassive jump with hang time.\nFast, direct attacking play.";
            if(c==CharacterType.Goro) return "ROLE: BRUISER\n\nSPEED      ■■□□□\nSHOOT      ■■■□□\nSTRENGTH   ■■■■■\nPASS       ■■□□□\nDEFENSE    ■■■■□\n\nSPECIAL ABILITY\nCHARGE\nPower through defenders and\nwin physical collisions.";
            return "ROLE: ATTACKER\n\nSPEED      ■■■■□\nSHOOT      ■■■■□\nSTRENGTH   ■■■□□\nPASS       ■■■■□\nDEFENSE    ■■□□□\n\nSPECIAL ABILITY\nPRIDE RUSH\nFast playmaker with improved\npassing and attacking speed.";
        }

        private GameObject Group(Transform p,string n){ var g=new GameObject(n,typeof(RectTransform));g.transform.SetParent(p,false);Stretch(g.GetComponent<RectTransform>());return g; }
        private GameObject ImageFull(Transform p,string n,string resource)
        {
            var g=new GameObject(n,typeof(RectTransform),typeof(RawImage)); g.transform.SetParent(p,false); Stretch(g.GetComponent<RectTransform>());
            var img=g.GetComponent<RawImage>(); img.texture=Resources.Load<Texture2D>("BeastMenu/"+resource); img.raycastTarget=false; return g;
        }
        private void InvisibleButton(Transform p,string n,Rect normalized,System.Action action)
        {
            var g=new GameObject(n,typeof(RectTransform),typeof(Image),typeof(Button));g.transform.SetParent(p,false);
            var rt=g.GetComponent<RectTransform>();rt.anchorMin=new Vector2(normalized.xMin,1f-normalized.yMax);rt.anchorMax=new Vector2(normalized.xMax,1f-normalized.yMin);rt.offsetMin=rt.offsetMax=Vector2.zero;
            var im=g.GetComponent<Image>();im.color=new Color(1,1,1,0.001f);var b=g.GetComponent<Button>();b.onClick.AddListener(()=>action());
        }
        private void AddSimpleButton(Transform p,string label,Vector2 pos,Vector2 size,System.Action action,int font)
        {
            var g=new GameObject(label,typeof(RectTransform),typeof(Image),typeof(Button));g.transform.SetParent(p,false);var rt=g.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.sizeDelta=size;rt.anchoredPosition=pos;
            g.GetComponent<Image>().color=new Color(.05f,.08f,.11f,.92f);g.GetComponent<Button>().onClick.AddListener(()=>action());var t=CreateText(g.transform,label+"_TEXT",Vector2.zero,Vector2.one,font,TextAnchor.MiddleCenter,true);t.text=label;
        }
        private Text CreateText(Transform p,string n,Vector2 anchorMin,Vector2 anchorSize,int font,TextAnchor align,bool local=false)
        {
            var g=new GameObject(n,typeof(RectTransform),typeof(Text));g.transform.SetParent(p,false);var rt=g.GetComponent<RectTransform>();
            if(local){rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;} else {rt.anchorMin=anchorMin;rt.anchorMax=anchorMin+anchorSize;rt.offsetMin=rt.offsetMax=Vector2.zero;}
            var t=g.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=font;t.alignment=align;t.color=new Color(.92f,.82f,.55f,1f);t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;return t;
        }
        private void Toggle(GameObject parent,string child,bool on){var t=parent.transform.Find(child);if(t)t.gameObject.SetActive(on);}
        private static void Stretch(RectTransform rt){rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;}
    }
}
