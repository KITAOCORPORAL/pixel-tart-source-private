"""Explicit, versioned host paths. Never substitutes an environment or input silently."""
import json,os
from pathlib import Path

def environment(path=None):
    path=path or os.environ.get('COLOR_RESEARCH_ENV')
    if not path:raise ValueError('Set COLOR_RESEARCH_ENV to an explicit ColorEngineHomeEnvironmentV1 JSON')
    obj=json.loads(Path(path).read_text(encoding='utf-8-sig'))
    if obj.get('version')!='ColorEngineHomeEnvironmentV1':raise ValueError('Unsupported host environment version')
    root=Path(obj['authorizedOutputRoot']).resolve();source=Path(obj['sourceRoot']).resolve()
    if root==source or root.is_relative_to(source) or source.is_relative_to(root):raise ValueError('Research/source overlap')
    if obj.get('caps')!={'deviceMiB':256,'stagingMiB':64,'callerMiB':512,'freeReserveMiB':256}:raise ValueError('Frozen caps changed')
    file=Path(obj['sourceFile']).resolve()
    if not file.is_relative_to(source) or obj.get('sourceSha256')!='B7D358517DB0CD5B5C54215AA089A5A30C79234AEF50E9381931128493E33401':raise ValueError('Frozen source or authorized source root mismatch')
    for key in ['researchRoot','streamLibrary','wholeLibrary','resources','preparationRoot','smallRoot']:
        value=Path(obj[key]).resolve()
        if key=='researchRoot':
            if value!=root:raise ValueError('Research roots differ')
        elif not value.is_relative_to(root):raise ValueError('Host path outside research root: '+key)
    return obj
