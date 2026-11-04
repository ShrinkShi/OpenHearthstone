"""Refresh original RandomWaitAction parameters from exact source FSMs."""
from .export_runtime import RuntimeExport
from .native_effects import refresh_scale_cache


def main():
    refresh_scale_cache(RuntimeExport(), {'RandomWaitAction'}, 'random-wait')


if __name__=='__main__':
    main()
