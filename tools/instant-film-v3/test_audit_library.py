import unittest,importlib.util,tempfile,json,zipfile
from pathlib import Path
spec=importlib.util.spec_from_file_location("audit",Path(__file__).with_name("audit_library.py"));m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class AuditTests(unittest.TestCase):
 def test_truncated_is_never_complete(self):
  with tempfile.TemporaryDirectory() as d:
   p=Path(d)/"bad.zip";p.write_bytes(b"PK\x03\x04"+b"x"*64);r=m.audit(p)
   self.assertEqual(r["packages"][0]["integrity"],"InvalidOrTruncated");self.assertEqual(r["counts"]["rawRecords"],0);self.assertFalse(r["calibrationEligible"])
 def test_muse_not_substituted_and_provider_ids_fail_closed(self):
  r=m.audit(None);self.assertTrue(any("MUSE" in x for x in r["gaps"]))
  out=dict(records=[],sources=[],images=[],relationships=[],observations=[],permissions=[],gaps=[])
  with self.assertRaises(ValueError):m.consume(out,"MUSE",[{"sample_id":"GROK-POL-0001"}],lambda _:b"", "2026-10-16")
if __name__=="__main__":unittest.main()
