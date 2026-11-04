"""Refresh original ActorElite owner/event parameters, preserving other fields."""
from .export_runtime import RuntimeExport
from .native_effects import refresh_scale_cache


def main():
    refresh_scale_cache(RuntimeExport(), {'ActorEliteEventAction'}, 'actor-elite')


if __name__=='__main__':
    main()
