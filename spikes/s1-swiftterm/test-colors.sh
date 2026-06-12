#!/bin/bash
# Color battery test script for SwiftTerm validation

set -e

echo "=== SwiftTerm Color Battery Test ==="
echo

# Test 1: ANSI 16 colors
echo "Test 1: ANSI-16 Colors (foreground)"
for i in {30..37}; do
    echo -en "\e[${i}m███\e[0m "
done
echo
for i in {90..97}; do
    echo -en "\e[${i}m███\e[0m "
done
echo
echo

# Test 2: ANSI backgrounds
echo "Test 2: ANSI-16 Colors (background)"
for i in {40..47}; do
    echo -en "\e[${i}m   \e[0m "
done
echo
for i in {100..107}; do
    echo -en "\e[${i}m   \e[0m "
done
echo
echo

# Test 3: 256-color cube
echo "Test 3: 256-color palette (sample)"
for i in {16..51}; do
    echo -en "\e[38;5;${i}m█\e[0m"
    [ $(( ($i - 15) % 6 )) -eq 0 ] && echo
done
echo

# Test 4: Grayscale ramp
echo "Test 4: 256-color grayscale ramp"
for i in {232..255}; do
    echo -en "\e[38;5;${i}m█"
done
echo -e "\e[0m"
echo

# Test 5: Truecolor gradient
echo "Test 5: 24-bit truecolor gradient"
for i in {0..255..4}; do
    echo -en "\e[38;2;${i};100;$((255-i))m█"
done
echo -e "\e[0m"
echo

# Test 6: Text attributes
echo "Test 6: Text attributes"
echo -e "\e[1mBold\e[0m"
echo -e "\e[3mItalic\e[0m"
echo -e "\e[4mUnderline\e[0m"
echo -e "\e[9mStrikethrough\e[0m"
echo -e "\e[2mDim/Faint\e[0m"
echo -e "\e[7mInverse\e[0m"
echo

# Test 7: Combined attributes
echo "Test 7: Combined attributes"
echo -e "\e[1;31mBold Red\e[0m"
echo -e "\e[3;34mItalic Blue\e[0m"
echo -e "\e[4;32;48;5;235mUnderline Green on Gray\e[0m"
echo

# Test 8: Underline styles (if supported)
echo "Test 8: Underline styles"
echo -e "\e[4:1mSingle underline\e[0m"
echo -e "\e[4:2mDouble underline\e[0m"
echo -e "\e[4:3mCurly underline\e[0m"
echo

# Test 9: ls colors
echo "Test 9: ls with colors"
ls -G --color=auto /usr/bin | head -10
echo

echo "=== Color battery complete ==="
echo "Visual inspection required:"
echo "  - All ANSI-16 colors distinct and vivid"
echo "  - 256-color cube shows full spectrum"
echo "  - Truecolor gradient is smooth (no banding)"
echo "  - Text attributes render correctly"
echo "  - ls colors show correctly"
