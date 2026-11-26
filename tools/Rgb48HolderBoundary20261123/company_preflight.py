"""Bind inputs for a new company observation. Never repair a historical LAUNCH."""
from dataclasses import dataclass
import hashlib
import json
from pathlib import Path
import struct


@dataclass(frozen=True)
class RunPins:
    launch: str
    app: str
    core: str
    payload: str
    tiff: str
    icc: str
    icc_bytes: int
    width: int
    height: int


# Independent byte digests observed on company hardware (914ee867, COMPANY_PREFLIGHT_20261126).
# No CLI, task, sidecar or environment setting may supply replacement pins.
COMPANY_63A = RunPins(
    launch='518b78bacca0059cfc4fcfd22d4b11e6daa6158f34dc0875b05c9f434fd62d11',
    app='80f8ab6cbc562c7c479fc4d9fe0af0d422e07a4b137de21b609710a5df4f59f0',
    core='8cf99b0750314cbde3ebdd03d3b7505b3d24e6fac76667172ac4bc91c6ad9187',
    payload='0806d680acc74facd093dd2280e1c1a82cd18e40d0c27823d8c6c87cc7e3fc7e',
    tiff='12b98366044307542befef302625bf654ace400937e979bb03f0d8e2e2ff3034',
    icc='2b3aa1645779a9e634744faf9b01e9102b0c9b88fd6deced7934df86b949af7e',
    icc_bytes=3144, width=7752, height=5184,
)


class PreflightError(ValueError):
    pass


def require(ok, reason):
    if not ok:
        raise PreflightError(reason)


def check_hash(actual, expected, label):
    require(isinstance(expected, str) and actual == expected.lower(), label + ' SHA-256 mismatch or missing')


