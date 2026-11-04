"""复用本机权威服务器并启动两个独立的 Windows 客户端。"""
import argparse
import json
import socket
import subprocess
import sys
import time
from pathlib import Path
from .background_player import startup

ROOT = Path(__file__).resolve().parents[1]
ACCOUNT_CAPABILITY = 'accounts_v1'
FOUR_SLOT_QUESTS_CAPABILITY = 'account_quests_four_slots_v1'
SOCIAL_CAPABILITY = 'account_social_v1'
DECK_CODE_CAPABILITY = 'account_deck_codes_v1'
DECK_FORMAT_CAPABILITY = 'account_deck_format_metadata_v1'


def server_info(port):
    try:
        connection = socket.create_connection(('127.0.0.1', port), timeout=.5)
    except OSError as exc:
        # 无监听时 Windows 也可能返回超时，以独占 bind 确认端口是否空闲。
        try:
            with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as probe:
                if hasattr(socket, 'SO_EXCLUSIVEADDRUSE'):
                    probe.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
                probe.bind(('0.0.0.0', port))
            return None
        except OSError:
            raise RuntimeError(f'端口 {port} 已占用，但无法连接本项目服务器') from exc
    try:
        with connection:
            connection.settimeout(.5)
            data = connection.makefile('rb').readline(4096)
            hello = json.loads(data)
            if hello.get('type') != 'hello' or hello.get('protocol') != 1:
                raise ValueError('unrecognized server handshake')
            return hello
    except (TimeoutError, OSError, ValueError, AttributeError) as exc:
        raise RuntimeError(f'端口 {port} 已占用，无法确认是本项目的服务器') from exc


def server_ready(port):
    return server_info(port) is not None


def supports_account_quests(hello):
    if not isinstance(hello, dict):
        return False
    capabilities = hello.get('capabilities')
    return (isinstance(capabilities, list)
            and ACCOUNT_CAPABILITY in capabilities
            and FOUR_SLOT_QUESTS_CAPABILITY in capabilities)


def supports_account_social(hello):
    return (supports_account_quests(hello)
            and SOCIAL_CAPABILITY in hello.get('capabilities', []))


def supports_deck_codes(hello):
    return (supports_account_social(hello)
            and DECK_CODE_CAPABILITY in hello.get('capabilities', [])
            and DECK_FORMAT_CAPABILITY in hello.get('capabilities', []))


def server_matches_mode(hello, autoplay):
    if not isinstance(hello, dict):
        return False
    capabilities = hello.get('capabilities')
    if not isinstance(capabilities, list):
        return False
    if autoplay:
        return ACCOUNT_CAPABILITY not in capabilities
    return supports_deck_codes(hello)


def choose_account_server(start_port):
    for port in range(start_port, min(65535, start_port + 100) + 1):
        hello = server_info(port)
        if supports_deck_codes(hello):
            return port, False
        if hello is None:
            return port, True
    raise RuntimeError(f'从端口 {start_port} 起未找到可用的账号服务端口')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--port', type=int, default=37240)
    parser.add_argument('--class-a', type=int, default=None)
    parser.add_argument('--class-b', type=int, default=None)
    parser.add_argument('--profile-prefix', default='player')
    parser.add_argument('--autoplay', action='store_true')
    parser.add_argument('--background', action='store_true', help='后台启动测试窗口，不激活窗口')
    args = parser.parse_args()
    executable = ROOT / 'build/client/HearthstoneClone.exe'
    if not executable.exists() or not (ROOT / 'local/runtime/registry.json').exists():
        raise RuntimeError('请先完成 Unity 构建与本机资源提取')
    work = ROOT / 'work'
    work.mkdir(exist_ok=True)
    if args.autoplay:
        hello = server_info(args.port)
        if hello and ACCOUNT_CAPABILITY in hello.get('capabilities', []):
            raise RuntimeError('--autoplay 不能使用账号服务；请连接显式访客测试服务')
        selected_port, start_server = args.port, hello is None
    else:
        selected_port, start_server = choose_account_server(args.port)
    report = {'port': selected_port, 'clients': [], 'guest_diagnostic': bool(args.autoplay)}
    if start_server:
        logname = f'server-{int(time.time())}.log'
        logdir = work / f'guest-{int(time.time())}' if args.autoplay else None
        server_command = ([sys.executable, '-X', 'utf8', '-m', 'tools.guest_fixture_server',
                '--host', '127.0.0.1', '--port', str(selected_port), '--logdir', str(logdir)]
            if args.autoplay else [sys.executable, '-X', 'utf8', '-m', 'game.server',
                '--host', '127.0.0.1', '--port', str(selected_port)])
        with (work / logname).open('ab') as output:
            server = subprocess.Popen(server_command, cwd=ROOT,
                stdout=output, stderr=subprocess.STDOUT,
                creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
        report.update(server_pid=server.pid, server_log=logname, server_logdir=None if logdir is None else str(logdir))
        for _ in range(100):
            if server.poll() is not None:
                raise RuntimeError(f'服务器启动失败，详情见 {work / logname}')
            try:
                hello = server_info(selected_port)
                if server_matches_mode(hello, bool(args.autoplay)):
                    break
            except RuntimeError:
                # 启动中的本项目进程可能刚绑定端口，尚未接受首次连接。
                pass
            time.sleep(.1)
        else:
            raise RuntimeError(f'服务器启动超时，详情见 {work / logname}')
    for index, cls in enumerate((args.class_a, args.class_b), 1):
        profile = f'{args.profile_prefix}{index}'
        command = [str(executable), '--profile', profile, '--name', f'Shrink{index}',
                   '--host', '127.0.0.1', '--port', str(selected_port), '-logFile', str(work / f'{profile}.log')]
        if cls is not None:
            command.extend(['--class', str(cls)])
        if args.autoplay:
            command.extend(['--auto-match', '--autoplay'])
            if args.background:
                command.append('-batchmode')
            else:
                command.extend(['--screenshot-dir', str(ROOT / 'local/visual/launcher-match')])
        client = subprocess.Popen(command, cwd=ROOT, **startup(args.background))
        report['clients'].append({'name': f'Shrink{index}', 'profile': profile, 'pid': client.pid})
    (work / 'launcher-report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False))


if __name__ == '__main__':
    main()
