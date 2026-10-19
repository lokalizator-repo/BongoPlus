import argparse
import hashlib
import json
import struct
from pathlib import Path


class Reader:
    def __init__(self, data, offset=0):
        self.data = data
        self.offset = offset

    def read(self, fmt):
        result = struct.unpack_from('<' + fmt, self.data, self.offset)
        self.offset += struct.calcsize('<' + fmt)
        return result[0] if len(result) == 1 else result

    def align(self):
        self.offset = (self.offset + 3) & ~3

    def string(self):
        size = self.read('i')
        if size < 0 or self.offset + size > len(self.data):
            raise ValueError(f'Invalid string at {self.offset - 4}')
        value = self.data[self.offset:self.offset + size].decode('utf-8')
        self.offset += size
        self.align()
        return value

    def boolean(self):
        value = self.read('B')
        if value not in (0, 1):
            raise ValueError(f'Invalid Boolean at {self.offset - 1}')
        self.align()
        return bool(value)

    def pointer(self):
        return self.read('iq')


class AssetFile:
    def __init__(self, path):
        self.path = path
        self.data = path.read_bytes()
        version = struct.unpack_from('>I', self.data, 8)[0]
        if version != 22 or self.data[16] != 0:
            raise ValueError(f'Unsupported asset format: {path}, version={version}')
        data_start = struct.unpack_from('>Q', self.data, 32)[0]
        reader = Reader(self.data, self.data.index(0, 48) + 1)
        reader.read('i')
        if reader.read('B'):
            raise ValueError('Embedded type trees are not supported by this focused parser')
        types = []
        for _ in range(reader.read('i')):
            class_id = reader.read('i')
            reader.offset += 3 + (16 if class_id == 114 else 0) + 16
            types.append(class_id)
        self.objects = {}
        for _ in range(reader.read('i')):
            reader.align()
            object_id, offset, size, type_id = reader.read('qqIi')
            self.objects[object_id] = (types[type_id], data_start + offset, size)

    def reader(self, object_id):
        _, start, size = self.objects[object_id]
        return Reader(self.data[start:start + size])


def mono_script_names(asset):
    result = {}
    for object_id, (class_id, _, _) in asset.objects.items():
        if class_id == 115:
            result[object_id] = asset.reader(object_id).string()
    return result


def object_header(reader):
    reader.offset = 16
    script = reader.pointer()
    name = reader.string()
    return script, name


