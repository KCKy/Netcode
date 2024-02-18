import os, re

ROOT = 'src'

def is_source(filename: str) -> bool:
    return filename.endswith('.cs') and not filename.endswith('GlobalUsings.cs')
    
def is_source_dir(dirname: str) -> bool:
    dirs = re.split('/|\\\\', dirname)
    return 'bin' not in dirs and 'obj' not in dirs and '.vs' not in dirs

# -------------------------------------------------

def human_readable(num: str, suffix: str = "B") -> str:
    for unit in ("", "Ki", "Mi", "Gi", "Ti", "Pi", "Ei", "Zi"):
        if abs(num) < 1024.0:
            return f"{num:3.1f} {unit}{suffix}"
        num /= 1024.0
    return f"{num:.1f} Yi{suffix}"

total_lines = 0
total_size = 0
total_codelines = 0

def file_stats(file: str, dir: str) -> tuple[int, int, int]:
    global total_lines, total_size

    if not is_source(file):
        return (None, None, None)

    path = os.path.join(dir, file)

    size = os.path.getsize(path)
    total_size += size
    
    with open(path, 'r') as f:
        lines = f.readlines()

        code_lines = 0
        for line in lines:
            code_lines += 0 if line.strip().startswith(("//")) else 1
        
        return (size, len(lines), code_lines)

for subdir, dirs, files in os.walk(ROOT):
    if not is_source_dir(subdir) or len(files) == 0:
        continue

    dir_lines = 0
    dir_size = 0
    dir_codelines = 0
    
    file_names = []

    for file in files:
        (size, lines, code_lines) = file_stats(file, subdir)
        if size is None or size <= 0:
            continue

        file_names.append(file)
        dir_lines += lines
        dir_size += size
        dir_codelines += code_lines

    total_lines += dir_lines
    total_size += dir_size
    total_codelines += dir_codelines

    if dir_lines > 0:
        print(subdir)
        print(f"    {", ".join(file_names)}")
        print(f"    {dir_lines} lines")
        print(f"    {dir_codelines} code lines")
        print(f"    {human_readable(dir_size)}")

print()
print(f"{total_lines} lines")
print(f"{total_codelines} code lines")
print(f"{human_readable(total_size)}")
print(f"{round(total_codelines / total_lines * 100, 1)} % of code")
