"""Read existing round11 proxy/output/stage files. Never load an engine or RAW."""
import argparse
import hashlib
import json
from pathlib import Path
import sys

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent / 'diagnostic'))
from package_validation import FILM, PRINT, need, read, safe
from compare_rows import compare

STAGES = FILM + PRINT


def sha(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def validate_stage_manifest(report, photos):
    need(report.get('version') == 'NaturalPassiveStageFiniteV1', 'Stage report version')
    cases = report.get('cases', [])
    expected_ids = [photo['id'] for photo in photos]
    need(len(set(expected_ids)) == len(expected_ids), 'Duplicate expected photo')
    need([case['id'] for case in cases] == expected_ids, 'Stage photo identity/order')
    for case, photo in zip(cases, photos):
        need(case.get('tracedOutputExact') is True, 'Recorded trace final mismatch')
        stages = case.get('stages', [])
        need([stage['stage'] for stage in stages] == STAGES, 'Stage order/count')
        for stage in stages:
            need(stage['shape'] == photo['shape'][:2] + [3], 'Stage photo shape')
            need(stage.get('nonfiniteSamples') == 0, 'Recorded nonfinite stage')
            need(np.isfinite([stage['min'], stage['max']]).all(), 'Nonfinite stage range')
            need(stage['min'] <= stage['max'], 'Invalid stage range')
    return cases


def validate_stage_file(path, record):
    shape = record['shape']
    need(len(shape) == 3 and all(type(n) is int and n > 0 for n in shape), 'Stage shape')
    need(Path(path).stat().st_size == int(np.prod(shape)) * 4, 'Stage length')
    need(sha(path) == record['sha256'], 'Stage bytes changed')
    data = np.fromfile(path, dtype='<f4')
    need(np.isfinite(data).all(), 'Nonfinite saved stage')
    actual = {
        'nonfiniteSamples': 0, 'min': float(data.min()), 'max': float(data.max()),
        'negativeSamples': int((data < 0).sum()),
        'aboveOneSamples': int((data > 1).sum()), 'zeroSamples': int((data == 0).sum()),
    }
    need(all(record[key] == value for key, value in actual.items()), 'Stage statistics changed')
    return {'stage': record['stage'], 'sha256': record['sha256'], 'dataChecksRecomputed': True}


def stage_filename(photo_index, stage_index, photo_id, record):
    # The passive native tap counter is process-global across the six photos.
    sequence = photo_index * len(STAGES) + stage_index
    h, w, channels = record['shape']
    return f"{photo_id}/{sequence:04d}-{record['stage']}-{h}-{w}-{channels}.f32"


def validate_saved_outputs(evidence, inputs, outputs, stages):
    evidence, inputs, outputs, stages = map(Path, (evidence, inputs, outputs, stages))
    metrics = read(evidence / 'natural-coverage-metrics.json')
    manifest = read(inputs / 'manifest.json')
    finite = read(evidence / 'natural-stage-finite.json')
    photos = metrics['photos']
    need(len(photos) == 6, 'Six-photo bound')
    need(sha(inputs / 'manifest.json') == metrics['admissionManifestSha256'], 'Proxy manifest changed')
    need(manifest == read(evidence / 'natural-input-manifest.json'), 'Recorded proxy manifest mismatch')
    need(read(outputs / 'report.json') == metrics, 'Saved output report mismatch')
    need(read(stages / 'report.json') == finite, 'Saved stage report mismatch')
    need([entry['id'] for entry in manifest['sources']] == [photo['id'] for photo in photos], 'Proxy photo identity/order')
    cases = validate_stage_manifest(finite, photos)
    result = {'version': 'Round11SavedOutputAuditV1', 'renderCalls': 0, 'rawDecodeCalls': 0,
              'scope': 'existing saved data only; not a performance or preference test', 'photos': []}
    for photo_index, (photo, entry, case) in enumerate(zip(photos, manifest['sources'], cases)):
        photo_id, shape = photo['id'], photo['shape']
        need(len(shape) == 3 and all(type(n) is int and n > 0 for n in shape) and shape[2] == 4 and max(shape[:2]) <= 1200, 'Proxy bound/shape')
        need(entry['sha256'] == photo['sourceSha256'], 'Source identity mismatch')
        proxy = safe(inputs, entry['file'])
        need(sha(proxy) == entry['fileSha256'], 'Proxy file changed')
        image = np.load(proxy, allow_pickle=False)
        need(list(image.shape) == shape[:2] + [3] == entry['proxyShape'] and image.dtype == np.dtype('<f4') and np.isfinite(image).all(), 'Proxy domain/shape/finite')
        need(hashlib.sha256(image.tobytes()).hexdigest() == entry['pixelSha256'] == photo['inputPixelSha256'], 'Proxy pixels changed')
        whole = safe(outputs, photo_id + '/whole.rgba16')
        need(sha(whole) == photo['wholeInfo']['sha256'], 'Whole output changed')
        need(whole.stat().st_size == int(np.prod(shape)) * 2, 'Whole length')
        rgb = np.fromfile(whole, dtype='<u2').reshape(shape)[:, :, :3]
        for key, value in [('outputZeroFraction', 0), ('outputMaxFraction', 65535)]:
            need((rgb == value).mean(axis=(0, 1)).tolist() == photo['numeric'][key], 'Output endpoint statistics changed')
        checks = []
        for name in ['stream-r17.rgba16', 'stream-r37.rgba16', 'traced.rgba16']:
            root = stages if name == 'traced.rgba16' else outputs
            difference = compare(whole, safe(root, photo_id + '/' + name), shape, '<u2')
            need(difference['changedChannels'] == 0, 'Saved whole/stream/trace data mismatch')
            checks.append({'artifact': name, 'comparison': difference})
        stage_checks = []
        for index, record in enumerate(case['stages']):
            filename = stage_filename(photo_index, index, photo_id, record)
            stage_checks.append(validate_stage_file(safe(stages, filename), record))
        result['photos'].append({'id': photo_id, 'proxyPixelSha256': entry['pixelSha256'],
                                 'wholeSha256': photo['wholeInfo']['sha256'],
                                 'comparisons': checks, 'stages': stage_checks})
    result['allPassed'] = True
    result['stageFilesRecomputed'] = sum(len(photo['stages']) for photo in result['photos'])
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['evidence', 'inputs', 'outputs', 'stages', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    need(not args.output.exists(), 'New audit output required')
    report = validate_saved_outputs(args.evidence, args.inputs, args.outputs, args.stages)
    with args.output.open('x', encoding='utf-8') as stream:
        json.dump(report, stream, indent=2, allow_nan=False)
    print(json.dumps({'allPassed': True, 'photos': len(report['photos']),
                      'stageFilesRecomputed': report['stageFilesRecomputed'], 'renderCalls': 0, 'rawDecodeCalls': 0}))


if __name__ == '__main__':
    main()
