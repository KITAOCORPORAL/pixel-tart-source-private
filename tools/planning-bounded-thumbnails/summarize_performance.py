import pathlib,json,statistics,sys
r=pathlib.Path("E:/Kitao-Deliveries/planning-bounded-thumbnails-20261023");rows=[]
for mode in ['before','after']:
 values=[json.loads(p.read_text(encoding='utf-8-sig')) for p in sorted(r.glob('perf-final-'+mode+'-*/MEASUREMENT.json'))]
 if len(values)!=3:raise RuntimeError('Need exactly three completed measurements per mode')
 rows.append(dict(mode=mode,runs=values,median={k:statistics.median(v[k] for v in values) for k in ['coldOpenMs','warmRefreshMs','switchToEmptyProjectMs','peakWorkingBytes','peakPrivateBytes','allocatedBytes','cacheHitCount','unchangedCacheFiles']}))
(r/'PERFORMANCE_COMPARISON.json').write_text(json.dumps(dict(fixtureManifest=str(r/'large-fixtures/MANIFEST.json'),scope='Same test binary, separate processes, same machine/fixtures, real stores/36shots; not native desktop benchmark',rows=rows),indent=2));print(json.dumps(rows))
