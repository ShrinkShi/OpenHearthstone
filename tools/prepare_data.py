"""取得并校验固定历史元数据；不下载或修改官方客户端资产。"""
import hashlib
import shutil
import urllib.request
from .importer import PROJECT

COMMIT = '742c0010782909dbfc3ee689d46e71bbb94e3c15'
URL = f'https://raw.githubusercontent.com/HearthSim/hsdata/{COMMIT}/CardDefs.xml'
DIGEST = '96c094b6971b44c8238a7aa05aa2831d809b7ea2d943e50eee8b165e1db1b099'


def checksum(path):
    with path.open('rb') as source:
        return hashlib.file_digest(source, 'sha256').hexdigest()


def main():
    path = PROJECT / 'local/historical-159202.xml'
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists():
        if checksum(path) != DIGEST:
            raise ValueError('existing historical snapshot checksum mismatch; preserve and inspect it')
        print('Historical snapshot already verified')
        return
    temporary = path.with_suffix('.download')
    with urllib.request.urlopen(URL, timeout=60) as response, temporary.open('wb') as target:
        shutil.copyfileobj(response, target)
    if checksum(temporary) != DIGEST:
        raise ValueError('download checksum mismatch')
    temporary.replace(path)
    print('Historical snapshot downloaded and verified')


if __name__ == '__main__':
    main()
