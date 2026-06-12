#!/usr/bin/env python3
"""
YOLOTerm Color Golden Corpus Generator

Generates comprehensive test cases for ANSI/256/truecolor rendering per SPEC §5.1.
Output format: JSON fixtures with raw escape sequences and expected cell grids.

Each fixture includes:
  - name: human-readable test case description
  - input_b64: base64-encoded raw bytes (ANSI escapes preserved)
  - grid: expected 80x24 cell grid (or smaller region)
    - Each cell: {ch: "char", fg: "#rrggbb", bg: "#rrggbb", attrs: ["bold", ...]}

Usage:
  python3 generate-corpus.py > fixtures.json
"""

import base64
import json
from typing import List, Dict, Any

# Standard xterm vivid ANSI-16 palette (from TermGrid themes.ts DEFAULT_ANSI)
ANSI_16_PALETTE = {
    "black": "#000000",
    "red": "#cd0000",
    "green": "#00cd00",
    "yellow": "#cdcd00",
    "blue": "#0000ee",
    "magenta": "#cd00cd",
    "cyan": "#00cdcd",
    "white": "#e5e5e5",
    "bright_black": "#7f7f7f",
    "bright_red": "#ff0000",
    "bright_green": "#00ff00",
    "bright_yellow": "#ffff00",
    "bright_blue": "#5c5cff",
    "bright_magenta": "#ff00ff",
    "bright_cyan": "#00ffff",
    "bright_white": "#ffffff",
}