def file_sha256(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def first_ifd_icc(stream, expected_length):
    """Read only the classic TIFF first IFD and its one UNDEFINED (type 7) ICC tag."""
    def read_at(offset, size):
        stream.seek(offset)
        data = stream.read(size)
        require(len(data) == size, 'Truncated TIFF first IFD or ICC')
        return data

    head = read_at(0, 8)
    order = {b'II': '<', b'MM': '>'}.get(head[:2])
    require(order is not None, 'Unknown TIFF byte order')
    magic, offset = struct.unpack(order + 'HI', head[2:])
    require(magic == 42 and offset >= 8, 'Expected classic TIFF first IFD')
    count, = struct.unpack(order + 'H', read_at(offset, 2))
    entries = read_at(offset + 2, 12 * count)
    tags = [(kind, size, value) for tag, kind, size, value in struct.iter_unpack(order + 'HHI4s', entries) if tag == 34675]
    require(len(tags) == 1, 'Need exactly one ICC tag 34675 in the first IFD')
    kind, size, value = tags[0]
    require(kind == 7 and size == expected_length, 'ICC tag 34675 type or length mismatch')
    return value[:size] if size <= 4 else read_at(struct.unpack(order + 'I', value)[0], size)


def same_run_export(run, pins, current_icc):
    directory = run / 'export-first'
    images = [p for p in directory.iterdir() if p.suffix.lower() in ('.tif', '.tiff') and p.is_file()]
    require(len(images) == 1, 'Need one original TIFF in this LAUNCH export-first directory')
    image = images[0]
    sidecar_bytes = image.with_name(image.name + '.kitao-export.json').read_bytes()
    sidecar = json.loads(sidecar_bytes)
    require(sidecar.get('CompletionState') == 'Completed' and
            Path(sidecar.get('OutputPath') or '').resolve() == image.resolve(),
            'Original completed sidecar must name this first export')
    with image.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
        check_hash(digest, pins.tiff, 'Pinned original first TIFF')
        check_hash(digest, sidecar.get('OutputHash'), 'Original sidecar OutputHash')
        embedded = first_ifd_icc(stream, pins.icc_bytes)
    embedded_sha = hashlib.sha256(embedded).hexdigest()
    check_hash(embedded_sha, pins.icc, 'Original first TIFF embedded ICC')
    require(embedded == current_icc, 'Current designated ICC differs from the embedded ICC')
    return {'tiffSha256': digest, 'sidecarOutputHash': sidecar['OutputHash'].lower(),
            'sidecarSha256': hashlib.sha256(sidecar_bytes).hexdigest(), 'ifd': 0, 'tag': 34675,
            'type': 7, 'embeddedIccBytes': len(embedded), 'embeddedIccSha256': embedded_sha}


@dataclass
class VerifiedInputs:
    app: Path
    task: dict
    icc: bytes
    receipt: dict

    def task_for_observation(self, new_icc):
        # Give the new task the exact bytes checked, not a mutable path reopened after the check.
        with new_icc.open('xb') as stream:
            stream.write(self.icc)
        return {**self.task, 'icc': str(new_icc.resolve()),
                'observationIccSha256': self.receipt['iccSha256'], 'observationIccBinding': self.receipt['iccBinding'],
                'originalLaunchSha256': self.receipt['launchFileSha256']}


def verify_inputs(launch_path, product_app=None, *, require_original=False):
    pins = COMPANY_63A
    raw = launch_path.read_bytes()
    launch_sha = hashlib.sha256(raw).hexdigest()
    launch = json.loads(raw)
    task = launch['task']
    declared = 'iccSha256' in task  # Present but null/empty/malformed is a mismatch, never a fallback.
    original_only = not declared or require_original
    if original_only:
        check_hash(launch_sha, pins.launch, 'Original 63a LAUNCH bytes')
    require(launch['scenario'] == 'points', 'Expected processed points baseline')
    if original_only:
        require((task['width'], task['height'], task['orientation']) == (pins.width, pins.height, 1),
                'Expected authorized upright RGB48 dimensions')
    icc_path = Path(task['icc'])
    if declared:
        # Reject a mistakenly designated large file by streaming, before taking the ICC snapshot.
        check_hash(file_sha256(icc_path), task['iccSha256'], 'Current designated ICC')
    else:
        require(icc_path.stat().st_size == pins.icc_bytes, 'Current designated ICC length mismatch')
    icc = icc_path.read_bytes()
    icc_hash = hashlib.sha256(icc).hexdigest()
    check_hash(icc_hash, task['iccSha256'] if declared else pins.icc, 'Current designated ICC')

    app = (product_app or Path(launch['stage'])).resolve()
    app_hash = file_sha256(app / 'KitaoPhotoSelector.dll')
    core_hash = file_sha256(app / 'RAWSelectionAssistant.Core.dll')
    for actual, field, pin in [(app_hash, 'appSha256', pins.app), (core_hash, 'coreSha256', pins.core)]:
        check_hash(actual, launch[field], 'LAUNCH ' + field)
        if original_only:
            check_hash(actual, pin, 'Company ' + field)
    payload = Path(task['payload'])
    size = payload.stat().st_size
    require(size == task['width'] * task['height'] * 6, 'RGB48 payload length mismatch')
    payload_hash = file_sha256(payload)
    check_hash(payload_hash, task['pixelSha256'], 'LAUNCH RGB48 payload')
    if original_only:
        check_hash(payload_hash, pins.payload, 'Authorized company RGB48 payload')

    evidence = None if declared else same_run_export(launch_path.parent, pins, icc)
    receipt = {'classification': 'INPUT_PREFLIGHT_ONLY',
               'iccBinding': 'LAUNCH_TASK_SHA256' if declared else 'CURRENT_INPUT_FIXED_BY_SAME_RUN_EXPORT',
               'launchFileSha256': launch_sha, 'historicalTaskHasIccSha256': declared,
               'appSha256': app_hash, 'coreSha256': core_hash, 'payloadBytes': size, 'payloadSha256': payload_hash,
               'iccBytes': len(icc), 'iccSha256': icc_hash, 'sameRunFirstExport': evidence,
               'historicalIccFileReadProven': False, 'historicalInputsFullyReproduced': False,
               'largeImageReopenPassed': False,
               'scope': 'Binds inputs for a new observation only; cannot identify the ICC file read by the old host. No resource verdict.'}
    return VerifiedInputs(app, task, icc, receipt)


def observation_task(launch, stage, output):
    """Final check after staging; only a fresh attempt gets a private ICC copy."""
    previous = json.loads((output / 'INPUT_PREFLIGHT.json').read_bytes())
    try:
        require(previous['status'] == 'INPUTS_VERIFIED_BEFORE_HOST_BUILD', 'Missing successful company input preflight')
        verified = verify_inputs(launch, stage)
        require(verified.receipt == previous['input'], 'Company evidence changed since preflight')
        task = verified.task_for_observation(output / 'observation-input.icc')
        return task, verified.receipt
    except (OSError, ValueError, KeyError, TypeError) as error:
        with (output / 'PRELAUNCH_STOP.json').open('x', encoding='utf-8') as file:
            json.dump({'status': 'STOP_BEFORE_TARGET', 'reason': str(error), 'targetStarted': False,
                       'historicalIccFileReadProven': False, 'resourceAdmission': 'NOT_TESTED'}, file, indent=2)
        raise
