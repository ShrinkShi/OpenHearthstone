"""导出收藏卡牌和翻页音效，核对本机原始对象身份。"""
from .export_runtime import RuntimeExport


SOURCES = (
    ('SFX_COLLECTION_CARD', 'collection_manager_card_move_invalid_or_click', '3019f3d30ddd2548c85fba68'),
    ('SFX_COLLECTION_PAGE_BACK', 'collection_manager_book_page_flip_back', '7831e6d801feb4e6a6da6932'),
    ('SFX_COLLECTION_PAGE_FORWARD', 'collection_manager_book_page_flip_forward', 'a8de15658e2bcba9970f83be'),
    ('SFX_COLLECTION_PAGE_BACK_2', 'collection_manager_book_page_flip_back_2', 'e390e0d65ba8b7505c6d862f'),
    ('SFX_COLLECTION_PAGE_BACK_3', 'collection_manager_book_page_flip_back_3', 'e403df403f2727377a45cac3'),
    ('SFX_COLLECTION_PAGE_BACK_4', 'collection_manager_book_page_flip_back_4', 'def2f40fcd2909d63e1a6d30'),
    ('SFX_COLLECTION_PAGE_FORWARD_2', 'collection_manager_book_page_flip_forward_2', 'a3643ec46867608bc0a0bf40'),
    ('SFX_COLLECTION_PAGE_FORWARD_3', 'collection_manager_book_page_flip_forward_3', 'a6588c34efea1df5d0d09701'),
    ('SFX_COLLECTION_PAGE_FORWARD_4', 'collection_manager_book_page_flip_forward_4', '7f9849b68b46120b6f7f58cb'),
)
BUNDLE = 'initial_base_cm_global-audio-0.unity3d'
CAB = 'CAB-9b523d1a7164d028996cafbff62b1cfc'


def main():
    exporter = RuntimeExport()
    for logical_id, name, asset_id in SOURCES:
        rows = exporter.imp.store.db.execute(
            'SELECT id,bundle,cab FROM assets WHERE name=? AND type=? ORDER BY bundle',
            (name, 'AudioClip'),
        ).fetchall()
        if not rows or (rows[0]['id'], rows[0]['bundle'], rows[0]['cab']) != (asset_id, BUNDLE, CAB):
            raise RuntimeError(f'收藏音效来源不匹配: {logical_id}')
        exporter.named(logical_id, name, 'AudioClip')
    for logical_id, name, asset_id in (
        ('SFX_COLLECTION_FILTER_OPEN', 'choose_opponent_panel_slide_on', '10326e334839c52a2cb157fb'),
        ('SFX_COLLECTION_FILTER_CLOSE', 'choose_opponent_panel_slide_off', '6cd4fefea4c8449d8c79972d'),
    ):
        row = exporter.imp.store.db.execute('SELECT id,bundle,cab FROM assets WHERE id=?', (asset_id,)).fetchone()
        if row is None or tuple(row) != (asset_id, 'essential_base_global-audio-0.unity3d', 'CAB-c491983cea83897756f11fa097177509'):
            raise RuntimeError(f'筛选音效来源不匹配: {logical_id}')
        exporter.named(logical_id, name, 'AudioClip')
    if exporter.errors:
        raise RuntimeError(f'收藏音效导出失败: {exporter.errors}')
    exporter.save()


if __name__ == '__main__':
    main()
