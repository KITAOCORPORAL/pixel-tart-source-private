# Frozen color review viewer

Only prepares a view of already saved PNGs and JSON. It does not import a RAW decoder, run the candidate, change exposure, interpolate source files, or regenerate missing panels. Photos must stay local and are not in the Git delivery or review ZIP.

Use an ordinary Python interpreter (standard library only):

python tools/ColorResearchReview20261117/prepare_viewer.py --evidence docs/research/color-proposals-20261117 --output NEW_LOCAL_VIEWER_ROOT
python tools/ColorResearchReview20261117/serve_local.py --root NEW_LOCAL_VIEWER_ROOT --port 0 --receipt NEW_SERVER_RECEIPT.json

The first command requires all34 exact PNGs from their recorded original locations:18 highlightexisting panels,1 numeric curve,15 frozen XTrans components. It verifies SHA-256 before copying and refuses existing output roots. Missing files fail explicitly; update a local mapping only after deliberate relocation and retained hashes, not by changing pixels or filling an unavailable result. Candidate native files and photographic source TIFFs are not opened.

Open the loopback URL printed by the second command. Same-exposure and fixed midtone-match conditions are recorded in JSON; Canon's unavailable anchors stay unavailable. The native-device mode divides displayed CSS size by actual browserDPR for nominal1imagepixel=1devicepixel; CSS mode and fit-page are separate. Browser/OS scaling and display characterization remain limits, not physical calibration or16-bit acceptance. Native mode changes layout only; it is not a new rendering algorithm.

The first generated viewer used the bundled Python/Pillow/NumPy only to verify that4 pre-existing montages contain the pinned256×256 component pixels. That check did not render new outputs. The fifth old montage is absent; all3 frozen component PNGs exist and are shown directly. The static replay scripts here need no Pillow or NumPy.

Source images, anonymous sharpening ratings/mappings, GPL sources and reference binaries are deliberately not included. See the research HANDOFF, MATERIAL_GAPS and APPROVAL_AND_STOP_BOUNDARIES before any next algorithm or port action.
