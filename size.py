import os, re

ROOT = 'src'

def is_source(filename: str) -> bool:
    return filename.endswith('.cs') and not filename.endswith('GlobalUsings.cs')
    
def is_source_dir(dirname: str) -> bool:
    dirs = re.split('/|\\\\', dirname)
    return 'bin' not in dirs and 'obj' not in dirs

# -------------------------------------------------

def human_readable(num, suffix="B"):
    for unit in ("", "Ki", "Mi", "Gi", "Ti", "Pi", "Ei", "Zi"):
        if abs(num) < 1024.0:
            return f"{num:3.1f} {unit}{suffix}"
        num /= 1024.0
    return f"{num:.1f} Yi{suffix}"

total_lines = 0
total_size = 0

def process_file(file: str, dir: str):
    global total_lines, total_size

    if not is_source(file):
        return

    path = os.path.join(dir, file)

    print(path)

    total_size += os.path.getsize(path)
    
    with open(path, 'r') as f:
        lines = f.readlines()
        total_lines += len(lines)

for subdir, dirs, files in os.walk(ROOT):
    if not is_source_dir(subdir):
        continue

    for file in files:
        process_file(file, subdir)

print()
print(f"{total_lines} lines")
print(f"{human_readable(total_size)}")
