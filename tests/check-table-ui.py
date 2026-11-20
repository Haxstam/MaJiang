"""检查牌桌资源中的摸牌尺寸与四席绑定；不启动Unity，也不生成资源。"""
from pathlib import Path
import json
import re

ASSETS = Path(__file__).resolve().parents[1] / 'MaJiangUnity/Assets'


def blocks(path):
    source = path.read_text(encoding='utf-8-sig')
    matches = list(re.finditer(r'^--- !u!(\d+) &(-?\d+).*?(?=^--- !u!|\Z)', source, re.M | re.S))
    assert len(matches) == len({m[2] for m in matches}), f'{path.name}: duplicate fileIDs'
    return {m[2]: m[0] for m in matches}


def ref(body, field):
    return re.search(r'^  ' + field + r': \{fileID: (-?\d+)', body, re.M)[1]


def object_name(body):
    value = re.search(r'^  m_Name: (.*)', body, re.M)[1]
    return json.loads(value) if value.startswith('"') else value


def modifications(body):
    return {(fileid, field): value for fileid, field, value in re.findall(
        r'- target: \{fileID: (\d+), guid: \w+, type: \d+\}\n      propertyPath: ([^\n]+)\n      value: ([^\n]*)', body)}


def children(body):
    return re.findall(r'fileID: (\d+)', re.search(r'  m_Children:(.*?)(?=  m_Father:)', body, re.S)[1])


hand = blocks(ASSETS / 'Prefabs/手牌区.prefab')
strip = next(body for body in hand.values() if '\n  Concealed:' in body and '\n  Drawn:' in body)
draw = ref(strip, 'Drawn')
concealed = ref(strip, 'Concealed')
assert draw not in children(hand[concealed]), 'The drawn tile must not be sorted into the concealed hand.'
draw_instance = hand[ref(hand[draw], 'm_PrefabInstance')]
assert re.search(r'm_TransformParent: \{fileID: (\d+)', draw_instance)[1] == ref(hand[concealed], 'm_Father'), 'Drawn card must be a sibling of the concealed area.'
props = modifications(draw_instance)
assert props[('1428142633425908874', 'm_AspectMode')] == '1', 'The editor preview must use WidthControlsHeight (uGUI enum value 1).'
width = float(props[('6015176286968143671', 'm_SizeDelta.x')])
height = float(props[('6015176286968143671', 'm_SizeDelta.y')])
# Unity可将由布局驱动的尺寸、缩放保存为0；固定牌宽由布局的独立字段持有。
assert width >= 0 and height >= 0
assert float(re.search(r'  TileWidth: (.*)', strip)[1]) > 0, 'The hand strip must own a positive tile width independent of driven RectTransform fields.'
draw_name = props[('7352361702785972324', 'm_Name')]
assert (json.loads(draw_name) if draw_name.startswith('"') else draw_name) == '摸牌'
print('PASS drawn Card: separate from concealed hand, explicit layout width and width-driven aspect ratio')

table = blocks(ASSETS / 'Prefabs/牌桌画布.prefab')
scene = (ASSETS / 'Scenes/TableDebug.unity').read_text(encoding='utf-8-sig')
assert 'm_SourcePrefab: {fileID: 100100000, guid: 70a4cec918ff2ce47a5e7e4c76d9c3ab, type: 3}' in scene
view = next(body for body in table.values() if '\n  HandStrips:' in body and '\n  Players:' in body)
seats = ('自家', '下家', '对家', '上家')
instances = [table[ref(table[fileid], 'm_PrefabInstance')] for fileid in re.findall(
    r'fileID: (\d+)', re.search(r'  HandStrips:\n((?:  - .*\n)+)', view)[1])]
assert len(instances) == 4
for seat, instance in zip(seats, instances):
    p = modifications(instance)
    assert json.loads(p[('4724044302387395994', 'm_Name')]) == seat + '手牌区'
    x = float(p[('8185277683600203285', 'm_AnchoredPosition.x')])
    y = float(p[('8185277683600203285', 'm_AnchoredPosition.y')])
    assert {'自家': y < 0, '下家': x > 0, '对家': y > 0, '上家': x < 0}[seat], f'{seat}: hand strip is on the wrong side.'
