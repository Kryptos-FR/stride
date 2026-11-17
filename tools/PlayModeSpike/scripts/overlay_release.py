# Copies a game's (or the agent's) output folder and replaces each Stride.*.dll with the Release build of this checkout.
import os, shutil, subprocess, sys
src, dst, root = sys.argv[1], sys.argv[2], "/home/user/stride/sources"
if os.path.exists(dst):
    shutil.rmtree(dst)
shutil.copytree(src, dst, symlinks=True)
release = {}
for dirpath, dirnames, filenames in os.walk(root):
    dirnames[:] = [d for d in dirnames if d not in ("obj", "Debug", "Tests")]
    if "/bin/Release/net10.0" not in dirpath:
        continue
    vulkan = dirpath.endswith("/Vulkan")
    if not vulkan and dirpath.split("/bin/Release/net10.0")[1] not in ("",):
        continue
    for f in filenames:
        if f.startswith("Stride.") and f.endswith(".dll"):
            # Prefer the Vulkan copy of graphics-API-specific assemblies, else the copy in the project's own folder.
            own = os.path.basename(dirpath.split("/bin/Release")[0]) + ".dll" == f
            score = 2 if vulkan else (1 if own else 0)
            if f not in release or release[f][0] < score:
                release[f] = (score, os.path.join(dirpath, f))
replaced, kept = [], []
for f in sorted(os.listdir(dst)):
    if f.startswith("Stride.") and f.endswith(".dll") and not f.startswith("Stride.PlayMode"):
        if f in release:
            shutil.copy2(release[f][1], os.path.join(dst, f))
            pdb = release[f][1][:-4] + ".pdb"
            if os.path.exists(pdb):
                shutil.copy2(pdb, os.path.join(dst, f[:-4] + ".pdb"))
            replaced.append(f)
        else:
            kept.append(f)
print("replaced", len(replaced), "kept Debug:", kept)
