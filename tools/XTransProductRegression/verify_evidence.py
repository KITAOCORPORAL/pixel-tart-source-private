"""Verify stored regression evidence without loading RAW or native libraries.

Usage: python verify_evidence.py ARTIFACT_ROOT OUTPUT_DIR
The output directory must not exist. All original artifacts are read-only.
"""
from pathlib import Path
import argparse, array, binascii, hashlib, json, struct, sys, zlib
import xml.etree.ElementTree as ET
from datetime import datetime, timezone


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest().upper()


def save(path, value):
    Path(path).write_text(json.dumps(value, indent=2, ensure_ascii=False)+'\n', encoding='utf-8')


def png_rgb(path):
    data=Path(path).read_bytes()
    assert data[:8]==b'\x89PNG\r\n\x1a\n', 'PNG signature'
    offset=8; compressed=[]; profile=None; header=None; ended=False
    while offset<len(data):
        length=struct.unpack_from('>I',data,offset)[0]
        kind=data[offset+4:offset+8]; chunk=data[offset+8:offset+8+length]
        assert len(chunk)==length, 'Truncated PNG'
        crc=struct.unpack_from('>I',data,offset+8+length)[0]
        assert binascii.crc32(kind+chunk)&0xffffffff==crc, 'PNG CRC'
        if kind==b'IHDR':
            assert header is None, 'Duplicate IHDR'
            header=struct.unpack('>IIBBBBB',chunk)
        elif kind==b'IDAT': compressed.append(chunk)
        elif kind==b'iCCP':
            assert profile is None, 'Duplicate ICC'
            name, rest=chunk.split(b'\x00',1)
            assert rest[0]==0, 'ICC compression'
            profile=zlib.decompress(rest[1:])
        elif kind==b'IEND': ended=True
        offset+=length+12
    assert ended and offset==len(data), 'PNG end'
    width,height,depth,color,compression,filtering,interlace=header
    assert depth in (8,16) and color==2 and compression==filtering==interlace==0
    bpp=3*(depth//8); stride=width*bpp; packed=zlib.decompress(b''.join(compressed))
    assert len(packed)==height*(stride+1)
    previous=bytearray(stride); pixels=bytearray()
    for row_index in range(height):
        at=row_index*(stride+1); mode=packed[at]
        row=bytearray(packed[at+1:at+1+stride]); assert mode<=4
        for i in range(stride):
            left=row[i-bpp] if i>=bpp else 0; above=previous[i]
            upper_left=previous[i-bpp] if i>=bpp else 0
            if mode==1: predict=left
            elif mode==2: predict=above
            elif mode==3: predict=(left+above)//2
            elif mode==4:
                p=left+above-upper_left
                distances=(abs(p-left),abs(p-above),abs(p-upper_left))
                predict=(left,above,upper_left)[distances.index(min(distances))]
            else: predict=0
            row[i]=(row[i]+predict)&255
        pixels.extend(row); previous=row
    values=array.array('H' if depth==16 else 'B'); values.frombytes(pixels)
    if depth==16 and sys.byteorder=='little': values.byteswap()
    return width,height,depth,values,profile


def compare(left,right):
    assert len(left)==len(right) and len(left)%3==0
    changed_channels=changed_pixels=maximum=total=0
    for i in range(0,len(left),3):
        pixel_changed=False
        for channel in range(3):
            difference=abs(left[i+channel]-right[i+channel])
            changed_channels+=difference!=0; pixel_changed|=difference!=0
            maximum=max(maximum,difference); total+=difference
        changed_pixels+=pixel_changed
    return dict(changedPixels=changed_pixels,changedChannels=changed_channels,
                maxChannelDifference=maximum,meanAbsoluteDifferencePerChannel=total/len(left),
                pixelCount=len(left)//3,channelCount=len(left))


def verify(root, out):
    out.mkdir(parents=True,exist_ok=False)
    locked=json.loads((root/'SOURCE_AND_RUN_LOCK.json').read_text('utf-8'))
    plans={name:json.loads((root/f'PLAN-{name}.json').read_text('utf-8')) for name in ('A','B','control')}
    identities={}
    for name, plan in plans.items():
        assert plan['ProductSourceSha']==locked['featureProductSourceSha']
        assert plan['CandidateNativeSha256']==locked['candidateNativeSha']
        assert plan['DefaultNativeSha256']==locked['defaultNativeSha']
        assert plan['MaximumHeavyDecodes']==locked['maximumHeavyDecodes']==6
        assert plan['Task']==locked['rawTask'] and plan['Steps']==locked['steps']
        for record in plan['Files']:
            key=str(Path(record['Path']).resolve())
            assert key not in identities or identities[key]==record['Sha256']
            identities[key]=record['Sha256']
    checked=[]
    for file, expected in identities.items():
        actual=sha(file); assert actual==expected.upper(), f'Frozen drift: {file}'
        checked.append(dict(name=Path(file).name,sha256=actual,bytes=Path(file).stat().st_size))
    old=json.loads((root/'OLD_LEDGER_BINDING.json').read_text('utf-8'))
    for record in old: assert sha(record['Path'])==record['Sha256']
    ledger=Path(plans['A']['LedgerRoot'])
    consumed=list(ledger.glob('*.consumed'))
    path_files=list(Path(plans['A']['EvidenceRoot']).glob('**/*'))
    path_files=[p for p in path_files if p.is_file()]
    launch=json.loads((root/'ui-steps/A-cold/LAUNCH.json').read_text('utf-8'))
    assert launch['productSourceSha']==locked['featureProductSourceSha']
    assert launch['planSha256']==sha(root/'PLAN-A.json')
    assert launch['mainDllSha256']==sha(root/'app/KitaoPhotoSelector.dll')
    assert len(consumed)==0 and len(path_files)==0, 'RAW/path evidence changed: audit before continuation'
    save(out/'IDENTITY_VERIFICATION.json',dict(status='PASS',verifiedUtc=datetime.now(timezone.utc).isoformat(),
         productSourceSha=locked['featureProductSourceSha'],candidateResearchSha=locked['candidateResearch'],
         candidateNativeSha256=locked['candidateNativeSha'],defaultNativeSha256=locked['defaultNativeSha'],
         frozenFilesVerified=len(checked),files=checked,oldLedgerFilesVerified=len(old),oldLedgersUnchanged=True,
         consumedHeavyDecodes=0,maximumHeavyDecodes=6,pathEvidenceFiles=0,launchBindingVerified=True,
         newRawDecoderCalls=0,newNativeBuilds=0))
    plan=json.loads((root/'CROP_PLAN_FINAL.json').read_text('utf-8'))
    folder=root/'crops-frozen-a24fac65'
    receipt=json.loads((folder/'CROP_RECEIPT.json').read_text('utf-8'))
    assert receipt['rawDecoderCalls']==0 and receipt['syntheticNeutralCropEquivalence']
    icc=sha(folder/'product-srgb.icc'); assert icc==receipt['iccSha']
    stored=[]
    for record in plan['inputs']:
        actual=sha(record['path']); assert actual==record['sha256'].upper()
        stored.append(dict(label=record['label'],sha256=actual,bytes=Path(record['path']).stat().st_size))
    decoded={}; file_index=[]
    for record in receipt['receipts']:
        name=record['name']; raw=(folder/(name+'.rgb48')).read_bytes()
        assert hashlib.sha256(raw).hexdigest().upper()==record['renderedRgb16Sha']
        for depth in (8,16):
            png=folder/f'{name}-{depth}.png'; width,height,bits,values,profile=png_rgb(png)
            assert (width,height,bits)==(record['width'],record['height'],depth)
            assert hashlib.sha256(profile).hexdigest().upper()==icc
            canonical=array.array(values.typecode,values)
            if depth==16 and sys.byteorder!='little': canonical.byteswap()
            expected=record['renderedRgb16Sha' if depth==16 else 'displayRgb8Sha']
            assert hashlib.sha256(canonical.tobytes()).hexdigest().upper()==expected
            if depth==16: assert canonical.tobytes()==raw
            decoded[(name,depth)]=values
            file_index.append(dict(file=png.name,bytes=png.stat().st_size,sha256=sha(png)))
        file_index.append(dict(file=name+'.rgb48',bytes=len(raw),sha256=sha(folder/(name+'.rgb48'))))
    metrics=[]
    for roi in plan['rois']:
        for baseline in ('old-first','old-second'):
            row=dict(roi=roi['name'],x=roi['x'],y=roi['y'],width=roi['width'],height=roi['height'],baseline=baseline)
            for depth in (8,16):
                row[f'renderedRgb{depth}']=compare(decoded[(roi['name']+'-candidate',depth)],decoded[(roi['name']+'-'+baseline,depth)])
            metrics.append(row)
    save(out/'FIXED_CROP_VERIFICATION.json',dict(status='PASS',source='existing saved MakeMemoryImage RGB16, neutral product render',
         productSourceSha=locked['featureProductSourceSha'],rawDecoderCalls=0,storedFinishedInputs=stored,
         pngCount=30,rgb48Count=15,allPngCrcsPassed=True,allDecodedPixelsMatchReceipt=True,iccSha256=icc,
         iccValidationScope='byte identity only; inherited product profile, no independent ICC color-accuracy validation',
         files=file_index,comparisons=metrics))
    tests=[]
    for trx in sorted(root.glob('tests-*/*.trx')):
        doc=ET.parse(trx); namespace={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
        results=doc.findall('.//t:UnitTestResult',namespace)
        records=[dict(name=r.attrib['testName'],outcome=r.attrib['outcome'],
             messages=[m.text for m in r.findall('.//t:Message',namespace)]) for r in results]
        assert all(r['outcome'] in ('Passed','NotExecuted') for r in records), 'Failed test evidence'
        counts=doc.find('.//t:Counters',namespace).attrib
        assert int(counts['failed'])==0 and int(counts['passed'])==sum(r['outcome']=='Passed' for r in records)
        tests.append(dict(file=trx.relative_to(root).as_posix(),sha256=sha(trx),counts=counts,results=records))
    save(out/'TEST_RECEIPTS_VERIFIED.json',dict(status='PASS_EXISTING_RESULTS_READBACK',rerun=False,
        passed=sum(sum(r['outcome']=='Passed' for r in t['results']) for t in tests),
        notExecuted=sum(sum(r['outcome']=='NotExecuted' for r in t['results']) for t in tests),suites=tests))
    print(json.dumps(dict(status='PASS',frozenFiles=len(checked),pngs=30,cropComparisons=len(metrics),heavyDecodes=0)))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('artifact_root',type=Path); parser.add_argument('output_dir',type=Path)
    args=parser.parse_args(); verify(args.artifact_root.resolve(),args.output_dir.resolve())