players = re.search(r'  Players:\n(.*?)(?=  Indicators:)', view, re.S)[1]
for seat, fields in zip(seats, re.findall(r'  - Name: .*?(?=  - Name:|\Z)', players, re.S)):
    for field, suffix in (('Name', '名称'), ('Wind', '风位'), ('Points', '点数')):
        component = re.search(field + r': \{fileID: (\d+)', fields)[1]
        game_object = ref(table[component], 'm_GameObject')
        assert object_name(table[game_object]) == ('名称' if field == 'Name' else seat + suffix), f'{seat}: wrong {field} binding.'
print('PASS square seating: bottom self, right next, top opposite, left previous; wind and point bindings agree')

indicators = table[ref(view, 'Indicators')]
cards = children(indicators)


def has_card_widget(rect):
    if 'guid: 102380285c82ad347b7df688c77337af' in table[rect]:
        return True
    game_object = table[ref(table[rect], 'm_GameObject')]
    return any('guid: ef6000feb5c543eebe234005098593f5' in table[component]
               for component in re.findall(r'component: \{fileID: (\d+)\}', game_object))


assert len(cards) == 5
assert all(has_card_widget(card) for card in cards), 'Indicator cards must be direct children, without layout wrappers.'
choice = table[ref(view, 'ChoicePanel')]
cancel_go = ref(table[ref(choice, 'Cancel')], 'm_GameObject')
cancel_rect = next(body for body in table.values() if body.startswith('--- !u!224') and f'm_GameObject: {{fileID: {cancel_go}}}' in body)
for field in ('m_AnchorMin', 'm_AnchorMax', 'm_Pivot'):
    assert field + ': {x: 1, y: 1}' in cancel_rect, 'Cancel must stay inside the top-right corner.'
assert 'm_AnchoredPosition: {x: -6, y: -6}' in cancel_rect
lights = re.findall(r'fileID: (\d+)', re.search(r'  TurnLights:\n((?:  - .*\n)+)', view)[1])
assert len(lights) == 4
for seat, light in zip(seats, lights):
    assert object_name(table[ref(table[light], 'm_GameObject')]) == seat + '指示'
assert object_name(table[ref(table[ref(view, 'DebugHandLabel')], 'm_GameObject')]) == '调试文字'
print('PASS indicators, anchored cancel button, four turn lights and controlled-hand label')

settlement = table[ref(view, 'SettlementView')]
settlement_object = ref(settlement, 'm_GameObject')
assert object_name(table[settlement_object]) == '结算窗口'
hand_strip = table[ref(settlement, 'Hand')]
assert ref(hand_strip, 'Drawn') == '0'
hand_cards = children(table[ref(hand_strip, 'Concealed')])
assert len(hand_cards) == 13 and all(has_card_widget(card) for card in hand_cards)
winning_tile = ref(table[ref(settlement, 'WinningTile')], 'm_GameObject')
winning_rect = next(fileid for fileid, body in table.items()
                    if body.startswith('--- !u!224') and f'm_GameObject: {{fileID: {winning_tile}}}' in body)
assert winning_rect not in hand_cards and object_name(table[winning_tile]) == '所和牌'
for field in ('Dora', 'Ura'):
    indicators = children(table[ref(settlement, field)])
    assert len(indicators) == 5 and all(has_card_widget(card) for card in indicators)
rows = re.findall(r'fileID: (\d+)', re.search(r'  PointRows:\n((?:  - .*\n)+)', settlement)[1])
assert len(rows) == len(seats)
point_prefab = blocks(ASSETS / 'Prefabs/点数情况.prefab')
for seat, row in zip(seats, rows):
    instance = table[ref(table[row], 'm_PrefabInstance')]
    root = point_prefab[ref(table[row], 'm_CorrespondingSourceObject')]
    assert json.loads(modifications(instance)[(ref(root, 'm_GameObject'), 'm_Name')]) == seat + '点数情况'
