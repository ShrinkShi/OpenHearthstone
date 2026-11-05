"""取得并校验固定历史元数据；不下载或修改官方客户端资产。"""
import hashlib
import importlib.util
import shutil
import tempfile
import urllib.request
from pathlib import Path
from .importer import PROJECT

COMMIT = '742c0010782909dbfc3ee689d46e71bbb94e3c15'
URL = f'https://raw.githubusercontent.com/HearthSim/hsdata/{COMMIT}/CardDefs.xml'
DIGEST = '96c094b6971b44c8238a7aa05aa2831d809b7ea2d943e50eee8b165e1db1b099'
FIREPLACE_COMMIT = '47a2572a000db66645bb74a425a090d51f1004fa'
FIREPLACE_DIGEST = '7fccd87b7e20f9864fe0664aa9d9304213981dc6c883fe71010cceda1b9af7ac'
FIREPLACE_URL = f'https://media.githubusercontent.com/media/jleclanche/fireplace/{FIREPLACE_COMMIT}/fireplace/cards/CardDefs.xml'


def checksum(path):
    with path.open('rb') as source:
        return hashlib.file_digest(source, 'sha256').hexdigest()


def prepare_fireplace_carddefs(path):
    if checksum(path) == FIREPLACE_DIGEST:
        print('Fireplace card definitions already verified')
        return
    pointer = path.read_bytes()
    expected = f'oid sha256:{FIREPLACE_DIGEST}'.encode('ascii')
    lines = pointer.splitlines()
    if not lines or lines[0] != b'version https://git-lfs.github.com/spec/v1' or expected not in lines:
        raise ValueError(f'unexpected Fireplace CardDefs.xml; preserve and inspect it: {path}')
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(dir=path.parent, suffix='.download', delete=False) as target:
            temporary = Path(target.name)
            with urllib.request.urlopen(FIREPLACE_URL, timeout=60) as response:
                shutil.copyfileobj(response, target)
        if checksum(temporary) != FIREPLACE_DIGEST:
            raise ValueError('Fireplace card definitions checksum mismatch')
        temporary.replace(path)
        print('Fireplace card definitions downloaded and verified')
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def main():
    path = PROJECT / 'local/historical-159202.xml'
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists():
        if checksum(path) != DIGEST:
            raise ValueError('existing historical snapshot checksum mismatch; preserve and inspect it')
        print('Historical snapshot already verified')
    else:
        temporary = path.with_suffix('.download')
        with urllib.request.urlopen(URL, timeout=60) as response, temporary.open('wb') as target:
            shutil.copyfileobj(response, target)
        if checksum(temporary) != DIGEST:
            raise ValueError('download checksum mismatch')
        temporary.replace(path)
        print('Historical snapshot downloaded and verified')
    spec = importlib.util.find_spec('fireplace.cards')
    if spec is None or spec.origin is None:
        raise RuntimeError('Install requirements.txt before preparing Fireplace card definitions')
    prepare_fireplace_carddefs(Path(spec.origin).parent / 'CardDefs.xml')


if __name__ == '__main__':
    main()
