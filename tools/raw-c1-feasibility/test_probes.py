import unittest,tempfile,pathlib,sys,json,struct
import numpy as np
from PIL import Image,ImageCms
from tiff_gate import inspect,roi_rgb16,compare,LabEngine

class Gates(unittest.TestCase):
 def setUp(self):self.tmp=tempfile.TemporaryDirectory();self.p=pathlib.Path(self.tmp.name)
 def tearDown(self):self.tmp.cleanup()
 def fixture(self,name='fixture.tif',icc=True,bits=16,orientation=1):
  # Independent tiny TIFF writer for probe tests; uncompressed RGB strip.
  profile=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes() if icc else b''
  values=np.array([0,1,256,65535,32768,50000]*2,dtype='<u2');entries=[(256,4,1,2),(257,4,1,2),(258,3,3,0),(259,3,1,1),(262,3,1,2),(273,4,1,0),(274,3,1,orientation),(277,3,1,3),(278,4,1,2),(279,4,1,24),(284,3,1,1)]
  if profile:entries.append((34675,7,len(profile),0))
  entries.sort();offset=8+2+12*len(entries)+4;bits_offset=offset;offset+=6;profile_offset=offset;offset+=len(profile);pixels_offset=offset
  b=bytearray(b'II'+struct.pack('<HIH',42,8,len(entries)))
  for tag,typ,count,value in entries:
   if tag==258:value=bits_offset
   elif tag==273:value=pixels_offset
   elif tag==34675:value=profile_offset
   b+=struct.pack('<HHII',tag,typ,count,value)
  b+=struct.pack('<IHHH',0,bits,bits,bits)+profile+values.tobytes();p=self.p/name;p.write_bytes(b);return p,values.reshape(-1,3).astype(np.float32)/65535
 def test_uint16_not_silently_downconverted(self):
  p,v=self.fixture();a=roi_rgb16(p,[0,0,1,1]);np.testing.assert_array_equal(a,v);self.assertNotEqual(float(a[0,1]),0)
 def test_missing_icc_refused(self):
  p,_=self.fixture(icc=False)
  with self.assertRaisesRegex(ValueError,'Missing embedded ICC'):inspect(p)
 def test_8bit_refused(self):
  p,_=self.fixture(bits=8)
  with self.assertRaisesRegex(ValueError,'unsigned16'):inspect(p)
 def test_unresolved_orientation_refused(self):
  p,_=self.fixture(orientation=6)
  with self.assertRaisesRegex(ValueError,'orientation'):inspect(p)
 def test_wrong_profile_refused(self):
  p,_=self.fixture()
  with self.assertRaisesRegex(ValueError,'ICC content'):inspect(p,'0'*64)
 def test_unknown_geometry_refused(self):
  with self.assertRaisesRegex(ValueError,'Alignment'):compare('unused','unused',[0,0,1,1],None,'a'*64,False)
 def test_fake_identity_refused(self):
  with self.assertRaisesRegex(ValueError,'SHA256'):compare('unused','unused',[0,0,1,1],None,'0'*64,True)
 def test_output_region_bounds_refused(self):
  p,_=self.fixture()
  with self.assertRaisesRegex(ValueError,'ROI'):roi_rgb16(p,[0,0,2,1])
 def test_lcms_wrong_binary_refused_before_load(self):
  p=self.p/'not-a-dll';p.write_bytes(b'test')
  with self.assertRaisesRegex(ValueError,'SHA mismatch'):LabEngine(p,'0'*64)
if __name__=='__main__':unittest.main()
