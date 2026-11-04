"""Author editable upgrade UI in the two scenes. Run once on the cleanup baseline."""
from unity_scene_tools import *
from pathlib import Path
import copy
TEXT='5f7201a12d95ffc409449d95f23cf332'; IMAGE='fe87c0e1cc204ed48ad3b37840f39efc'; RAW='1344c3c82d62a2a41a3576d8abb8e3ea'; BUTTON='4e29b1a8efbd4b44bb3f3716e73f07ff'
GOLD={'r':.94,'g':.76,'b':.34,'a':1}; WHITE={'r':.9,'g':.93,'b':.95,'a':1}; DARK={'r':.018,'g':.043,'b':.063,'a':1}
def sg(name):return guid('Assets/Scripts/'+name+'.cs')
def texture(path,sprite=False):return {'fileID':21300000 if sprite else 2800000,'guid':guid(path),'type':3}
class UI:
 def __init__(self,s):
  self.s=s;self.templates={g:copy.deepcopy(s.data(s.by_guid(g)[0])) for g in [TEXT,IMAGE,RAW,BUTTON]}
 def component(self,go,g,**fields):
  d=copy.deepcopy(self.templates[g]);d['m_GameObject']=ref(go);d.update(fields)
  c=self.s.add(114,'MonoBehaviour',d);self.s.edit(go)['m_Component'].append({'component':ref(c)});return c
 def obj(self,name,parent,pos=(0,0),size=(100,100),anchor=(.5,.5),active=True):
  g,r=self.s.add_ui(name,parent,active);self.s.place(r,anchor,pos,size);return g,r
 def image(self,g,color=DARK,ray=0,sprite=None):
  self.s.add_renderer(g);return self.component(g,IMAGE,m_Color=color,m_RaycastTarget=ray,m_Sprite=sprite or ref(0),m_Type=1 if sprite else 0)
 def text(self,name,parent,value,pos,size=(600,50),font=28,align=4,color=WHITE,anchor=(.5,.5)):
  g,r=self.obj(name,parent,pos,size,anchor);self.s.add_renderer(g)
  d=copy.deepcopy(self.templates[TEXT]['m_FontData']);d.update(m_FontSize=font,m_FontStyle=1,m_BestFit=1,m_MinSize=max(14,font-4),m_MaxSize=font,m_Alignment=align)
  c=self.component(g,TEXT,m_Text=value,m_Color=color,m_RaycastTarget=0,m_FontData=d);return c,g
 def raw(self,name,parent,pos,size,tex=None,uv=None):
  g,r=self.obj(name,parent,pos,size);self.s.add_renderer(g)
  c=self.component(g,RAW,m_Texture=tex or ref(0),m_UVRect=uv or {'x':0,'y':0,'width':1,'height':1},m_Color={'r':1,'g':1,'b':1,'a':1},m_RaycastTarget=0);return c,g,r
 def call(self,controller,method,typename):
  return {'m_Target':ref(controller),'m_TargetAssemblyTypeName':'BeastSoccer.Upgrades.'+typename+', BeastSoccer.Runtime','m_MethodName':method,'m_Mode':0,'m_Arguments':{'m_ObjectArgument':ref(0),'m_ObjectArgumentAssemblyTypeName':'UnityEngine.Object, UnityEngine','m_IntArgument':0,'m_FloatArgument':0,'m_StringArgument':'','m_BoolArgument':0},'m_CallState':2}
 def button(self,name,parent,label,pos,size,controller=0,method='',typename='ExpandedMenuController',primary=False,anchor=(.5,.5),card=None):
  g,r=self.obj(name,parent,pos,size,anchor);hit=self.image(g,{'r':1,'g':1,'b':1,'a':.001},1)
  vg,vr=self.obj('Button Art',r,(0,0),size)
  if card:
   name_,box=card;x0,y0,x1,y1=box
   self.s.add_renderer(vg);face=self.component(vg,RAW,m_Texture=texture('Assets/Resources/BeastMenu/'+name_+'.png'),m_UVRect={'x':x0/1672,'y':1-y1/941,'width':(x1-x0)/1672,'height':(y1-y0)/941},m_RaycastTarget=0)
  else:face=self.image(vg,{'r':1,'g':1,'b':1,'a':1},sprite=texture('Assets/Art/Progression/GoldButton.png',True))
  colors=copy.deepcopy(self.templates[BUTTON]['m_Colors']);colors['m_PressedColor']={'r':1.18,'g':1.18,'b':1.18,'a':1};colors['m_DisabledColor']={'r':.5,'g':.5,'b':.5,'a':.65}
  b=self.component(g,BUTTON,m_TargetGraphic=ref(face),m_Colors=colors,m_OnClick={'m_PersistentCalls':{'m_Calls':[self.call(controller,method,typename)] if method else []}})
  labelc=0
  if label:labelc,_=self.text('Label',vr,label,(0,0),(size[0]-40,size[1]-12),min(32,int(size[1]*.36)),color={'r':.08,'g':.065,'b':.025,'a':1})
  mg,mr=self.obj('Selected',r,(0,-size[1]/2-7),(size[0]*.8,4),active=False);self.image(mg,GOLD)
  feedback=self.s.add_component(g,sg('Upgrades/UpgradeButtonFeedback'),{'visual':ref(vr),'face':ref(0 if card else face),'primary':int(primary),'selectedMarker':ref(mg)})
  return b,feedback,labelc,g
 def panel(self,name,parent,active=False):
  g,r=self.obj(name,parent,active=active);self.s.stretch(r);self.image(g,DARK,1);return g,r
 def safe(self,canvas,name):
  g,r=self.obj(name,canvas);self.s.stretch(r);self.s.add_component(g,sg('UI/SafeAreaRoot'))
  dg,dr=self.obj('Design Space',r,size=(1600,900));self.s.add_component(dg,sg('UI/SafeAreaDesignSpace'),{'minimumSize':vec(1600,900)});return g,dr
 def tile(self,name,parent,pos,size):
  g,r=self.obj(name,parent,pos,size);bg=self.image(g,{'r':.035,'g':.08,'b':.105,'a':1},1)
  b=self.component(g,BUTTON,m_TargetGraphic=ref(bg),m_OnClick={'m_PersistentCalls':{'m_Calls':[]}})
  w,h=size;thumb,_,_=self.raw('Pitch Preview',r,(0,h/2-80),(w-12,min(138,h-165)));nt,_=self.text('Pitch Name',r,'Pitch',(0,-h/2+118),(w-12,36),26)
  mt,_=self.text('Match Requirement',r,'3 matches',(-w*.2,-h/2+77),(w*.54,30),21,3)
  ct,_=self.text('Coin Requirement',r,'500 coins',(w*.28,-h/2+77),(w*.4,30),21,5,color=GOLD)
  st,_=self.text('Status',r,'Locked',(0,-h/2+36),(w-14,30),21)
  mg,mr=self.obj('Selected',r,(0,-h/2+4),(w,4),active=False);self.image(mg,GOLD)
  return self.s.add_component(g,sg('Upgrades/PitchTileView'),{'button':ref(b),'thumbnail':ref(thumb),'nameText':ref(nt),'matchesText':ref(mt),'coinsText':ref(ct),'statusText':ref(st),'selectedMarker':ref(mg)})

