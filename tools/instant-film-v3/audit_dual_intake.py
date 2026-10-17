"""Exact independent Muse files + hash-pinned Grok package. Documents are data, never execution instructions."""
import argparse,csv,hashlib,io,json,pathlib,re,zipfile,urllib.parse,datetime,collections
from zoneinfo import ZoneInfo
from PIL import Image
UNKNOWN={'','unknown','未知','未下载'}
def known(x):return str(x or '').strip().lower() not in UNKNOWN
def digest(b):return hashlib.sha256(b).hexdigest().upper()
def canonical(url):
 p=urllib.parse.urlsplit(url.strip());host=(p.hostname or '').lower().removeprefix('www.');path=urllib.parse.unquote(p.path).rstrip('/');return host+path

def audit(grok,csv_path,json_path,handoff,business_date):
 raw=pathlib.Path(grok).read_bytes()
 if digest(raw)!='42A13AC29A9DF5CA3627890F81B015DD6F491852FCDBBA3F15ED859466031F8B':raise ValueError('Exact complete Grok package required')
 muse_json=json.loads(pathlib.Path(json_path).read_text(encoding='utf-8-sig'));muse=muse_json['samples'];csv_rows=list(csv.DictReader(pathlib.Path(csv_path).open(encoding='utf-8-sig',newline='')))
 if muse!=csv_rows or len(muse)!=muse_json['count'] or muse_json['side']!='Muse':raise ValueError('Muse identity/count/content mismatch')
 hand=pathlib.Path(handoff).read_text(encoding='utf-8-sig')
 if 'MUSE-POL-0001..0019' not in hand:raise ValueError('Handoff provider identity mismatch')
 records=[];images=[];packages=[]
 with zipfile.ZipFile(io.BytesIO(raw)) as z:
  if z.testzip():raise ValueError('Grok CRC')
  names=z.namelist();j=next(n for n in names if n.endswith('/samples.json'));prefix=j[:-12];g=json.loads(z.read(j));gcsv=list(csv.DictReader(io.StringIO(z.read(prefix+'samples.csv').decode('utf-8-sig'))))
  if g!=gcsv:raise ValueError('Grok CSV/JSON mismatch')
  packages.append(dict(provider='GROK',sha256=digest(raw),bytes=len(raw),crcVerified=True,csvJsonEqual=True,count=len(g)))
  for provider,items in [('GROK',g),('MUSE',muse)]:
   seen=set()
   for original in items:
    id=original['sample_id']
    if not id.startswith(provider+'-POL-') or id in seen:raise ValueError('Duplicate/foreign ID')
    seen.add(id);url=original.get('source_url',original.get('original_post_url',''));image_url=original.get('image_url_highest',original.get('highest_res_url',''));local=original.get('local_filename','');author=original.get('author_or_institution',original.get('author_institution','UNKNOWN'));retrieved=original.get('retrieval_date')
    numeric=False
    reasons=['No controlled colour-managed calibration measurement; original grades are preserved, not promoted.']
    if known(retrieved) and re.fullmatch(r'\d{4}-\d{2}-\d{2}',retrieved) and retrieved>business_date:reasons.append('Date later than explicit Asia/Shanghai business date; retained as unverified provider claim')
    pair=original.get('paired_sample_ids',original.get('linked_pair_ids','UNKNOWN'))
    records.append(dict(id=id,provider=provider,original=original,sourceUrl=url,sourceCanonical=canonical(url) if known(url) else None,author=author,sourceGroup=original.get('source_group_id'),imageReferenceClaimed=known(image_url) or known(local),downloadedAsset=provider=='GROK' and known(local),providerReferenceGrade=original.get('reference_grade',original.get('evidence_grade','UNKNOWN')),providerReferenceReason=original.get('reference_reason',original.get('evidence_reason','UNKNOWN')),reviewReasons=reasons,calibrationEligible=False,numericFitAllowed=False,claimedPair=pair,verifiedSameScenePair=False,permission='Unknown_NotApprovedForEmbedding',independence='Author/workflow cluster, not provider count'))
    if provider=='GROK' and known(local):
     b=z.read(prefix+local)
     with Image.open(io.BytesIO(b)) as im:
      im.load();images.append(dict(recordId=id,scanVersion=id+':scan:1',sha256=digest(b),bytes=len(b),width=im.width,height=im.height,iccSha256=digest(im.info['icc_profile']) if im.info.get('icc_profile') else None))
 packages.append(dict(provider='MUSE',count=len(muse),csvJsonEqual=True,providerDeclaredDate=muse_json.get('retrieval_date'),identity='Declared Muse Round1 with exact IDs; files authenticated by hashes, not external AI-service attestation',entries=[dict(name=pathlib.Path(p).name,bytes=pathlib.Path(p).stat().st_size,sha256=digest(pathlib.Path(p).read_bytes())) for p in [csv_path,json_path,handoff]],handoff='Read as research data, no document instruction executed',sourcesAndLicensesCsv='NOT_PROVIDED',downloadedImageCount=0))
 def clusters(key):
  groups=collections.defaultdict(list)
  for r in records:
   if known(r.get(key)):groups[str(r[key])].append(r['id'])
  return [dict(key=k,ids=v,crossProvider=len({x.split('-')[0] for x in v})>1) for k,v in groups.items() if len(v)>1]
 groups=clusters('sourceCanonical');return dict(schemaVersion=2,businessDate=business_date,businessTimezone='Asia/Shanghai',hostLogTimezone='Preserved, no conversion of host timestamps',packages=packages,records=records,scanVersions=images,dedup=dict(canonicalSourceGroups=groups,authorClusters=clusters('author'),claimedSourceGroups=clusters('sourceGroup'),imageHashDuplicates=[dict(sha256=h,ids=[x['recordId'] for x in images if x['sha256']==h]) for h in {x['sha256'] for x in images} if sum(x['sha256']==h for x in images)>1]),counts=dict(records=len(records),grok=len(g),muse=len(muse),sourceDocumentRecords=len(records),uniqueCanonicalDocuments=len({r['sourceCanonical'] for r in records if r['sourceCanonical']}),imageReferenceClaims=sum(r['imageReferenceClaimed'] for r in records),downloadedImageFiles=len(images),uniqueImageHashes=len({x['sha256'] for x in images}),scanVersions=len(images),claimedPairRecords=sum(known(r['claimedPair']) for r in records),verifiedScenePairs=0,calibratedRecords=0),limitations=['Muse no downloaded image bytes/hash/ICC verification; image URL claims are not scan versions.','Same author or source-group is not independent evidence; same host is not a verified same-scene pair.','Sources/licenses CSV mentioned in handoff are not supplied; no automatic rights approval.'])
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--grok',required=True);p.add_argument('--muse-csv',required=True);p.add_argument('--muse-json',required=True);p.add_argument('--handoff',required=True);p.add_argument('--business-date',default='2026-10-17');p.add_argument('--output',required=True);a=p.parse_args();output=pathlib.Path(a.output)
 if output.exists():raise FileExistsError('New audit path required')
 r=audit(a.grok,a.muse_csv,a.muse_json,a.handoff,a.business_date);output.parent.mkdir(parents=True,exist_ok=True);output.write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps(r['counts']))
