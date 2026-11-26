"""Small on-disk fixtures. Production pins are replaced in this test process only."""
from dataclasses import replace
import hashlib
import json
from pathlib import Path
import struct
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

import company_preflight as pre
import observe_company as cli


def sha(data):
    return hashlib.sha256(data).hexdigest()


def put(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')


def tiny_tiff(profile, *, tag=34675, kind=7, count=None):
    # Classic first IFD with an external UNDEFINED value and a one-byte grayscale strip.
    offset = 8 + 2 + 10 * 12 + 4
    fields = [(256,4,1,1), (257,4,1,1), (258,3,1,8), (259,3,1,1), (262,3,1,1),
              (273,4,1,offset+len(profile)), (277,3,1,1), (278,4,1,1), (279,4,1,1),
              (tag,kind,len(profile) if count is None else count,offset)]
    entries = b''.join(struct.pack('<HHII', *field) for field in fields)
    return struct.pack('<2sHIH', b'II', 42, 8, len(fields)) + entries + bytes(4) + profile + b'\x7f'


class TestCompanyPreflight(unittest.TestCase):
    def setUp(self):
        self.root = Path(self.enterContext(tempfile.TemporaryDirectory())).resolve()
        self.run_dir = self.root/'delivery'/'legacy-processed-three-cycle'
        self.stage = self.root/'stage'
        self.stage.mkdir()
        first = self.run_dir/'export-first'
        first.mkdir(parents=True)
        self.launch = self.run_dir/'LAUNCH.json'
        self.out = self.root/'new-observation'
        self.icc = self.root/'current.icc'
        self.payload = self.root/'frozen.rgb48'
        self.image = first/'first capture.tif'
        self.sidecar = self.image.with_name(self.image.name+'.kitao-export.json')
        self.profile = b'fixture ICC bytes; not a real company profile'
        self.icc.write_bytes(self.profile)
        self.payload.write_bytes(bytes(range(12)))
        self.image.write_bytes(tiny_tiff(self.profile))
        self.app, self.core = self.stage/'KitaoPhotoSelector.dll', self.stage/'RAWSelectionAssistant.Core.dll'
        self.app.write_bytes(b'fixture App')
        self.core.write_bytes(b'fixture Core')
        self.task = {'icc':str(self.icc), 'payload':str(self.payload), 'pixelSha256':sha(self.payload.read_bytes()).upper(),
                     'width':2, 'height':1, 'orientation':1}
        self.base = {'task':self.task, 'stage':str(self.stage), 'scenario':'points', 'mode':'processed-three-cycle',
                     'appSha256':sha(self.app.read_bytes()), 'coreSha256':sha(self.core.read_bytes())}
        put(self.launch, self.base)
        self.original = self.launch.read_bytes()
        self.pins = pre.RunPins(sha(self.original), self.base['appSha256'], self.base['coreSha256'],
                                sha(self.payload.read_bytes()), sha(self.image.read_bytes()), sha(self.profile), len(self.profile), 2, 1)
        self.done = {'OutputPath':str(self.image), 'OutputHash':self.pins.tiff.upper(), 'CompletionState':'Completed'}
        put(self.sidecar, self.done)
        self.enterContext(patch.object(pre, 'COMPANY_63A', self.pins))

    def test_declared_icc_full_hash_succeeds_without_export_evidence(self):
        self.task['iccSha256'] = self.pins.icc.upper()
        put(self.launch, self.base)
        self.sidecar.unlink()  # A declared digest never needs the missing-field path.
        # The declared-field branch retains its own LAUNCH/asset binding contract.
        with patch.object(pre, 'COMPANY_63A', replace(self.pins, app='0'*64, payload='0'*64)):
            result = pre.verify_inputs(self.launch)
        with self.assertRaisesRegex(pre.PreflightError, 'Original 63a'):
            pre.verify_inputs(self.launch, require_original=True)
        self.assertEqual(result.receipt['iccBinding'], 'LAUNCH_TASK_SHA256')
        self.assertIsNone(result.receipt['sameRunFirstExport'])
        self.assertEqual(result.icc, self.profile)

    def test_declared_conflict_or_invalid_value_stops_before_any_subprocess(self):
        for index, digest in enumerate(['f'*64, None, '', 'invalid']):
            with self.subTest(digest=digest):
                self.task['iccSha256'] = digest
                put(self.launch, self.base)
                # Even a matching fixture launch pin and perfect export evidence must not rescue it.
                out = self.root/f'rejected-{index}'
                with patch.object(pre, 'COMPANY_63A', replace(self.pins, launch=sha(self.launch.read_bytes()))), \
                     patch.object(cli.subprocess, 'Popen', side_effect=AssertionError('unexpected process')):
                    with self.assertRaisesRegex(pre.PreflightError, 'Current designated ICC'):
                        cli.main(['--baseline-launch',str(self.launch),'--output',str(out)])
                stop = json.loads((out/'INPUT_PREFLIGHT.json').read_bytes())
                self.assertEqual(stop['status'], 'STOP_BEFORE_HOST')
                self.assertFalse(stop['targetStarted'])
                self.assertEqual(list(out.iterdir()), [out/'INPUT_PREFLIGHT.json'])

    def test_pinned_missing_field_fixes_only_the_new_observation(self):
        result = pre.verify_inputs(self.launch, require_original=True)
        r = result.receipt
        self.assertEqual(r['iccBinding'], 'CURRENT_INPUT_FIXED_BY_SAME_RUN_EXPORT')
        self.assertEqual((r['launchFileSha256'],r['appSha256'],r['coreSha256'],r['payloadSha256']),
                         (self.pins.launch,self.pins.app,self.pins.core,self.pins.payload))
        e = r['sameRunFirstExport']
        self.assertEqual((e['tiffSha256'],e['sidecarOutputHash'],e['embeddedIccBytes'],e['embeddedIccSha256']),
                         (self.pins.tiff,self.pins.tiff,len(self.profile),self.pins.icc))
        self.assertEqual((e['ifd'], e['tag'], e['type']), (0,34675,7))
        self.assertFalse(any(r[k] for k in ['historicalTaskHasIccSha256','historicalIccFileReadProven',
                                             'historicalInputsFullyReproduced','largeImageReopenPassed']))
        self.out.mkdir()
        put(self.out/'INPUT_PREFLIGHT.json', {'status':'INPUTS_VERIFIED_BEFORE_HOST_BUILD','input':r})
        task, binding = pre.observation_task(self.launch,self.stage,self.out)
        self.assertEqual(binding,r)
        self.assertNotIn('iccSha256',task)
        self.assertEqual(task['observationIccSha256'], self.pins.icc)
        self.assertEqual(Path(task['icc']).parent,self.out)
        self.icc.write_bytes(b'changed after the final preflight')
        self.assertEqual(Path(task['icc']).read_bytes(),self.profile)
        self.assertEqual(self.launch.read_bytes(),self.original)

    def test_wrong_sidecar_hash_state_path_or_current_icc_stops(self):
        for field, value in [('OutputHash','0'*64),('CompletionState','Encoded'),('OutputPath',str(self.root/'other.tif'))]:
            with self.subTest(field=field):
                put(self.sidecar,{**self.done,field:value})
                with self.assertRaisesRegex(pre.PreflightError,'sidecar'): pre.verify_inputs(self.launch)
        put(self.sidecar,self.done)
        for data in [self.profile[:-1]+b'!',self.profile[:-1]]:
            with self.subTest(iccLength=len(data)):
                self.icc.write_bytes(data)
                with self.assertRaisesRegex(pre.PreflightError,'Current designated ICC'): pre.verify_inputs(self.launch)

    def test_other_launch_app_core_or_payload_cannot_enter_fallback(self):
        for path, reason in [(self.launch,'Original 63a'),(self.app,'appSha256'),(self.core,'coreSha256'),(self.payload,'payload')]:
            with self.subTest(file=path.name):
                original = path.read_bytes()
                path.write_bytes(original+b' ' if path==self.launch else original[:-1]+b'!')
                with self.assertRaisesRegex(pre.PreflightError,reason): pre.verify_inputs(self.launch)
                path.write_bytes(original)

    def test_tiff_and_sidecar_changed_together_still_fail_fixed_whole_file_digest(self):
        # Change just the pixel: the embedded ICC and matching sidecar alone cannot admit it.
        altered = self.image.read_bytes()[:-1]+b'\x00'
        self.image.write_bytes(altered)
        put(self.sidecar,{**self.done,'OutputHash':sha(altered)})
        with self.assertRaisesRegex(pre.PreflightError,'Pinned original first TIFF'): pre.verify_inputs(self.launch)

    def test_first_ifd_tag_type_length_and_value_are_all_required(self):
        for name, image in [('tag',tiny_tiff(self.profile,tag=34674)),('type',tiny_tiff(self.profile,kind=1)),
                            ('length',tiny_tiff(self.profile,count=len(self.profile)-1)),
                            ('profile',tiny_tiff(self.profile[:-1]+b'!'))]:
            with self.subTest(fault=name):
                self.image.write_bytes(image)
                put(self.sidecar,{**self.done,'OutputHash':sha(image)})
                # Bind this test export so the test actually reaches extraction.
                with patch.object(pre,'COMPANY_63A',replace(self.pins,tiff=sha(image))):
                    with self.assertRaisesRegex(pre.PreflightError,'ICC'): pre.verify_inputs(self.launch)

    def test_change_after_preflight_stops_before_guard(self):
        r = pre.verify_inputs(self.launch).receipt
        self.out.mkdir()
        put(self.out/'INPUT_PREFLIGHT.json', {'status':'INPUTS_VERIFIED_BEFORE_HOST_BUILD','input':r})
        put(self.sidecar,{**self.done,'ExportId':'different sidecar bytes even with identical OutputHash'})
        with self.assertRaisesRegex(pre.PreflightError,'changed since preflight'):
            pre.observation_task(self.launch,self.stage,self.out)
        self.assertFalse(json.loads((self.out/'PRELAUNCH_STOP.json').read_bytes())['targetStarted'])
        self.assertFalse((self.out/'observation-input.icc').exists())

    def test_one_command_checks_original_and_writes_receipt_before_dispatch(self):
        validation = self.root/'VALIDATION.json'
        put(validation,{'legacyCandidateGuard':{'path':str(self.run_dir)}})
        tool = self.root/'fixture-dump.exe'
        tool.write_bytes(b'fixture; never executed')
        with patch.object(cli,'VALIDATION',validation), patch.object(cli.shutil,'which',return_value=str(tool)), \
             patch.object(cli.shutil,'disk_usage',return_value=SimpleNamespace(free=4*1024**3)), \
             patch.object(cli.subprocess,'check_output',side_effect=[(cli.F319_PE+'\n')*2,'10.0.750501+fixture']) as checks, \
             patch.object(cli.subprocess,'run') as dispatch, patch.object(cli,'print',create=True):
            cli.main(['--company-63a','--output',str(self.out)])
        self.assertEqual(checks.call_count,2)
        self.assertEqual(checks.call_args_list[1].args[0], [str(tool),'--version'])
        cmds = [c.args[0] for c in dispatch.call_args_list]
        self.assertEqual(len(cmds),4)
        self.assertEqual(cmds[0][:2], ['dotnet','publish'])
        self.assertEqual([Path(c[1]).name for c in cmds[1:]], ['prepare_stage.py','capture.py','analyze.py'])
        cap = cmds[2]
        self.assertEqual(Path(cap[cap.index('--baseline-launch')+1]), self.launch)
        self.assertEqual(Path(cap[cap.index('--out')+1]), self.out/'run')
        receipt = json.loads((self.out/'INPUT_PREFLIGHT.json').read_bytes())
        self.assertEqual(receipt['status'], 'INPUTS_VERIFIED_BEFORE_HOST_BUILD')
        self.assertEqual(receipt['input']['iccBinding'], 'CURRENT_INPUT_FIXED_BY_SAME_RUN_EXPORT')
        self.assertFalse((self.out/'run').exists())
        self.assertEqual(self.original,self.launch.read_bytes())

    def test_one_command_uses_receipted_run_and_refuses_existing_output(self):
        validation = self.root/'VALIDATION.json'
        put(validation,{'legacyCandidateGuard':{'path':str(self.run_dir)}})
        expected = self.run_dir.parent.with_name('rgb48-holder-boundary-company-once-20261126')
        with patch.object(cli,'VALIDATION',validation), patch.object(cli,'run') as start:
            cli.main(['--company-63a'])
            start.assert_called_once_with(self.launch,expected,None,None,True)
        expected.mkdir()
        keep = expected/'STOP.json'
        keep.write_bytes(b'old stop')
        with patch.object(cli,'VALIDATION',validation), self.assertRaises(FileExistsError):
            cli.main(['--company-63a'])
        self.assertEqual(keep.read_bytes(), b'old stop')


if __name__ == '__main__':
    unittest.main()
