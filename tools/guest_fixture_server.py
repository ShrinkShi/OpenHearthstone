"""显式的本机访客回放服务；正常游戏使用带账号的game.server。"""
import argparse
import asyncio
import ipaddress

from game.server import Server


async def serve(host, port, logdir):
    if not ipaddress.ip_address(host).is_loopback:
        raise ValueError('Guest fixture server must bind a loopback address')
    server = Server(logdir)
    listener = await asyncio.start_server(server.handle, host, port, limit=32768)
    print(f'Guest fixture server listening on {host}:{port}', flush=True)
    async with listener:
        await listener.serve_forever()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--host', default='127.0.0.1')
    parser.add_argument('--port', type=int, default=37240)
    parser.add_argument('--logdir', default='work/guest-fixture-matches')
    args = parser.parse_args()
    asyncio.run(serve(args.host, args.port, args.logdir))


if __name__ == '__main__':
    main()