# 256-color palette (xterm standard)
def color_256(n: int) -> str:
    """Convert 256-color index to hex RGB."""
    if n < 16:
        # Use ANSI-16 palette
        names = ["black", "red", "green", "yellow", "blue", "magenta", "cyan", "white",
                 "bright_black", "bright_red", "bright_green", "bright_yellow",
                 "bright_blue", "bright_magenta", "bright_cyan", "bright_white"]
        return ANSI_16_PALETTE[names[n]]
    elif n < 232:
        # 216-color cube: 16 + 36*r + 6*g + b (r,g,b in 0..5)
        n -= 16
        r = (n // 36) * 51  # 0, 51, 102, 153, 204, 255
        g = ((n // 6) % 6) * 51
        b = (n % 6) * 51
        return f"#{r:02x}{g:02x}{b:02x}"
    else:
        # Grayscale ramp: 232-255 (24 shades)
        gray = 8 + (n - 232) * 10
        return f"#{gray:02x}{gray:02x}{gray:02x}"


def create_fixture(name: str, input_bytes: bytes, grid: List[List[Dict]]) -> Dict[str, Any]:
    """Create a fixture object."""
    return {
        "name": name,
        "input_b64": base64.b64encode(input_bytes).decode('ascii'),
        "grid": grid
    }


def cell(ch: str, fg: str = None, bg: str = None, attrs: List[str] = None) -> Dict:
    """Create a cell object."""
    result = {"ch": ch}
    if fg:
        result["fg"] = fg
    if bg:
        result["bg"] = bg
    if attrs:
        result["attrs"] = attrs
    return result


# Generator functions for each test category

def gen_ansi16_foreground() -> Dict:
    """SGR 30-37, 90-97: ANSI-16 foreground colors."""
    # Input: 8 normal + 8 bright colors, each printing "█"
    seq = b""
    for code in [30, 31, 32, 33, 34, 35, 36, 37, 90, 91, 92, 93, 94, 95, 96, 97]:
        seq += f"\\x1b[{code}m█".encode('utf-8')
    seq += b"\\x1b[0m"  # reset
    
    # Expected: first row with 16 colored blocks
    colors = list(ANSI_16_PALETTE.values())
    grid = [[cell("█", fg=colors[i]) for i in range(16)]]
    
    return create_fixture("ANSI-16 foreground colors (SGR 30-37, 90-97)", seq, grid)


def gen_ansi16_background() -> Dict:
    """SGR 40-47, 100-107: ANSI-16 background colors."""
    seq = b""
    for code in [40, 41, 42, 43, 44, 45, 46, 47, 100, 101, 102, 103, 104, 105, 106, 107]:
        seq += f"\\x1b[{code}m ".encode('utf-8')
    seq += b"\\x1b[0m"
    
    colors = list(ANSI_16_PALETTE.values())
    grid = [[cell(" ", bg=colors[i]) for i in range(16)]]
    
    return create_fixture("ANSI-16 background colors (SGR 40-47, 100-107)", seq, grid)


def gen_256_sample() -> Dict:
    """SGR 38;5;N: 256-color palette sample (first 16)."""
    seq = b""
    for i in range(16):
        seq += f"\\x1b[38;5;{i}m█".encode('utf-8')
    seq += b"\\x1b[0m"
    
    grid = [[cell("█", fg=color_256(i)) for i in range(16)]]
    
    return create_fixture("256-color palette (first 16, SGR 38;5;N)", seq, grid)


def gen_256_grayscale() -> Dict:
    """256-color grayscale ramp (232-255)."""
    seq = b""
    for i in range(232, 256):
        seq += f"\\x1b[38;5;{i}m█".encode('utf-8')
    seq += b"\\x1b[0m"
    
    grid = [[cell("█", fg=color_256(i)) for i in range(232, 256)]]
    
    return create_fixture("256-color grayscale ramp (232-255)", seq, grid)


def gen_truecolor() -> Dict:
    """SGR 38;2;R;G;B: truecolor red gradient."""
    seq = b""
    colors_hex = []
    for r in range(0, 256, 16):  # 16 steps
        seq += f"\\x1b[38;2;{r};0;0m█".encode('utf-8')
        colors_hex.append(f"#{r:02x}0000")
    seq += b"\\x1b[0m"
    
    grid = [[cell("█", fg=c) for c in colors_hex]]
    
    return create_fixture("Truecolor red gradient (SGR 38;2;R;G;B)", seq, grid)


def gen_bold() -> Dict:
    """SGR 1: bold attribute."""
    seq = b"\\x1b[1mBOLD\\x1b[0m"
    grid = [[cell("B", attrs=["bold"]), cell("O", attrs=["bold"]),
             cell("L", attrs=["bold"]), cell("D", attrs=["bold"])]]
    return create_fixture("Bold text (SGR 1)", seq, grid)


def gen_italic() -> Dict:
    """SGR 3: italic attribute."""
    seq = b"\\x1b[3mITALIC\\x1b[0m"
    grid = [[cell("I", attrs=["italic"]), cell("T", attrs=["italic"]),
             cell("A", attrs=["italic"]), cell("L", attrs=["italic"]),
             cell("I", attrs=["italic"]), cell("C", attrs=["italic"])]]
    return create_fixture("Italic text (SGR 3)", seq, grid)


def gen_underline() -> Dict:
    """SGR 4: underline attribute."""
    seq = b"\\x1b[4mUNDERLINE\\x1b[0m"
    grid = [[cell("U", attrs=["underline"]), cell("N", attrs=["underline"]),
             cell("D", attrs=["underline"]), cell("E", attrs=["underline"]),
             cell("R", attrs=["underline"]), cell("L", attrs=["underline"]),
             cell("I", attrs=["underline"]), cell("N", attrs=["underline"]),
             cell("E", attrs=["underline"])]]
    return create_fixture("Underline text (SGR 4)", seq, grid)


def gen_strikethrough() -> Dict:
    """SGR 9: strikethrough attribute."""
    seq = b"\\x1b[9mSTRIKE\\x1b[0m"
    grid = [[cell("S", attrs=["strikethrough"]), cell("T", attrs=["strikethrough"]),
             cell("R", attrs=["strikethrough"]), cell("I", attrs=["strikethrough"]),
             cell("K", attrs=["strikethrough"]), cell("E", attrs=["strikethrough"])]]
    return create_fixture("Strikethrough text (SGR 9)", seq, grid)


def gen_dim() -> Dict:
    """SGR 2: dim/faint attribute."""
    seq = b"\\x1b[2mDIM\\x1b[0m"
    grid = [[cell("D", attrs=["dim"]), cell("I", attrs=["dim"]), cell("M", attrs=["dim"])]]
    return create_fixture("Dim/faint text (SGR 2)", seq, grid)


def gen_inverse() -> Dict:
    """SGR 7: inverse attribute."""
    seq = b"\\x1b[7mINVERSE\\x1b[0m"
    grid = [[cell("I", attrs=["inverse"]), cell("N", attrs=["inverse"]),
             cell("V", attrs=["inverse"]), cell("E", attrs=["inverse"]),
             cell("R", attrs=["inverse"]), cell("S", attrs=["inverse"]),
             cell("E", attrs=["inverse"])]]
    return create_fixture("Inverse text (SGR 7)", seq, grid)


def gen_combined_attrs() -> Dict:
    """Combined: bold + red foreground."""
    seq = b"\\x1b[1;31mBOLD RED\\x1b[0m"
    grid = [[cell("B", fg=ANSI_16_PALETTE["red"], attrs=["bold"]),
             cell("O", fg=ANSI_16_PALETTE["red"], attrs=["bold"]),
             cell("L", fg=ANSI_16_PALETTE["red"], attrs=["bold"]),
             cell("D", fg=ANSI_16_PALETTE["red"], attrs=["bold"]),
             cell(" ", fg=ANSI_16_PALETTE["red"], attrs=["bold"]),
             cell("R", fg=ANSI_16_PALETTE["red"], attrs=["bold"]),
             cell("E", fg=ANSI_16_PALETTE["red"], attrs=["bold"]),
             cell("D", fg=ANSI_16_PALETTE["red"], attrs=["bold"])]]
    return create_fixture("Bold + red foreground (SGR 1;31)", seq, grid)


def gen_bold_not_bright() -> Dict:
    """SGR 1 does NOT brighten colors (bold stays same color, just heavier weight)."""
    # Test: SGR 31m (red) vs SGR 1;31m (bold red) — should be same color
    seq = b"\\x1b[31mRED\\x1b[0m \\x1b[1;31mBOLD\\x1b[0m"
    red = ANSI_16_PALETTE["red"]
    grid = [[cell("R", fg=red), cell("E", fg=red), cell("D", fg=red),
             cell(" "),
             cell("B", fg=red, attrs=["bold"]), cell("O", fg=red, attrs=["bold"]),
             cell("L", fg=red, attrs=["bold"]), cell("D", fg=red, attrs=["bold"])]]
    return create_fixture("Bold does NOT brighten color (SGR 1;31 same hue as 31)", seq, grid)


# Main generator
def generate_corpus() -> List[Dict]:
    """Generate complete color corpus."""
    return [
        gen_ansi16_foreground(),
        gen_ansi16_background(),
        gen_256_sample(),
        gen_256_grayscale(),
        gen_truecolor(),
        gen_bold(),
        gen_italic(),
        gen_underline(),
        gen_strikethrough(),
        gen_dim(),
        gen_inverse(),
        gen_combined_attrs(),
        gen_bold_not_bright(),
    ]


if __name__ == "__main__":
    corpus = generate_corpus()
    print(json.dumps(corpus, indent=2))
