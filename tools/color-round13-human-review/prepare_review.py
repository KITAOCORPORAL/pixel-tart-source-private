"""Read-only, local-only viewer for already frozen round-13 development images.

No engine imports, subprocesses, RAW decoding, pixel candidate or tuning paths.
"""
import argparse
import ast
import base64
import hashlib
import io
import json
from pathlib import Path
import sys

REPO = Path(__file__).resolve().parents[2]
HOME = Path('N:/pixart/ColorResearchHome-20261030')
BASE_SHA = '9b8c52715b45540ffaf1d1fc99e0a7bd832678fc'
PINS = {
    'docs/research/color-engine-20261029/home-round13/candidate-natural.json': '5f86ccb71bde714f4537ea05a9766e17517fc7fd99c88540c09011d655a0ca53',
    'docs/research/color-engine-20261029/home-round13/ab-review.json': '4bc4cdf43984569a2f138311b0495c65f3b13bcd10c3d624f386c7245649ae24',
    'docs/research/color-engine-20261029/home-round11/natural-stage-finite.json': 'eb461a9e333feff28b7907d5490943cf83b6b39e77f2731bbed779f43d952f48',
    'tools/color-round13/review_ab.py': '3642df6c0b04f6f98557cdb503e7b4f11404326c95c371f960ad9cc582587392',
}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def load_frozen(repo=REPO):
    data = {}
    for name, expected in PINS.items():
        raw = (repo / name).read_bytes()
        if digest(raw) != expected:
            raise ValueError('Frozen metadata/tool drift: ' + name)
        data[Path(name).name] = raw
    natural = json.loads(data['candidate-natural.json'])
    review = json.loads(data['ab-review.json'])
    finite = json.loads(data['natural-stage-finite.json'])
    # Read literals, never execute the old script (it computes and writes files).
    mapping = None
    for node in ast.walk(ast.parse(data['review_ab.py'])):
        if isinstance(node, ast.Assign) and any(isinstance(t, ast.Name) and t.id == 'mapping' for t in node.targets):
            mapping = ast.literal_eval(node.value)
    if mapping is None:
        raise ValueError('Missing frozen anonymous mapping')
    cases = []
    for rec, rev, stagecase in zip(natural['records'], review['natural'], finite['cases'], strict=True):
        name = rec['id']
        if name != rev['id'] or name != stagecase['id']:
            raise ValueError('Frozen scene order drift')
        i = finite['cases'].index(stagecase)
        si, stage = next((j, s) for j, s in enumerate(stagecase['stages']) if s['stage'] == 'print_linear')
        h, w, channels = rec['shape']
        if stage['sha256'] != rec['inputSha256'] or stage['shape'] != rec['shape'] or channels != 3:
            raise ValueError('Frozen input binding drift')
        sequence = i * len(stagecase['stages']) + si
        case = {'id': name, 'shape': rec['shape'], 'input': f'{name}/{sequence:04d}-print_linear-{h}-{w}-3.f32',
                'inputSha256': rec['inputSha256'], 'outputs': rec['outputHashes'],
                'crop': rev['fixedCrop'], 'cropSha256': rev['anonymousImageHash'], 'rois': [rev['fixedCrop']]}
        if rec.get('fixedRoi') and rec['fixedRoi']['xywh'] not in case['rois']:
            case['rois'].append(rec['fixedRoi']['xywh'])
        cases.append(case)
    if len(cases) != 6 or set(mapping) != {c['id'] for c in cases}:
        raise ValueError('Only the frozen six development images are supported')
    return cases, mapping


