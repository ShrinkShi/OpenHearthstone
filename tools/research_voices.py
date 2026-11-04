"""查看卡牌事件声音的默认/条件来源。"""
import json
from .importer import Importer


def main():
    imp = Importer()
    definition = imp.reader.definition('BT_351')
    root, _, _ = imp.reader.resolve(definition['m_PlayEffectDef']['m_SoundSpellPaths'][0], 'zhcn')
    for obj, tree in imp.reader.walk(root, 'zhcn'):
        if obj.type.name == 'MonoBehaviour' and tree and ('m_CardSoundData' in tree or 'm_AudioClip' in tree):
            print(json.dumps(tree, ensure_ascii=False)[:5000])


if __name__ == '__main__':
    main()
