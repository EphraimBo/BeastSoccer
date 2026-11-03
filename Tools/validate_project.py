#!/usr/bin/env python3
"""Source/serialization checks only. Unity compilation and PlayMode tests are separate checks."""
from pathlib import Path
import json
import re
import sys
import yaml
from tree_sitter import Language, Parser
import tree_sitter_c_sharp

ROOT = Path(__file__).resolve().parents[1]
errors = []
checks = 0

def check(condition, message):
    global checks
    checks += 1
    if not condition:
        errors.append(message)

def documents(path):
    result = {}
    for match in re.finditer(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)',
                             path.read_text(), re.M | re.S):
        data = yaml.safe_load(match[3])
        result[int(match[2])] = (int(match[1]), next(iter(data.values())))
    return result

def walk(value):
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from walk(child)
    elif isinstance(value, list):
        for child in value:
            yield from walk(child)

guid_paths = {}
for meta in (ROOT / 'Assets').rglob('*.meta'):
    match = re.search(r'^guid: (\w+)', meta.read_text(), re.M)
    check(match is not None, f'Metadata without GUID: {meta.relative_to(ROOT)}')
    if not match:
        continue
    guid = match[1]
    check(guid not in guid_paths, f'Duplicate asset GUID: {guid}')
    guid_paths[guid] = Path(str(meta)[:-5])

parser = Parser(Language(tree_sitter_c_sharp.language()))
sources = {}
for path in (ROOT / 'Assets').rglob('*.cs'):
    content = path.read_bytes()
    tree = parser.parse(content)
    check(not tree.root_node.has_error, f'C# syntax error: {path.relative_to(ROOT)}')
    check(Path(str(path) + '.meta').exists(), f'Missing script metadata: {path.relative_to(ROOT)}')
    sources[path] = content.decode()

# Every direct GameConfig access must resolve to a surviving public member.
config_source = (ROOT / 'Assets/Scripts/Data/GameConfig.cs').read_text()
config_members = set(re.findall(r'public\s+(?:static\s+)?[\w.<>]+\s+(\w+)', config_source))
for declaration in re.findall(r'public float ([^;]+);', config_source):
    config_members.update(re.findall(r'(?:^|,)\s*(\w+)\s*(?:=|$)', declaration))
for path, source in sources.items():
    for member in re.findall(r'GameConfig\.Instance\??\.(\w+)', source):
        check(member in config_members, f'Unknown GameConfig member {member}: {path.relative_to(ROOT)}')

# Preserve known package/built-in references from the imported baseline; new unresolved GUIDs fail.
external = set(json.loads((ROOT / 'Tools/package_reference_guids.json').read_text()))
asset_suffixes = {'.unity', '.asset', '.mat', '.prefab', '.anim', '.controller', '.meta'}
for path in (ROOT / 'Assets').rglob('*'):
    if not path.is_file() or path.suffix not in asset_suffixes:
        continue
    for guid in re.findall(r'guid: ([0-9a-f]{32})', path.read_text(errors='replace')):
        check(guid in guid_paths or guid in external or guid.startswith('0000000000000000'),
              f'New unresolved asset GUID {guid}: {path.relative_to(ROOT)}')

scenes = {}
for name in ['MainMenu', 'Match']:
    path = ROOT / f'Assets/Scenes/{name}.unity'
    docs = documents(path)
    scenes[name] = docs
    for fid, (kind, data) in docs.items():
        for item in walk(data):
            if 'fileID' in item and 'guid' not in item and item['fileID']:
                check(item['fileID'] in docs, f'{name}: missing local reference {item["fileID"]} from {fid}')
            method = item.get('m_MethodName')
            target = item.get('m_Target', {}).get('fileID')
            if not method or not target or target not in docs:
                continue
            script_guid = docs[target][1].get('m_Script', {}).get('guid')
            source_path = guid_paths.get(script_guid)
            if source_path in sources:
                check(re.search(r'\b' + re.escape(method) + r'\s*\(', sources[source_path]) is not None,
                      f'{name}: missing callback {source_path.stem}.{method}')
        if kind == 114:
            script_guid = data.get('m_Script', {}).get('guid')
            check(script_guid in guid_paths or script_guid in external,
                  f'{name}: unresolved script on component {fid}')
        if kind in (4, 224):
            for child in data.get('m_Children', []):
                target = docs.get(child['fileID'])
                if target is not None:
                    actual_parent = target[1].get('m_Father', {}).get('fileID')
                    if actual_parent is None:
                        prefab = docs.get(target[1].get('m_PrefabInstance', {}).get('fileID'))
                        if prefab is not None:
                            actual_parent = prefab[1].get('m_Modification', {}).get('m_TransformParent', {}).get('fileID')
                    check(actual_parent == fid,
                          f'{name}: inconsistent parent link for {child["fileID"]}')

