"""本机导出器共用的写锁与原子 JSON，避免双客户端丢失缓存映射。"""
import json
import msvcrt
import os
import time
from contextlib import contextmanager


@contextmanager
def asset_lock(local, timeout=60):
    local.mkdir(parents=True, exist_ok=True)
    with (local / 'asset-write.lock').open('a+b') as lock:
        if lock.seek(0, os.SEEK_END) == 0:
            lock.write(b'0')
            lock.flush()
        deadline = time.monotonic()+timeout
        while True:
            try:
                lock.seek(0)
                msvcrt.locking(lock.fileno(), msvcrt.LK_NBLCK, 1)
                break
            except OSError:
                if time.monotonic() >= deadline:
                    raise TimeoutError('asset write lock timeout')
                time.sleep(.1)
        try:
            yield
        finally:
            lock.seek(0)
            msvcrt.locking(lock.fileno(), msvcrt.LK_UNLCK, 1)


def atomic_json(path, value):
    temporary = path.with_name(path.name+f'.{os.getpid()}.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')
    for attempt in range(30):
        try:
            temporary.replace(path)
            return
        except PermissionError:
            # Unity 正在读取旧文件时，Windows 文件共享可能短暂拒绝 replace。
            if attempt == 29:
                raise
            time.sleep(.1)


def merge_effects(local, entries):
    """各按需导出器只更新自己的卡牌，不能抹掉其他效果。"""
    with asset_lock(local):
        path = local / 'runtime/effect-index.json'
        existing = json.loads(path.read_text(encoding='utf-8'))['entries'] if path.exists() else []
        merged = {e['card_id']: e for e in existing}
        merged.update({e['card_id']: e for e in entries})
        atomic_json(path, dict(entries=list(merged.values())))