def required_files(root, stages, cases):
    rows = [{'path': str(root / 'review-v1/anonymous-key.json'), 'role': 'anonymous-key', 'expectedSha256': None}]
    for c in cases:
        name = c['id']
        count = c['shape'][0] * c['shape'][1]
        rows.append({'path': str(stages / c['input']), 'role': 'input-f32', 'expectedSha256': c['inputSha256'], 'bytes': count * 12})
        for filename, sha in c['outputs'].items():
            rows.append({'path': str(root / 'candidate-natural' / (name + '-whole') / filename), 'role': 'output',
                         'expectedSha256': sha, 'bytes': count * (8 if filename.endswith('.rgba16') else 12)})
        for label in ['A', 'B']:
            rows.append({'path': str(root / 'candidate-natural' / f'{name}-{label}.png'), 'role': 'display-png', 'expectedSha256': None})
        rows.append({'path': str(root / 'review-v1' / f'{name}-crop-anonymous.png'), 'role': 'anonymous-crop', 'expectedSha256': c['cropSha256']})
    return rows


def inspect_files(root, stages, cases):
    rows = required_files(root, stages, cases)
    for r in rows:
        r['exists'] = Path(r['path']).is_file()
    return {'version': 'Round13HumanReviewAvailabilityV1', 'readOnly': True, 'root': str(root), 'stages': str(stages),
            'requiredFiles': rows, 'requiredCount': len(rows), 'missingCount': sum(not r['exists'] for r in rows),
            'humanPreference': 'NOT_RUN_AWAITING_HUMAN', 'independentValidation': 'NOT_RUN_DEVELOPMENT_ONLY',
            'product': 'DEFAULT_OFF_NOT_ADMITTED'}


def validate(root, stages, cases, expected_mapping):
    # Import only offline file readers. No engine/toolchain dependencies.
    import numpy as np
    from PIL import Image
    report = inspect_files(root, stages, cases)
    if report['missingCount']:
        missing = [r['path'] for r in report['requiredFiles'] if not r['exists']]
        raise ValueError('Missing frozen files (no replacement or rerender):\n' + '\n'.join(missing))
    blobs = {}
    for r in report['requiredFiles']:
        raw = Path(r['path']).read_bytes()
        r['actualSha256'] = digest(raw)
        r['actualBytes'] = len(raw)
        if r['expectedSha256'] and r['actualSha256'] != r['expectedSha256']:
            raise ValueError('Frozen hash mismatch: ' + r['path'])
        if 'bytes' in r and len(raw) != r['bytes']:
            raise ValueError('Frozen size mismatch: ' + r['path'])
        if r['role'] == 'input-f32' or (r['role'] == 'output' and r['path'].endswith('.f32')):
            if not np.isfinite(np.frombuffer(raw, dtype='<f4')).all():
                raise ValueError('Nonfinite frozen array: ' + r['path'])
        blobs[r['path']] = raw
    mapping = json.loads(blobs[str(root / 'review-v1/anonymous-key.json')])
    if mapping != expected_mapping:
        raise ValueError('Anonymous key differs from frozen mapping')
    public_cases = []
    for c in cases:
        name = c['id']
        h, w, _ = c['shape']
        images = {}
        for label, file in [('A', 'legacy.rgba16'), ('B', 'candidate.rgba16')]:
            raw = blobs[str(root / 'candidate-natural' / f'{name}-{label}.png')]
            with Image.open(io.BytesIO(raw)) as im:
                if im.format != 'PNG' or im.mode != 'RGB' or im.size != (w, h):
                    raise ValueError('PNG format/mode/size mismatch: ' + name)
                # Original PNGs are untagged encoded sRGB. Reject alternate per-image transformations.
                if any(k in im.info for k in ['icc_profile', 'gamma', 'srgb', 'chromaticity', 'exif', 'transparency']):
                    raise ValueError('Unexpected PNG color/orientation metadata: ' + name)
                pixels = np.array(im)
                q = np.frombuffer(blobs[str(root / 'candidate-natural' / (name + '-whole') / file)], dtype='<u2').reshape(h, w, 4)
                expected = np.rint(q[:, :, :3] / 257).astype(np.uint8)
                if not np.array_equal(pixels, expected):
                    raise ValueError('PNG does not match frozen RGBA16 display conversion: ' + name)
                images[label] = (raw, pixels)
        x, y, rw, rh = c['crop']
        with Image.open(io.BytesIO(blobs[str(root / 'review-v1' / f'{name}-crop-anonymous.png')])) as crop:
            for key, offset in zip(mapping[name], [0, 310], strict=True):
                if not np.array_equal(np.array(crop.crop((offset, 25, offset + rw, 25 + rh))), images[key][1][y:y+rh, x:x+rw]):
                    raise ValueError('Anonymous crop pixel/key mismatch: ' + name)
        public_cases.append({'id': name, 'width': w, 'height': h, 'rois': c['rois'],
                             'images': {label: 'data:image/png;base64,' + base64.b64encode(images[key][0]).decode('ascii')
                                        for label, key in zip(['X', 'Y'], mapping[name], strict=True)}})
    report['validation'] = 'PASS_HASH_SIZE_FINITE_PNG_PIXELS_MAPPING'
    report['display'] = 'Original untagged PNG bytes; both exact same round-to-even RGBA16/257 to RGB8 conversion; no new tone/color/resize encoding'
    report['fullPngHistoricalHash'] = 'NOT_RECORDED; validated against frozen raw RGBA16 per pixel; actual PNG SHA recorded now'
    return report, public_cases


