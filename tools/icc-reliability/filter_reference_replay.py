"""Research-only source extraction compile; GPL implementation remains external artifacts, never product."""
import pathlib,re,subprocess,json,sys,math,random
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/icc-reliability';random.seed(713);rows=[]
for w,h,r in [(9,7,1),(17,13,3),(127,61,7),(73,49,11)]:
 x=[random.random() for _ in range(w*h)];horizontal=[]
 for y in range(h):
  for xx in range(w):horizontal.append(sum(x[y*w+max(0,xx-r):y*w+min(w,xx+r+1)])/(min(w-1,xx+r)-max(0,xx-r)+1))
 oracle=[]
 for y in range(h):
  for xx in range(w):oracle.append(sum(horizontal[yy*w+xx] for yy in range(max(0,y-r),min(h,y+r+1)))/(min(h-1,y+r)-max(0,y-r)+1))
 values=[]
 for mode in [0,1]:
  p=subprocess.run([str(A/'filter_refs.exe'),str(mode),str(w),str(h),str(r)],input=' '.join(map(str,x)),text=True,capture_output=True,timeout=30);assert p.returncode==0;v=[float(z) for z in p.stdout.split()];assert len(v)==w*h;values.append(v)
 rows.append(dict(w=w,h=h,radius=r,rtMaxVsDouble=max(abs(a-b) for a,b in zip(values[0],oracle)),dtHelperMaxVsDouble=max(abs(a-b) for a,b in zip(values[1],oracle)),rtVsDt=max(abs(a-b) for a,b in zip(*values)),classification='finite clipped-window scalar blur; not full Structure equivalence',dtScope='exact original horizontal helper twice via transpose, NOT original optimized vertical/fulldt_box_mean',rtScope='exact boxblur function scalar path; noSSE2/OpenMP'))
print(json.dumps(rows,indent=2))
