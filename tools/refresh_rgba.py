"""Refresh original RGBA parameters/None flags using the existing source parser."""
from .export_runtime import RuntimeExport
from .native_effects import refresh_color_cache


if __name__ == '__main__':
    refresh_color_cache(RuntimeExport(), {'SetColorRGBA'}, label='rgba')
