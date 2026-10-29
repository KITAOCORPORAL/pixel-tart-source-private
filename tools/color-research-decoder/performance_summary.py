import json,statistics,sys
from pathlib import Path
r=Path(sys.argv[1]);out={};indices=[int(x) for x in sys.argv[2:]] or [1,2,3]
for mode in ['baseline','matrix','headroom']:
 rows=[json.loads((r/f'perf-{mode}-{i}/performance.json').read_text()) for i in indices];out[mode]={'decodeMilliseconds':[x['decodeMs'] for x in rows],'medianDecodeMs':statistics.median(x['decodeMs'] for x in rows),'peakAtDecodeBytes':[x['peakAtDecode'] for x in rows],'masterHashes':[x['recipe']['pixelHash'] for x in rows],'researchAssemblyHashes':[x.get('researchAssemblyHash') for x in rows]}
out['matrixExtraMedianMs']=out['matrix']['medianDecodeMs']-out['baseline']['medianDecodeMs'];out['scope']='separate-process three runs, same source hash work, includes native decode and float conversion, no intermediate disk or second decode; not broad benchmark';print(json.dumps(out,indent=2))
