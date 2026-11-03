"""Small generated-file tests only, never private images or engine execution."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

import numpy as np
from PIL import Image, PngImagePlugin

spec = importlib.util.spec_from_file_location('prepare_review', Path(__file__).with_name('prepare_review.py'))
review = importlib.util.module_from_spec(spec)
spec.loader.exec_module(review)


class FrozenReviewTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='round13-viewer-test-')
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.root, self.stages = self.base/'frozen', self.base/'stages'
        self.cases, self.mapping = [], {}
        for i in range(6):
            name = f'test{i}'
            folder = self.root/'candidate-natural'/(name+'-whole')
            folder.mkdir(parents=True)
            (self.stages/name).mkdir(parents=True)
            inp = np.full((4,5,3), .1, dtype='<f4').tobytes()
            (self.stages/name/'frozen.f32').write_bytes(inp)
            outputs, images = {}, {}
            for label, prefix, value in [('A','legacy',12345),('B','candidate',23456)]:
                rgb = np.full((4,5,3),value,dtype='<u2')
                rgba = np.concatenate([rgb,np.full((4,5,1),65535,dtype='<u2')],axis=2)
                for suffix,raw in [('.f32',inp),('-encoded.f32',inp),('.rgba16',rgba.tobytes())]:
                    filename=prefix+suffix
                    (folder/filename).write_bytes(raw)
                    outputs[filename]=review.digest(raw)
                im=Image.fromarray(np.rint(rgb/257).astype(np.uint8))
                im.save(self.root/'candidate-natural'/f'{name}-{label}.png')
                images[label]=im
            self.mapping[name]=['B','A'] if i%2 else ['A','B']
            crop=Image.new('RGB',(620,280),'#222')
            for key,offset in zip(self.mapping[name],[0,310]):
                crop.paste(images[key].crop((1,1,3,3)),(offset,25))
            (self.root/'review-v1').mkdir(exist_ok=True)
            path=self.root/'review-v1'/f'{name}-crop-anonymous.png'
            crop.save(path)
            self.cases.append({'id':name,'shape':[4,5,3],'input':f'{name}/frozen.f32','inputSha256':review.digest(inp),
                               'outputs':outputs,'crop':[1,1,2,2],'cropSha256':review.digest(path.read_bytes()),'rois':[[1,1,2,2]]})
        (self.root/'review-v1/anonymous-key.json').write_text(json.dumps(self.mapping))

    def validate(self):
        return review.validate(self.root,self.stages,self.cases,self.mapping)

    def test_real_frozen_manifest_has_only_six_development_cases_and_61_paths(self):
        cases,mapping=review.load_frozen()
        self.assertEqual(len(cases),6)
        self.assertEqual(len(review.required_files(self.root,self.stages,cases)),61)
        self.assertEqual(cases[3]['rois'][1],[310,390,35,60])
        self.assertEqual(mapping['canon600d'],['B','A'])

    def test_missing_files_exact_and_no_output_or_placeholder(self):
        report=review.inspect_files(self.base/'absent',self.base/'absent-stages',self.cases)
        self.assertEqual(report['missingCount'],61)
        with self.assertRaisesRegex(ValueError,'Missing frozen files'):
            review.validate(self.base/'absent',self.stages,self.cases,self.mapping)
        self.assertFalse((self.base/'absent').exists())

    def test_six_cases_validate_and_embed_original_anonymous_png_bytes(self):
        report,cases=self.validate()
        self.assertEqual(report['validation'],'PASS_HASH_SIZE_FINITE_PNG_PIXELS_MAPPING')
        import base64
        for c in cases:
            for label,key in zip(['X','Y'],self.mapping[c['id']]):
                self.assertEqual(base64.b64decode(c['images'][label].split(',')[1]),
                                 (self.root/'candidate-natural'/f"{c['id']}-{key}.png").read_bytes())
        self.assertNotIn('legacy',json.dumps(cases))
        self.assertNotIn('candidate',json.dumps(cases))

    def test_input_and_output_hash_drift_refused(self):
        for path in [self.stages/self.cases[0]['input'],self.root/'candidate-natural/test0-whole/candidate.f32']:
            raw=path.read_bytes();path.write_bytes(bytes([raw[0]^1])+raw[1:])
            with self.assertRaisesRegex(ValueError,'hash mismatch'):self.validate()
            path.write_bytes(raw)

    def test_finite_and_size_defense_even_if_test_hash_is_rebound(self):
        p=self.root/'candidate-natural/test0-whole/candidate.f32'
        raw=np.full((4,5,3),np.nan,dtype='<f4').tobytes();p.write_bytes(raw)
        self.cases[0]['outputs']['candidate.f32']=review.digest(raw)
        with self.assertRaisesRegex(ValueError,'Nonfinite'):self.validate()
        p.write_bytes(raw[:-4]);self.cases[0]['outputs']['candidate.f32']=review.digest(raw[:-4])
        with self.assertRaisesRegex(ValueError,'size mismatch'):self.validate()

    def test_png_pixel_change_and_per_version_metadata_refused(self):
        p=self.root/'candidate-natural/test0-A.png'
        Image.new('RGB',(5,4),'red').save(p)
        with self.assertRaisesRegex(ValueError,'display conversion'):self.validate()
        meta=PngImagePlugin.PngInfo();meta.add(b'gAMA',(45455).to_bytes(4,'big'))
        Image.new('RGB',(5,4),'red').save(p,pnginfo=meta)
        with self.assertRaisesRegex(ValueError,'metadata'):self.validate()

    def test_key_and_existing_crop_drift_refused(self):
        p=self.root/'review-v1/anonymous-key.json';p.write_text('{}')
        with self.assertRaisesRegex(ValueError,'Anonymous key differs'):self.validate()
        p.write_text(json.dumps(self.mapping))
        crop=self.root/'review-v1/test0-crop-anonymous.png';Image.new('RGB',(620,280),'red').save(crop)
        with self.assertRaisesRegex(ValueError,'hash mismatch'):self.validate()
        self.cases[0]['cropSha256']=review.digest(crop.read_bytes())
        with self.assertRaisesRegex(ValueError,'crop pixel/key'):self.validate()

    def test_bundle_stays_private_no_overwrite_and_no_answers_prefilled(self):
        report,cases=self.validate()
        out=self.base/'private-review'
        page=review.write_bundle(out,report,cases)
        body=page.read_text(encoding='utf-8')
        self.assertNotIn('/*__FROZEN_DATA__*/null',body)
        self.assertIn("[id,null]",body)
        self.assertIn('NOT_RUN_AWAITING_HUMAN',body)
        self.assertNotIn('anonymous-key',body)
        self.assertNotIn('candidate.rgba16',body)
        self.assertIn("connect-src 'none'",body)
        with self.assertRaisesRegex(ValueError,'existing output'):review.write_bundle(out,report,cases)
        with self.assertRaisesRegex(ValueError,'outside the Git'):review.write_bundle(review.REPO/'private',report,cases)
        with self.assertRaisesRegex(ValueError,'overlaps'):review.write_bundle(self.root,report,cases)


if __name__ == '__main__':
    unittest.main(verbosity=2)