print('PASS settlement slots: 13 concealed cards, separate winning tile, two indicator lists and four relative score rows')

tenpai = table[ref(view, 'TenpaiHint')]
assert object_name(table[ref(tenpai, 'm_GameObject')]) == '听牌提示按钮'
for panel, name in ((ref(table[ref(tenpai, 'List')], 'm_GameObject'), '听牌提示'),
                    (ref(tenpai, 'FuritenPanel'), '振听提示')):
    assert object_name(table[panel]) == name
    components = [table[fileid] for fileid in re.findall(r'component: \{fileID: (\d+)\}', table[panel])]
    canvas_group = next(body for body in components if body.startswith('--- !u!225 '))
    assert ref(canvas_group, 'm_GameObject') == panel and '\n  m_BlocksRaycasts: 0\n' in canvas_group
assert 'guid: 61070b3fd9d8eca4e9ed96456ab04a58' in tenpai
entry = blocks(ASSETS / 'Prefabs/听牌情况.prefab')
entry_rect = entry[ref(tenpai, 'EntryPrefab')]
entry_children = [entry[child] for child in children(entry_rect)]
assert any('guid: 102380285c82ad347b7df688c77337af' in child for child in entry_children)
assert {object_name(entry[ref(child, 'm_GameObject')]) for child in entry_children if '\n  m_GameObject:' in child} == {'振听提示', '番数', '剩余张数'}
card_prefab = blocks(ASSETS / 'Prefabs/Card.prefab')
card_widget = next(body for body in card_prefab.values() if '\n  BlackMask:' in body)
black_mask = card_prefab[ref(card_widget, 'BlackMask')]
assert object_name(card_prefab[ref(black_mask, 'm_GameObject')]) == 'BlackMask'
assert 'm_RaycastTarget: 0' in black_mask
print('PASS tenpai and furiten panels pass through clicks, with bound wait entries and riichi mask')

assert '\n  DoraSweep:\n' in view and '\n  DiscardFrame:\n' in view
assert all(f'    {field}:' in view for field in ('Duration', 'Pause', 'Width', 'Angle', 'ResponsePeriod',
                                                 'ResponseScaleMin', 'ResponseScaleMax', 'ResponseOpacityMin', 'ResponseOpacityMax'))
frame_meta = (ASSETS / 'Images/圆角矩形框.psd.meta').read_text(encoding='utf-8-sig')
frame_guid = re.search(r'^guid: (\w+)$', frame_meta, re.M)[1]
frame_id = re.search(r'^      internalID: (-?\d+)$', frame_meta, re.M)[1]
tile_art = (ASSETS / 'Presentation/MahjongTileArt.asset').read_text(encoding='utf-8-sig')
assert f'DiscardFrame: {{fileID: {frame_id}, guid: {frame_guid}, type: 3}}' in tile_art
print('PASS configurable sweep and discard frame settings, with the supplied PSD sprite bound')

font_guid = '06747f952e6fa1a4591c2e339715eb1f'
for prefab in ('牌桌画布', '点数情况', '番种面板', '操作按钮', '鸣牌选择组', '听牌情况'):
    text = (ASSETS / ('Prefabs/' + prefab + '.prefab')).read_text(encoding='utf-8-sig')
    refs = re.findall(r'^  (?:m_fontAsset|m_sharedMaterial): (.+)$', text, re.M)
    assert refs and all(font_guid in ref_value for ref_value in refs), f'{prefab}: mixed TMP font or materials'
font_overrides = re.findall(r'propertyPath: (?:m_fontAsset|m_sharedMaterial)\n      value:[^\n]*\n      objectReference: ([^\n]+)', scene)
assert all(font_guid in value for value in font_overrides), 'Scene overrides the shared font with a different asset.'
font = (ASSETS / 'TextMesh Pro/Fonts/SourceHanSansSC-Heavy SDF.asset').read_text(encoding='utf-8-sig')
assert 'm_AtlasPopulationMode: 1' in font and 'm_SourceFontFile: {fileID: 12800000,' in font
print('PASS table and settlement text share one dynamically populated Source Han Sans font asset')
