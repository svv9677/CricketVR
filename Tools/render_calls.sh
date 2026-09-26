#!/bin/sh
# Render the fielders' calls (FieldingCalls) with the macOS voices. Run from the repo root.
set -e
out=Assets/Resources/Sounds/Calls
tmp=$(mktemp -d)
mkdir -p "$out"
render() {  # name voice text
    say -v "$2" -r 230 -o "$tmp/$1.aiff" "$3"
    afconvert -f WAVE -d LEI16@22050 -c 1 "$tmp/$1.aiff" "$out/$1.wav"
    echo "$out/$1.wav ($2): $3"
}
render mine_1 "Daniel" "Mine!"
render mine_2 "Aman" "Mine!"
render mine_3 "Eddy (English (UK))" "Mine, mine!"
render keeper_1 "Daniel" "Keeper's!"
render keeper_2 "Aman" "Yes, keeper's!"