def menu():
 s=Scene('Assets/Scenes/MainMenu.unity');assert not s.by_guid(sg('Upgrades/ExpandedMenuController')),'Already authored'
 u=UI(s);cg=916976362;canvas=916976367;ctrl=s.add_component(cg,sg('Upgrades/ExpandedMenuController'))
 root,design=u.safe(canvas,'Expanded Menu (edit screens here)')
 pages={name:u.panel(name,design,name=='Home') for name in ['Home','Your Beast','Rival and Difficulty','Select Map','Pitch Store']}
 home,beast,rival,maps,store=[pages[n] for n in pages]
 title,_=u.text('Screen Title',design,'QUICK MATCH',(0,335),(900,70),38,color=GOLD)
 u.text('Game Title',design,'BEAST SOCCER',(-610,395),(330,48),30,3,color=GOLD)
 wallet,_=u.text('Coins',design,'Coins 0',(-185,-44),(330,48),28,5,color=GOLD,anchor=(1,1))
 storeb,*_=u.button('Store',design,'STORE',(-140,-112),(200,64),ctrl,'OpenStore',anchor=(1,1))
 u.text('Home Subtitle',home[1],'Choose your beast. Own the pitch.',(0,170),(950,65),32)
 u.button('Quick Match',home[1],'QUICK MATCH',(0,30),(540,105),ctrl,'QuickMatch',primary=True)
 u.text('Home Hint',home[1],'Play matches to unlock pitches. Earn coins to make them yours.',(0,-125),(1100,65),26)
 for page,parent,methods in [('Your Beast',beast[1],['PickVolt','PickLeo']),('Rival',rival[1],['RivalVolt','RivalLeo'])]:
  for i,(card,box) in enumerate([('VoltCard',(1233,559,1650,758)),('LeoCard',(1230,369,1650,562))]):
   b,fb,_,_=u.button(card,parent,'',(-340+680*i,65 if page=='Your Beast' else 110),(540,250),ctrl,methods[i],card=(card,box))
   if page=='Rival':rivalbuttons.append(ref(fb))
 u.button('Back',beast[1],'BACK',(-630,-350),(230,80),ctrl,'Home')
 u.text('Choose Rival',rival[1],'CHOOSE YOUR RIVAL',(0,265),(850,45),26)
 u.text('Choose Difficulty',rival[1],'DIFFICULTY',(0,-85),(700,44),25,color=GOLD)
 levels=[]
 for i,label in enumerate(['EASY','NORMAL','HARD','GGs']):levels.append(ref(u.button(label,rival[1],label,(-315+i*210,-165),(190,72))[1]))
 u.button('Back',rival[1],'BACK',(-630,-350),(230,80),ctrl,'BackToBeasts')
 select,*_=u.button('Select Map',rival[1],'SELECT MAP',(570,-350),(360,90),ctrl,'SelectMap',primary=True)
 matchup,_=u.text('Match Setup',maps[1],'',(0,255),(1340,50),26)
 tiles=[ref(u.tile('Map '+str(i),maps[1],(-568+i*284,0),(264,310))) for i in range(5)]
 selection,_=u.text('Selected Map',maps[1],'Original Pitch selected',(0,-235),(1100,45),26,color=GOLD)
 u.button('Back',maps[1],'BACK',(-630,-350),(230,80),ctrl,'BackToRivals')
 u.button('Start Match',maps[1],'START MATCH',(570,-350),(360,90),ctrl,'StartMatch',primary=True)
 storetiles=[ref(u.tile('Store Pitch '+str(i),store[1],(-565+(i%2)*310,130-(i//2)*290),(290,275))) for i in range(4)]
 preview,_,_=u.raw('Focused Pitch',store[1],(400,145),(570,285))
 sn,_=u.text('Pitch Name',store[1],'LION PITCH',(400,-30),(600,48),32,color=GOLD)
 req,_=u.text('Match Requirement',store[1],'3 MATCHES',(400,-85),(600,40),24)
 price,_=u.text('Price',store[1],'500 COINS',(400,-135),(600,44),28,color=GOLD)
 buy,_,buyl,_=u.button('Buy Pitch',store[1],'BUY PITCH',(400,-210),(430,85),ctrl,'Buy',primary=True)
 notice,_=u.text('Purchase Notice',store[1],'',(400,-285),(620,55),22)
 _,_,backlabel,_=u.button('Back',store[1],'MAIN MENU',(-600,-365),(340,80),ctrl,'StoreBack')
 s.edit(ctrl).update(expandedRoot=ref(root),legacyRoot=ref(1185876281),homePage=ref(home[0]),beastPage=ref(beast[0]),rivalPage=ref(rival[0]),mapPage=ref(maps[0]),storePage=ref(store[0]),walletText=ref(wallet),titleText=ref(title),matchupText=ref(matchup),mapSelectionText=ref(selection),storeName=ref(sn),storeRequirement=ref(req),storePrice=ref(price),storeNotice=ref(notice),storeBackLabel=ref(backlabel),storePreview=ref(preview),storeButton=ref(storeb),selectMapButton=ref(select),buyButton=ref(buy),buyLabel=ref(buyl),mapTiles=tiles,storeTiles=storetiles,difficultyButtons=levels,rivalButtons=rivalbuttons)
 s.save();print('Authored MainMenu')

def match():
 s=Scene('Assets/Scenes/Match.unity');assert not s.by_guid(sg('Upgrades/MatchUpgradeUI')),'Already authored';u=UI(s)
 ctrl=s.add_component(1912463240,sg('Upgrades/MatchUpgradeUI'))
 root,design=u.safe(1912463249,'Match Menus (edit screens here)')
 coins,cg=u.text('Coins',9000000005,'Coins 0',(-190,-42),(340,46),28,5,color=GOLD,anchor=(1,1))
 # A floating pause control; score/time retain their existing separate text elements.
 _,_,_,pausebutton=u.button('Pause',9000000005,'PAUSE',(125,-44),(190,64),ctrl,'Pause','MatchUpgradeUI',anchor=(0,1))
 results,rr=u.panel('Match Results',design);pause,pr=u.panel('Pause Menu',design)
 u.text('Full Time',rr,'FULL TIME',(0,335),(900,65),40,color=GOLD)
 score,_=u.text('Final Score',rr,'0 — 0',(0,245),(800,110),76)
 details,_=u.text('Match Details',rr,'',(0,155),(1200,50),26)
 earnings,_=u.text('Coins Earned',rr,'+0 COINS',(0,75),(1000,60),40,color=GOLD)
 breakdown,_=u.text('Reward Breakdown',rr,'',(0,-65),(1050,190),27)
 saved,_=u.text('Save Status',rr,'',(0,-205),(1250,70),23,color=GOLD)
 for i,(label,method) in enumerate([('REMATCH','Rematch'),('STORE','Store'),('CHANGE MATCH','ChangeMatch'),('MAIN MENU','MainMenu')]):
  u.button(label,rr,label,(-540+i*360,-335),(330,90),ctrl,method,'MatchUpgradeUI',primary=i==0)
 u.text('Paused',pr,'PAUSED',(0,260),(1000,80),48,color=GOLD)
 diff,_=u.text('Difficulty',pr,'NORMAL',(0,170),(1000,50),28)
 for i,(label,method) in enumerate([('RESUME','Resume'),('RESTART MATCH','Rematch'),('MAIN MENU','MainMenu')]):u.button(label,pr,label,(0,50-i*135),(500,95),ctrl,method,'MatchUpgradeUI',primary=i==0)
 legacy=0
 for b in s.by_guid(BUTTON):
  if any(x.get('m_MethodName') in ('PauseMatch','OnPause') for x in s.data(b).get('m_OnClick',{}).get('m_PersistentCalls',{}).get('m_Calls',[])):legacy=s.data(b)['m_GameObject']['fileID']
 # Existing pause uses PauseUI.OnPause or GameManager.PauseMatch; find by name if needed.
 if not legacy:
  candidates=[g for g in s.gos() if s.data(g).get('m_Name') in ['PauseButton','Pause Button']]
  assert len(candidates)==1,candidates;legacy=candidates[0]
 s.edit(ctrl).update(coinRoot=ref(cg),resultRoot=ref(results),pauseRoot=ref(pause),pauseButtonRoot=ref(pausebutton),legacyPauseButton=ref(legacy),coins=ref(coins),earnings=ref(earnings),rewardBreakdown=ref(breakdown),savedLabel=ref(saved),resultScore=ref(score),resultDetails=ref(details),pauseDifficulty=ref(diff))
 for fid in s.by_guid(sg('Player/PlayerController')):
  d=s.data(fid);g=d['m_GameObject']['fileID'];s.add_component(g,sg('AI/TacticalOpponent'))
  if d['Role']==1:s.add_component(g,sg('AI/ReadableGoalkeeper'))
 gm=s.by_guid(sg('Core/GameManager'))[0];s.add_component(s.data(gm)['m_GameObject']['fileID'],sg('Upgrades/MatchRewards'))
 pp=s.by_guid(sg('Presentation/PitchPresentation'))[0];s.add_component(s.data(pp)['m_GameObject']['fileID'],sg('Upgrades/SelectedPitchPresentation'),{'presentation':ref(pp)})
 s.save();print('Authored Match')
rivalbuttons=[]
if __name__=='__main__':menu();match()
