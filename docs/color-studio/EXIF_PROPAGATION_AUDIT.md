# EXIF Propagation Audit

| Field | RAW decode | TIFF16 output | Status |
|---|---|---|---|
| Camera make/model | Read for many fixtures | Not preserved in corpus writer | PARTIAL |
| Lens | Not exposed by current acceptance decoder contract | Not preserved | NOT VERIFIED |
| ISO/shutter/aperture/focal length | Not exposed by current acceptance decoder contract | Not preserved | NOT VERIFIED |
| Capture date | Partial metadata read | Not preserved | PARTIAL |
| Orientation | Read and passed to TIFF writer | Requested, independent read-back not fully asserted | PARTIAL |
| Copyright/GPS | Not verified | Not verified | NOT VERIFIED |

Status: PARTIAL. EXIF propagation is separate from the numerical Match V4 closure.
