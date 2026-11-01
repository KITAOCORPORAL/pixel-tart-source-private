"""No-render regression checks for saved-data evidence validation."""
import copy
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

import numpy as np
from verify_saved_outputs import sha, stage_filename, validate_stage_file, validate_stage_manifest

EVIDENCE = Path(__file__).resolve().parents[2] / 'docs/research/color-engine-20261029/home-round11'


class SavedStageManifestTests(unittest.TestCase):
    def setUp(self):
        self.report = json.loads((EVIDENCE / 'natural-stage-finite.json').read_text())
        self.photos = json.loads((EVIDENCE / 'natural-coverage-metrics.json').read_text())['photos']

    def reject(self, message):
        with self.assertRaisesRegex(ValueError, message):
            validate_stage_manifest(self.report, self.photos)

    def test_valid_72_stage_manifest(self):
        cases = validate_stage_manifest(self.report, self.photos)
        self.assertEqual(sum(len(case['stages']) for case in cases), 72)

    def test_process_global_stage_sequence(self):
        second = self.report['cases'][1]
        self.assertEqual(stage_filename(1, 0, second['id'], second['stages'][0]),
                         'canon6d/0012-film_scale_and_expose-1200-801-3.f32')
        last = self.report['cases'][5]
        self.assertTrue(stage_filename(5, 11, last['id'], last['stages'][11]).startswith('canonr6/0071-'))

    def test_missing_photo(self):
        self.report['cases'].pop()
        self.reject('photo identity/order')

    def test_duplicate_photo(self):
        self.report['cases'][1] = copy.deepcopy(self.report['cases'][0])
        self.reject('photo identity/order')

    def test_missing_stage(self):
        self.report['cases'][0]['stages'].pop()
        self.reject('Stage order/count')

    def test_duplicate_stage(self):
        self.report['cases'][0]['stages'][1] = copy.deepcopy(self.report['cases'][0]['stages'][0])
        self.reject('Stage order/count')

    def test_false_trace(self):
        self.report['cases'][0]['tracedOutputExact'] = False
        self.reject('trace final mismatch')

    def test_wrong_shape(self):
        self.report['cases'][0]['stages'][0]['shape'][0] += 1
        self.reject('Stage photo shape')

    def test_recorded_nonfinite(self):
        self.report['cases'][0]['stages'][0]['nonfiniteSamples'] = 1
        self.reject('Recorded nonfinite')

    def test_nonfinite_range(self):
        self.report['cases'][0]['stages'][0]['max'] = float('nan')
        self.reject('Nonfinite stage range')


class SavedStageDataTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / 'stage.f32'
        np.array([-0.25, 0, 1.5], dtype='<f4').tofile(self.path)
        self.record = {'stage': 'test', 'shape': [1, 1, 3], 'sha256': sha(self.path),
                       'min': -0.25, 'max': 1.5, 'nonfiniteSamples': 0,
                       'negativeSamples': 1, 'aboveOneSamples': 1, 'zeroSamples': 1}

    def test_valid_data(self):
        self.assertTrue(validate_stage_file(self.path, self.record)['dataChecksRecomputed'])

    def test_changed_bytes(self):
        np.array([-0.25, 0, 2], dtype='<f4').tofile(self.path)
        with self.assertRaisesRegex(ValueError, 'Stage bytes changed'):
            validate_stage_file(self.path, self.record)

    def test_resealed_nan_rejected(self):
        np.array([-0.25, 0, float('nan')], dtype='<f4').tofile(self.path)
        self.record['sha256'] = sha(self.path)
        with self.assertRaisesRegex(ValueError, 'Nonfinite saved stage'):
            validate_stage_file(self.path, self.record)

    def test_resealed_truncated_rejected(self):
        self.path.write_bytes(self.path.read_bytes()[:-4])
        self.record['sha256'] = sha(self.path)
        with self.assertRaisesRegex(ValueError, 'Stage length'):
            validate_stage_file(self.path, self.record)

    def test_wrong_statistics(self):
        self.record['zeroSamples'] = 0
        with self.assertRaisesRegex(ValueError, 'Stage statistics changed'):
            validate_stage_file(self.path, self.record)

    def test_existing_report_refused_before_reading_data(self):
        output = Path(self.temp.name) / 'existing.json'
        output.write_text('keep')
        args = [sys.executable, '-O', str(Path(__file__).with_name('verify_saved_outputs.py'))]
        for name in ['evidence', 'inputs', 'outputs', 'stages']:
            args += ['--' + name, str(Path(self.temp.name) / 'absent')]
        result = subprocess.run(args + ['--output', str(output)], capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('New audit output required', result.stderr)
        self.assertEqual(output.read_text(), 'keep')


if __name__ == '__main__':
    unittest.main()
