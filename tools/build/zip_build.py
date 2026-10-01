"""Packs an exported build folder into one zip for sending, keeping programs runnable on Linux/macOS.

    python tools/build/zip_build.py <folder> <zip> [--exec NAME ...] [--add FILE ...]

Files named with --exec get Unix mode 755 (made on "Unix", so unzip and Finder honour it); everything
else 644. If <zip> already exists (Godot's macOS export), --add files are appended and the rest is kept.
"""
import os
import sys
import zipfile


def main(argv):
    folder, target = argv[0], argv[1]
    executables, extra = set(), []
    mode = None
    for arg in argv[2:]:
        if arg in ("--exec", "--add"):
            mode = arg
        elif mode == "--exec":
            executables.add(arg)
        elif mode == "--add":
            extra.append(arg)

    def write(z, path, name):
        info = zipfile.ZipInfo.from_file(path, name)
        info.create_system = 3  # Unix, so the mode below counts
        info.external_attr = (0o100755 if os.path.basename(name) in executables else 0o100644) << 16
        info.compress_type = zipfile.ZIP_DEFLATED
        with open(path, "rb") as f:
            z.writestr(info, f.read())

    if os.path.exists(target):
        with zipfile.ZipFile(target, "a") as z:
            for path in extra:
                write(z, path, os.path.basename(path))
        return
    with zipfile.ZipFile(target, "w") as z:
        for base, _, files in os.walk(folder):
            for file in sorted(files):
                path = os.path.join(base, file)
                write(z, path, os.path.relpath(path, folder).replace(os.sep, "/"))
        for path in extra:
            write(z, path, os.path.basename(path))


if __name__ == "__main__":
    main(sys.argv[1:])
