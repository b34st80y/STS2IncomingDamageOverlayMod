"""Read recognition references from the user's local Godot 4.5 game pack."""
import argparse
import re
import struct
from pathlib import Path
from compression import zstd


def font_bytes(resource):
    if resource[:4] != b'RSCC':
        raise ValueError("Unsupported Godot font resource")
    mode, block, total = struct.unpack_from('<III', resource, 4)
    if mode != 2 or not block or total > 32_000_000:
        raise ValueError("Unsupported font compression")
    count = total // block + 1
    start = 16 + count * 4
    raw = bytearray()
    for (size,) in struct.iter_unpack('<I', resource[16:start]):
        raw.extend(zstd.decompress(resource[start:start + size]))
        start += size
    # FontFile embeds the original TrueType byte array, prefixed by its length.
    for match in re.finditer(b'\x00\x01\x00\x00', raw):
        pos = match.start()
        if pos < 4 or pos + 28 > len(raw):
            continue
        length, = struct.unpack_from('<I', raw, pos - 4)
        tables, = struct.unpack_from('>H', raw, pos + 4)
        if 5 <= tables <= 64 and 12 + 16 * tables < length <= len(raw) - pos:
            tags = {bytes(raw[pos+i:pos+i+4]) for i in range(12, 12+16*tables, 16)}
            if {b'glyf', b'head', b'cmap'} <= tags:
                return raw[pos:pos+length]
    raise ValueError("No embedded TrueType font")


def prepare(pack, output):
    wanted = {f'intent_attack_{n}.png' for n in range(1, 6)}
    wanted.update({'intent_death_blow.png', 'block.png', 'kreon_bold.ttf'})
    with pack.open('rb') as f:
        if f.read(8) != b'GDPC\x03\x00\x00\x00':
            raise ValueError("Expected a Godot v3 pack directory")
        f.seek(24)
        base, directory = struct.unpack('<QQ', f.read(16))
        f.seek(directory)
        count, = struct.unpack('<I', f.read(4))
        assets = {}
        for _ in range(count):
            length, = struct.unpack('<I', f.read(4))
            path = f.read(length).rstrip(b'\0').decode()
            offset, size = struct.unpack('<QQ', f.read(16))
            f.read(20)
            if path.startswith('.godot/imported/'):
                name = Path(path).name.split('-')[0]
                if name in wanted:
                    assets[name] = (base + offset, size)
        if missing := wanted - assets.keys():
            raise ValueError(f"Missing recognition references: {sorted(missing)}")
        for name, (offset, size) in assets.items():
            f.seek(offset)
            resource = f.read(size)
            if name.endswith('.ttf'):
                (output / name).write_bytes(font_bytes(resource))
            else:
                start = resource.index(b'RIFF')
                length, = struct.unpack_from('<I', resource, start + 4)
                if resource[start+8:start+12] != b'WEBP':
                    raise ValueError(f"Unsupported texture encoding: {name}")
                (output / Path(name).with_suffix('.webp')).write_bytes(resource[start:start+8+length])


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('pack', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    prepare(args.pack, args.output)
