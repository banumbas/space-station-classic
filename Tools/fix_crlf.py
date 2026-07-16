#!/usr/bin/env python3

import subprocess
import os

def get_text_files():
    process = subprocess.run(
        ["git", "grep", "--cached", "-Il", ""],
        check=True,
        encoding="utf-8",
        stdout=subprocess.PIPE)

    for x in process.stdout.splitlines():
        yield x.strip()

def fix_file(path: str):
    try:
        with open(path, "rb") as f:
            content = f.read()
            
        if not content:
            return
            
        # Fix CRLF
        content = content.replace(b"\r\n", b"\n")
        
        # Fix trailing whitespace
        lines = content.split(b"\n")
        fixed_lines = [line.rstrip(b" \t") for line in lines]
        
        # Join lines
        content = b"\n".join(fixed_lines)
        
        # Fix final newline
        if content and not content.endswith(b"\n"):
            content += b"\n"
            
        with open(path, "wb") as f:
            f.write(content)
            
    except Exception as e:
        print(f"Error fixing {path}: {e}")

def main():
    print("Fixing files...")
    count = 0
    for file_name in get_text_files():
        if os.path.exists(file_name):
            fix_file(file_name)
            count += 1
    print(f"Fixed {count} files.")

if __name__ == "__main__":
    main()
