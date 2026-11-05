import hashlib
import json
import subprocess
import sys


def test_rechunk_preserves_bytes_and_hashes_across_source_boundaries(tmp_path):
    source = tmp_path / 'source'
    output = tmp_path / 'output'
    source.mkdir()
    content = bytes(range(256)) * 10000
    (source / 'openh-full-assets.7z.001').write_bytes(content[:800000])
    (source / 'openh-full-assets.7z.002').write_bytes(content[800000:1800000])
    (source / 'openh-full-assets.7z.003').write_bytes(content[1800000:])
    subprocess.run([sys.executable, '-m', 'tools.rechunk_release', str(source), str(output),
                    '--chunk-mib', '1'], check=True, capture_output=True)
    entries = json.loads((output / 'full-chunks.json').read_text())
    assert b''.join((output / item['name']).read_bytes() for item in entries) == content
    assert all(hashlib.sha256((output / item['name']).read_bytes()).hexdigest() == item['sha256']
               for item in entries)