# Baseline features remain addressable: the roster is still eight authored players.
match = scenes['Match']
player_guid = next(g for g,p in guid_paths.items() if p.name == 'PlayerController.cs')
check(sum(1 for kind,data in match.values() if kind == 114 and
          data.get('m_Script', {}).get('guid') == player_guid) == 8, 'Larger-team authored roster was lost.')
ball_visual_guid = next(g for g,p in guid_paths.items() if p.name == 'BallVisualProxy.cs')
for kind, data in match.values():
    if kind != 114 or data.get('m_Script', {}).get('guid') != ball_visual_guid:
        continue
    visual_id = data.get('visualModel', {}).get('fileID')
    check(visual_id in match, 'Assign the authored ball model explicitly.')
    if visual_id in match:
        target_go = match[visual_id][1].get('m_GameObject', {}).get('fileID')
        target_name = match.get(target_go, (None, {}))[1].get('m_Name', '')
        check(target_name != 'BallModel_PLACEHOLDER', 'The ball still targets the old placeholder.')

# Both intentionally layered menu backdrops are serialized and have valid parents.
menu_names = [data.get('m_Name') for kind,data in scenes['MainMenu'].values() if kind == 1]
for name in ['Fullscreen Backdrop (covers screen edges)', 'Menu Artwork Backdrop (behind menu)',
             'Menu (edit layout here)']:
    check(name in menu_names, f'Missing authored menu object: {name}')

# New input module is serialized once in each scene, with an actions asset assigned.
for name, docs in scenes.items():
    modules = [data for kind,data in docs.values() if kind == 114 and
               data.get('m_Script', {}).get('guid') == '01614664b831546d2ae94a42149d80ac']
    check(len(modules) == 1 and modules[0].get('m_ActionsAsset', {}).get('fileID') != 0,
          f'{name}: input module/action wiring is incomplete.')
    check(not any(data.get('m_Script', {}).get('guid') == '4f231c4fb786f3946a6b90b886c48677'
                  for kind,data in docs.values() if kind == 114), f'{name}: legacy input module remains.')

# Approved named-character sequences remain complete for both kits.
for character in ['Leo', 'Volt', 'Goro']:
    actions = ['Idle_Ready_ThreeQuarter', 'Idle_Holding_ThreeQuarter', 'Catch', 'Dive_Near', 'Dive_Far',
               'Recover', 'Throw_ThreeQuarter'] if character == 'Goro' else [
               'Run_Side', 'Run_FrontThreeQuarter', 'Run_Front', 'Run_BackThreeQuarter', 'Run_Back', 'Shoot', 'Tackle']
    if character == 'Volt':
        actions.append('Ability')
    for kit in ['Home_Orange', 'Away_Blue']:
        for action in actions:
            directory = ROOT / f'Assets/Resources/BeastArtV8/{character}/{kit}/{action}'
            check(directory.exists() and any(directory.glob('*.png')), f'Missing approved sequence: {character}/{kit}/{action}')

# Validate the remaining package graph without fetching or modifying the project.
manifest = json.loads((ROOT / 'Packages/manifest.json').read_text())['dependencies']
locked = json.loads((ROOT / 'Packages/packages-lock.json').read_text())['dependencies']
for name in manifest:
    check(name in locked and locked[name]['depth'] == 0, f'Unresolved direct dependency: {name}')
for name, info in locked.items():
    for dependency in info.get('dependencies', {}):
        check(dependency in locked, f'Incomplete package graph: {name} -> {dependency}')

if errors:
    for error in errors:
        print('FAIL:', error)
    print(f'{len(errors)} failure(s) across {checks} checks.')
    sys.exit(1)
print(f'PASS: {checks} source/serialization checks; {len(sources)} C# files; two product scenes.')
print('Not a Unity compilation, PlayMode run, rendered comparison or device test.')