def parse_basic(reader, asset_name):
    item = {'asset_name': asset_name, 'id': reader.read('i'), 'name': reader.string(),
            'description': reader.string(), 'name_color': reader.string()}
    item['tags'] = [reader.string() for _ in range(reader.read('i'))]
    for field in ['tradable', 'marketable', 'hidden', 'dropable']:
        item[field] = reader.boolean()
    item['bundle_frequency'] = reader.read('i')
    item['game_only'] = reader.boolean()
    item['promo'] = reader.string()
    item['drop_start_time'] = reader.string()
    for field in ['is_other_token', 'hide_until_received', 'is_active_promo']:
        item[field] = reader.boolean()
    item['price_category'] = reader.string()
    item['feature_in_item_store'] = reader.boolean()
    item['purchase_limit'] = reader.read('i')
    item['tap_tap_loot_buffs'] = [
        {'name': reader.string(), 'value': reader.read('f')}
        for _ in range(reader.read('i'))
    ]
    if reader.offset != len(reader.data):
        raise ValueError(f'Unexpected trailing item data: {asset_name}, {len(reader.data) - reader.offset}')
    item['slot'] = next((tag.split(':')[1] for tag in item['tags'] if tag.startswith('itemslot:')), '')
    return item


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('data_directory', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    files = {name: AssetFile(args.data_directory / name) for name in [
        'globalgamemanagers.assets', 'resources.assets', 'sharedassets0.assets', 'level0']}
    scripts = mono_script_names(files['globalgamemanagers.assets'])
    items = []
    references = []
    cache_pointers = []
    for file_name, asset in files.items():
        for object_id, (class_id, _, _) in asset.objects.items():
            if class_id != 114:
                continue
            reader = asset.reader(object_id)
            script, name = object_header(reader)
            script_name = scripts.get(script[1]) if script[0] == 1 else None
            if script_name == 'SteamItemBasic':
                item = parse_basic(reader, name)
                item.update(file=file_name, object_id=object_id)
                items.append(item)
            elif script_name == 'SteamItemIdReference':
                ids = [reader.read('i') for _ in range(reader.read('i'))]
                pointers = [reader.pointer() for _ in range(reader.read('i'))]
                references.append({'file': file_name, 'object_id': object_id, 'ids': ids, 'item_pointers': pointers})
            elif script_name == 'MemoryImageCache':
                cache_pointers.extend(reader.pointer() for _ in range(reader.read('i')))
    sprite_names = set()
    shared = files['sharedassets0.assets']
    for file_index, object_id in cache_pointers:
        if file_index != 3:
            raise ValueError(f'Unexpected cache pointer file index: {file_index}')
        class_id, _, _ = shared.objects[object_id]
        if class_id != 213:
            raise ValueError(f'Unexpected cached sprite class: {class_id}')
        sprite_names.add(shared.reader(object_id).string())
    reference_ids = set(value for reference in references for value in reference['ids'])
    referenced_objects = set()
    for reference in references:
        for file_index, object_id in reference['item_pointers']:
            if file_index != 0:
                raise ValueError(f'Unexpected item reference file index: {file_index}')
            referenced_objects.add((reference['file'], object_id))
    if referenced_objects != {(item['file'], item['object_id']) for item in items}:
        raise ValueError('Parsed item assets and AllItemData references differ')
    if len({item['id'] for item in items}) != len(items):
        raise ValueError('Duplicate item IDs')
    for item in items:
        item['in_reference_ids'] = item['id'] in reference_ids
        item['in_all_item_data'] = (item['file'], item['object_id']) in referenced_objects
        item['icon_in_cache'] = item['name'].replace(' ', '') + 'Icon' in sprite_names
        item['native_inventory_included'] = item['in_all_item_data'] and not item['hidden'] and not item['game_only'] and bool(item['slot'])
        item['mod_included'] = item['native_inventory_included'] and item['icon_in_cache']
        if item['slot'] in ('skin', 'bongos'):
            item['missing_animation_sprites'] = [
                suffix for suffix in ['Left', 'LeftPunch', 'Right', 'RightPunch']
                if item['name'].replace(' ', '') + suffix not in sprite_names
            ]
    skins = [item for item in items if item['slot'] == 'skin']
    summary = {
        'skin_assets': len(skins),
        'skins_in_native_inventory': sum(item['native_inventory_included'] for item in skins),
        'skins_in_mod': sum(item['mod_included'] for item in skins),
        'priced_skins': sum(bool(item['price_category']) for item in skins),
        'hide_until_received_skins': sum(item['hide_until_received'] for item in skins),
        'skins_with_missing_animation_sprites': sum(bool(item['missing_animation_sprites']) for item in skins),
        'excluded_skins': [item['id'] for item in skins if not item['mod_included']],
    }
    report = {'sources': [{
        'file': name, 'size': len(asset.data), 'sha256': hashlib.sha256(asset.data).hexdigest()
    } for name, asset in files.items()], 'summary': summary, 'references': references,
              'cached_sprite_count': len(sprite_names), 'items': items}
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'parsed_items': len(items), 'references': [(entry['file'], len(entry['ids']), len(entry['item_pointers'])) for entry in references],
                      'cached_sprites': len(sprite_names), 'skins': len([item for item in items if item['slot'] == 'skin'])}))


if __name__ == '__main__':
    main()
