#!/bin/bash

MATCH_START='^[^\[]*'
REST='.*$'
FIXED_CAPT='-?[0-9]+\.[0-9]+'

DELTA_CAPT="\[INF\] The delta for this frame has been ($FIXED_CAPT)\."
REGEX="$MATCH_START$DELTA_CAPT$REST"

echo 'X;Y'
grep --only-matching -E "$REGEX" "$1" | sed -E "s/$REGEX/;\1/g" | nl -w1 --number-separator ''
