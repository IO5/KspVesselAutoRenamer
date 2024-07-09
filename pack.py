from zipfile import ZipFile, ZIP_DEFLATED
from pathlib import Path, PureWindowsPath, PurePath

self_path = Path(__file__).resolve()
dir_path = self_path.parent
zip_path = dir_path / "VesselAutoRenamer.zip"

ignore = [
    "*.pdb",
]

def walk(path): 
    for p in path.iterdir(): 
        rel = p.relative_to(dir_path)
        if any(PureWindowsPath(rel).match(ign) for ign in ignore):
            continue

        if p.is_dir(): 
            yield from walk(p)
            continue
        yield rel

if zip_path.exists():
    zip_path.unlink(True)

with ZipFile(zip_path, "x", compression=ZIP_DEFLATED) as zip:
    for path in walk(Path("GameData").absolute()):
        print(path)
        zip.write(dir_path / path, path)
