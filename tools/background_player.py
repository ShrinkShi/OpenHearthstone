"""本项目测试 Player 不激活窗口；不操作桌面或其他应用。"""
import os
import subprocess


def startup(background):
    if background and os.name == 'nt':
        info = subprocess.STARTUPINFO()
        info.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        # 隐藏/最小化会停止 Unity 的 GUI 渲染，使用 SW_SHOWNOACTIVATE。
        info.wShowWindow = 4
        return {'startupinfo': info}
    return {}