def write_bundle(output, report, public_cases, template=None):
    output = output.resolve()
    if REPO.resolve() == output or REPO.resolve() in output.parents:
        raise ValueError('Private review output must stay outside the Git checkout')
    for name in ['root', 'stages']:
        protected = Path(report[name]).resolve()
        if output == protected or protected in output.parents:
            raise ValueError('Output overlaps a frozen input directory')
    # Protect all explicitly read inputs, even if callers supply nonstandard roots.
    for r in report['requiredFiles']:
        source = Path(r['path']).resolve()
        if source == output or output in source.parents or source in output.parents:
            raise ValueError('Output overlaps a frozen input')
    if output.exists():
        raise ValueError('Refusing existing output directory')
    if template is None:
        template = Path(__file__).with_name('viewer.html').read_text(encoding='utf-8')
    payload = {'version': 'Round13AnonymousHumanReviewV1', 'sourceBase': BASE_SHA, 'cases': public_cases}
    encoded = json.dumps(payload, ensure_ascii=True).replace('<', '\\u003c')
    page = template.replace('/*__FROZEN_DATA__*/null', encoded)
    if page == template:
        raise ValueError('Missing viewer data slot')
    output.mkdir(parents=True, exist_ok=False)
    (output / 'review.html').write_text(page, encoding='utf-8')
    (output / 'validation-local-only.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return output / 'review.html'


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--root', type=Path, default=HOME / 'round13-unsharp-v1')
    p.add_argument('--stages', type=Path, default=HOME / 'round11/natural-stage-finite-v1')
    p.add_argument('--output', type=Path)
    p.add_argument('--inventory-only', action='store_true', help='Only inspect explicit expected paths; no pixel reads/writes')
    a = p.parse_args(argv)
    a.root, a.stages = a.root.resolve(), a.stages.resolve()
    cases, mapping = load_frozen()
    if a.inventory_only:
        report = inspect_files(a.root, a.stages, cases)
        print(json.dumps(report, ensure_ascii=False, indent=2))
        return 2 if report['missingCount'] else 0
    if not a.output:
        p.error('--output requires a new private directory outside the Git checkout')
    # Fail early for missing data, before imports/any output creation.
    availability = inspect_files(a.root, a.stages, cases)
    if availability['missingCount']:
        print(json.dumps(availability, ensure_ascii=False, indent=2))
        return 2
    report, public_cases = validate(a.root, a.stages, cases, mapping)
    page = write_bundle(a.output, report, public_cases)
    print('PASS: open local file ' + str(page))
    print('Human preference: NOT_RUN_AWAITING_HUMAN. No engine/RAW/GPU/build calls. Keep all output private.')
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (OSError, ValueError, ImportError) as e:
        print('REFUSED: ' + str(e), file=sys.stderr)
        sys.exit(2)
