"""导出当前源码、全部场景和Prefab的文字与字体字符集；不生成字体资产。"""
from pathlib import Path
import json
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "MaJiangUnity/Assets"
OUTPUT = ASSETS / "Presentation"

def export():
    fragments = []
    coverage = []
    # 跳过注释，包含玩家可见的动态提示、规则/网络错误以及运行时对象名。
    tokens = re.compile(r'//[^\n]*|/\*.*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|.', re.S)
    sources = list((ASSETS / "Scripts").rglob("*.cs")) + list((ASSETS / "Editor").rglob("*.cs"))
    for path in sources:
        for token in tokens.findall(path.read_text(encoding="utf-8-sig")):
            if token.startswith(('//', '/*')):
                continue
            # 插值表达式里的嵌套引号、字符字面量也要覆盖，如座位标记◀。
            coverage.append(token)
            if token.startswith(('"', '@"')):
                value = token[2:-1].replace('""', '"') if token.startswith('@"') else token[1:-1]
                if not token.startswith('@"'):
                    value = re.sub(r'\\u([0-9a-fA-F]{4})', lambda m: chr(int(m[1],16)), value)
                    value = value.replace('\\n', '\n').replace('\\r', '\r').replace('\\t', '\t').replace('\\"', '"')
                fragments.append(value)
    for path in sorted(list(ASSETS.rglob('*.unity')) + list(ASSETS.rglob('*.prefab'))):
        text = path.read_text(encoding="utf-8-sig")
        for value in re.findall(r'^\s*(?:m_text|m_Text|m_Name|value): ("(?:\\.|[^"\\])*"|[^\n]*)', text, re.M):
            if value.startswith('"'):
                # Unity YAML的双引号字符串可跨行，并使用JSON之外的\x转义。
                value = re.sub(r'\\\r?\n\s*', '', value)
                value = json.loads(re.sub(r'\\x([0-9a-fA-F]{2})', r'\\u00\1', value))
            if value and not re.fullmatch(r'[-+\d.eE ]+', value):
                fragments.append(value)
    content = "".join(fragments + coverage)
    chinese = sorted({c for c in content if '\u3400' <= c <= '\u9fff' or '\uf900' <= c <= '\ufaff'})
    full = sorted(set(chinese) | {chr(i) for i in range(32,127)} | {c for c in content if ord(c)>127 and not c.isspace()})
    for name,chars in (("界面汉字.txt",chinese),("界面完整字符集.txt",full)):
        path=OUTPUT/name
        path.write_text("".join(chars)+"\n", encoding="utf-8")
        meta=Path(str(path)+".meta")
        if not meta.exists(): meta.write_text("fileFormatVersion: 2\nguid: "+uuid.uuid4().hex+"\n",encoding="utf-8")
        print(f"{name}: {len(chars)} characters")
    # 此文件用于查阅与继续拼UI，保留动态插值模板；字体图集应使用完整字符集文件。
    phrases = sorted({text.strip() for text in fragments if text.strip()})
    (OUTPUT / '界面全部文案.txt').write_text('\n\n'.join(phrases)+'\n', encoding='utf-8')
    assert '◀' in full, 'Nested interpolated UI symbols must be included'
    print(f'UI text entries: {len(phrases)}')

if __name__ == "__main__":
    export()
