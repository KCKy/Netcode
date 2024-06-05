#!/bin/bash

MATCH_START='^[^\[]*'
REST='.*$'
ID="$1"
INT_CAPT='[0-9]+'
FIXED_CAPT='-?[0-9]+\.[0-9]+'

SOON_CAPT="\[VRB\] Input from $ID for frame ($INT_CAPT) received ($FIXED_CAPT) s in advance\."
LATE_CAPT="\[DBG\] Got late input from client $ID for ($INT_CAPT) at $INT_CAPT \(($FIXED_CAPT) s\)\."
SOON_REGEX="$MATCH_START$SOON_CAPT$REST"
LATE_REGEX="$MATCH_START$LATE_CAPT$REST"

echo 'X;Y'
grep --only-matching -E "$LATE_REGEX" "$2" | sed -E "s/$LATE_REGEX/\1;\2/g" 
grep --only-matching -E "$SOON_REGEX" "$2" | sed -E "s/$SOON_REGEX/\1;\2/g" 
