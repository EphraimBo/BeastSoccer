from pathlib import Path
import re, copy, yaml

class UnityDumper(yaml.SafeDumper):
    def ignore_aliases(self, data): return True
def represent_map(dumper, data):
    flow = bool(data) and (set(data) <= {'fileID','guid','type'} or set(data) <= {'x','y','z','w'} or set(data) <= {'r','g','b','a'})
    return dumper.represent_mapping('tag:yaml.org,2002:map',data,flow_style=flow)
UnityDumper.add_representer(dict, represent_map)
UnityDumper.add_representer(type(None),lambda d,v:d.represent_scalar('tag:yaml.org,2002:null',''))

def ref(fid): return {'fileID':int(fid)}
def vec(x=0,y=0,z=None,w=None):
    d={'x':x,'y':y}
    if z is not None:d['z']=z
    if w is not None:d['w']=w
    return d

class Scene:
    def __init__(self,path):
        self.path=Path(path)
        self.docs={}
        self.next_id=9000000000
        text=self.path.read_text()
        self.preamble=text[:text.index('--- !u!')]
        for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)( stripped)?\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S):
            fid=int(m[2])
            self.docs[fid]={'kind':int(m[1]),'stripped':m[3] or '', 'raw':m[4],
                            'data':yaml.safe_load(m[4]),'changed':False}
    def data(self,fid):
        d=self.docs[int(fid)]['data']
        return next(iter(d.values()))
    def edit(self,fid):
        self.docs[int(fid)]['changed']=True
        return self.data(fid)
    def kind(self,fid):return self.docs[int(fid)]['kind']
    def fresh(self):
        self.next_id+=1
        while self.next_id in self.docs:self.next_id+=1
        return self.next_id
    def add(self,kind,name,body):
        fid=self.fresh()
        self.docs[fid]={'kind':kind,'stripped':'','data':{name:body},'changed':True,'raw':''}
        return fid
    def gos(self):return [fid for fid,d in self.docs.items() if d['kind']==1]
    def go(self,name,parent=None):
        matches=[fid for fid in self.gos() if self.data(fid).get('m_Name')==name]
        if parent is not None: matches=[g for g in matches if self.data(self.transform(g)).get('m_Father',ref(0))['fileID']==parent]
        assert len(matches)==1,(self.path.name,name,parent,matches)
        return matches[0]
    def transform(self,go):
        return next(c['component']['fileID'] for c in self.data(go)['m_Component'] if self.kind(c['component']['fileID']) in (4,224))
    def components(self,go):return [c['component']['fileID'] for c in self.data(go)['m_Component']]
    def component(self,go,guid):
        matches=[fid for fid in self.components(go) if self.data(fid).get('m_Script',{}).get('guid')==guid]
        assert len(matches)==1,(go,guid,matches)
        return matches[0]
    def by_guid(self,guid):
        return [fid for fid,d in self.docs.items() if d['kind']==114 and self.data(fid).get('m_Script',{}).get('guid')==guid]
    def add_component(self,go,guid,fields=None):
        body={'m_ObjectHideFlags':0,'m_CorrespondingSourceObject':ref(0),'m_PrefabInstance':ref(0),'m_PrefabAsset':ref(0),
              'm_GameObject':ref(go),'m_Enabled':1,'m_EditorHideFlags':0,
              'm_Script':{'fileID':11500000,'guid':guid,'type':3},'m_Name':'','m_EditorClassIdentifier':''}
        body.update(fields or {})
        fid=self.add(114,'MonoBehaviour',body)
        self.edit(go)['m_Component'].append({'component':ref(fid)})
        return fid
    def add_ui(self,name,parent,active=True):
        go=self.add(1,'GameObject',{'m_ObjectHideFlags':0,'m_CorrespondingSourceObject':ref(0),
             'm_PrefabInstance':ref(0),'m_PrefabAsset':ref(0),'serializedVersion':6,'m_Component':[],
             'm_Layer':5,'m_Name':name,'m_TagString':'Untagged','m_Icon':ref(0),
             'm_NavMeshLayer':0,'m_StaticEditorFlags':0,'m_IsActive':int(active)})
        rt=self.add(224,'RectTransform',{'m_ObjectHideFlags':0,'m_CorrespondingSourceObject':ref(0),
             'm_PrefabInstance':ref(0),'m_PrefabAsset':ref(0),'m_GameObject':ref(go),
             'm_LocalRotation':vec(0,0,0,1),'m_LocalPosition':vec(0,0,0),'m_LocalScale':vec(1,1,1),
             'm_ConstrainProportionsScale':0,'m_Children':[],'m_Father':ref(parent),
             'm_LocalEulerAnglesHint':vec(0,0,0),'m_AnchorMin':vec(.5,.5),'m_AnchorMax':vec(.5,.5),
             'm_AnchoredPosition':vec(0,0),'m_SizeDelta':vec(100,100),'m_Pivot':vec(.5,.5)})
        self.edit(go)['m_Component'].append({'component':ref(rt)})
        self.edit(parent).setdefault('m_Children',[]).append(ref(rt))
        return go,rt
    def add_renderer(self,go):
        fid=self.add(222,'CanvasRenderer',{'m_ObjectHideFlags':0,'m_CorrespondingSourceObject':ref(0),
            'm_PrefabInstance':ref(0),'m_PrefabAsset':ref(0),'m_GameObject':ref(go),'m_CullTransparentMesh':1})
        self.edit(go)['m_Component'].append({'component':ref(fid)})
    def parent(self,rt,parent,first=False):
        old=self.data(rt)['m_Father']['fileID']
        if old:
            self.edit(old)['m_Children']=[x for x in self.data(old).get('m_Children',[]) if x['fileID']!=rt]
        self.edit(rt)['m_Father']=ref(parent)
        children=self.edit(parent).setdefault('m_Children',[])
        if first:children.insert(0,ref(rt))
        else:children.append(ref(rt))
    def place(self,rt,anchor,pos,size):
        d=self.edit(rt)
        d.update(m_AnchorMin=vec(*anchor),m_AnchorMax=vec(*anchor),m_Pivot=vec(.5,.5),
                 m_AnchoredPosition=vec(*pos),m_SizeDelta=vec(*size),m_LocalScale=vec(1,1,1),
                 m_LocalRotation=vec(0,0,0,1))
    def stretch(self,rt):
        d=self.edit(rt)
        d.update(m_AnchorMin=vec(0,0),m_AnchorMax=vec(1,1),m_SizeDelta=vec(0,0),m_AnchoredPosition=vec(0,0))
    def remove_component(self,fid):
        go=self.data(fid).get('m_GameObject',ref(0))['fileID']
        if go in self.docs:
            self.edit(go)['m_Component']=[x for x in self.data(go)['m_Component'] if x['component']['fileID']!=fid]
        self.docs.pop(fid)
    def remove_go(self,go):
        rt=self.transform(go)
        for child in list(self.data(rt).get('m_Children',[])):
            c=child['fileID']
            if c in self.docs and self.data(c).get('m_GameObject',ref(0))['fileID'] in self.docs:
                self.remove_go(self.data(c)['m_GameObject']['fileID'])
        parent=self.data(rt).get('m_Father',ref(0))['fileID']
        if parent in self.docs:
            self.edit(parent)['m_Children']=[x for x in self.data(parent).get('m_Children',[]) if x['fileID']!=rt]
        for c in self.components(go): self.docs.pop(c,None)
        self.docs.pop(go)
        for fid,d in self.docs.items():
            if d['kind']==1660057539:
                self.edit(fid)['m_Roots']=[x for x in self.data(fid)['m_Roots'] if x['fileID']!=rt]
    def save(self):
        text=self.preamble
        for fid,doc in self.docs.items():
            text+=f"--- !u!{doc['kind']} &{fid}{doc['stripped']}\n"
            text+=yaml.dump(doc['data'],Dumper=UnityDumper,sort_keys=False,allow_unicode=True,width=120) if doc['changed'] else doc['raw']
        self.path.write_text(text)

def guid(path):
    return re.search(r'^guid: (\w+)',Path(str(path)+'.meta').read_text(),re.M)[1]
