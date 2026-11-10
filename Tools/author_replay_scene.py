"""One-time replay scene authoring on the progression branch. Runtime layout remains serialized."""
from author_upgrade_scenes import UI, sg, ref
from unity_scene_tools import Scene, vec
import copy
s = Scene('Assets/Scenes/Match.unity')
assert not s.by_guid(sg('Replay/CinematicReplay')), 'Replay scene already authored'
u = UI(s)
manager = s.data(s.by_guid(sg('Core/GameManager'))[0])['m_GameObject']['fileID']
controller = s.add_component(manager, sg('Replay/CinematicReplay'))
main_canvas = s.go('Canvas'); main_rt = s.transform(main_canvas)
canvas_group = s.add(225, 'CanvasGroup', {
    'm_ObjectHideFlags':0, 'm_CorrespondingSourceObject':ref(0), 'm_PrefabInstance':ref(0), 'm_PrefabAsset':ref(0),
    'm_GameObject':ref(main_canvas), 'm_Enabled':1, 'm_Alpha':1, 'm_Interactable':1, 'm_BlocksRaycasts':1, 'm_IgnoreParentGroups':0})
s.edit(main_canvas)['m_Component'].append({'component':ref(canvas_group)})
canvas, rt = u.obj('Replay UI (edit fixed replay overlay here)', main_rt, active=False)
s.stretch(rt)
s.edit(main_rt)['m_Children'] = [x for x in s.data(main_rt)['m_Children'] if x['fileID'] != rt]
s.edit(rt)['m_Father'] = ref(0)
for fid, doc in s.docs.items():
    if doc['kind'] == 1660057539: s.edit(fid)['m_Roots'].append(ref(rt))
for component in s.components(main_canvas):
    d = copy.deepcopy(s.data(component))
    if s.kind(component) not in [223,114]: continue
    if s.kind(component)==114 and d.get('m_EditorClassIdentifier','') not in ['UnityEngine.UI::UnityEngine.UI.CanvasScaler','UnityEngine.UI::UnityEngine.UI.GraphicRaycaster']: continue
    if s.kind(component)==114 and d.get('m_Script',{}).get('guid') not in ['0cd44c1031e13a943bb63640046fad76','dc42784cf147c0c48a680349fa168899']: continue
    d['m_GameObject'] = ref(canvas)
    if s.kind(component)==223: d.update(m_SortingOrder=100,m_OverrideSorting=1,m_RenderMode=0,m_Camera=ref(0))
    c=s.add(s.kind(component), next(iter(s.docs[component]['data'])),d)
    s.edit(canvas)['m_Component'].append({'component':ref(c)})
# Make sure CanvasScaler/GraphicRaycaster were copied even if old editor type names are blank.
for g in ['0cd44c1031e13a943bb63640046fad76','dc42784cf147c0c48a680349fa168899']:
    if not any(s.data(c).get('m_Script',{}).get('guid')==g for c in s.components(canvas)):
        original=s.by_guid(g)[0];d=copy.deepcopy(s.data(original));d['m_GameObject']=ref(canvas)
        c=s.add(114,'MonoBehaviour',d);s.edit(canvas)['m_Component'].append({'component':ref(c)})
block,br=u.obj('Replay Input Shield',rt);s.stretch(br);u.image(block,{'r':0,'g':0,'b':0,'a':.001},1)
_, design=u.safe(rt,'Replay Safe Area')
u.text('Replay Label',design,'REPLAY',(135,-55),(220,55),35,3,anchor=(0,1))
button,feedback,label,go=u.button('Skip Replay',design,'SKIP',(-145,-55),(220,75),controller,'Skip','CinematicReplay',primary=True,anchor=(1,1))
s.edit(button)['m_OnClick']['m_PersistentCalls']['m_Calls'][0]['m_TargetAssemblyTypeName']='BeastSoccer.Replay.CinematicReplay, BeastSoccer.Runtime'
proxies=s.by_guid(sg('Presentation/PlayerVisualProxy'))
ball=s.by_guid(sg('Presentation/BallVisualProxy'))[0]
cameras=[fid for fid,d in s.docs.items() if d['kind']==20]
assert len(cameras)==1,cameras
s.edit(controller).update(bufferSeconds=6,samplesPerSecond=60,leadSeconds=3.5,finalSecondSpeed=.4,goalAnnouncementSeconds=.55,
    replayView=ref(cameras[0]),cameraOffset=vec(6,6,-4.5),lookIntoPitch=3.5,lookHeight=.9,fieldOfView=62,
    keeperSilhouette={'r':.055,'g':.055,'b':.06,'a':1},players=[ref(x) for x in proxies],ballVisual=ref(ball),gameplayHUD=ref(canvas_group),replayUI=ref(canvas),
    goalVisualRoots=[ref(s.transform(s.go(name))) for name in ['Positive Goal (replace visual here)','Negative Goal (replace visual here)']])
s.save()
print('Authored replay camera settings, actor references and separate overlay canvas.')
