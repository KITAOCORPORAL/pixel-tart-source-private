"""Copy the canonical argument-safe prepare launcher into a completed developer package."""
from pathlib import Path
import shutil,sys
package=Path(sys.argv[1]).resolve()
template=Path(__file__).resolve().with_name("Prepare-Guangjing-Capture.cmd")
assert (package/"native-host/Guangjing.Capture.NativeHost.exe").is_file()
shutil.copy2(template,package/template.name)
print("Installed canonical --prepare launcher without an explicit package argument")
