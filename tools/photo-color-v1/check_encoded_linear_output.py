"""Read actual encoded-file pixel dumps against pinned upstream transfer functions."""
import argparse,ast,json,pathlib,sys
ROOT=pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'artifacts/v3-corrections/reference-python'))
import numpy as np

def main():
 parser=argparse.ArgumentParser();parser.add_argument('--input-dir',type=pathlib.Path,required=True);parser.add_argument('--output',type=pathlib.Path,required=True);args=parser.parse_args()
 source=ROOT/'artifacts/color-chain-next/skimage-colorconv.py';tree=ast.parse(source.read_text());functions=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in ['rgb2xyz','xyz2rgb']]
 for n in functions:n.decorator_list=[]
 ns=dict(np=np,xyz_from_rgb=np.eye(3),rgb_from_xyz=np.eye(3),_prepare_colorarray=lambda x,channel_axis=-1:np.array(x,dtype=np.float64),_convert=lambda m,x:np.array(x,dtype=np.float64)@m.T)
 exec(compile(ast.Module(body=functions,type_ignores=[]),str(source),'exec'),ns)
 rows=[]
 for item in json.loads((args.input_dir/'MANIFEST.json').read_text()):
  folder=args.input_dir/item['id'];x=np.fromfile(folder/'input.f32',dtype='<f4').astype(np.float64).reshape(-1,3);expected=ns['xyz2rgb'](ns['rgb2xyz'](x)*2**item['exposure']).astype(np.float32);results=[]
  if not np.isfinite(expected).all():raise ValueError('Non-finite independent output')
  for format in ['Tiff','Png','Jpeg']:
   scale=255 if format=='Jpeg' else 65535;actual=np.fromfile(folder/(format+'.raw'),dtype='u1' if scale==255 else '<u2').astype(float).reshape(-1,3)
   if actual.shape!=expected.shape:raise ValueError('Incomplete file pixels')
   oracle=np.rint(expected*scale).clip(0,scale);error=np.abs(actual-oracle);maximum=float(error.max());mae=float(error.mean());rmse=float(np.sqrt((error**2).mean()));valid=(mae<=5 and rmse<=15) if scale==255 else maximum==0
   results.append(dict(format=format,maximum=maximum,mae=mae,rmse=rmse,valid=valid))
  rows.append(dict(id=item['id'],sourceHash=item['inputSha256'],rows=len(x),formats=results))
 valid=all(f['valid'] for row in rows for f in row['formats']);args.output.write_text(json.dumps(dict(status='PASS' if valid else 'FAIL',reference='Pinned original scikit-image transfer functions, actual file pixels',floatBoundary='Frozen float32 display samples before quantization',rows=rows),indent=2)+'\n');print(valid,rows);return int(not valid)
if __name__=='__main__':raise SystemExit(main())
