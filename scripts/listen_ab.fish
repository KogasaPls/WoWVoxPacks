#!/usr/bin/env fish
# Play the committed recording then the working-tree one for every rebuilt sound.
#   listen_ab.fish [voice] [name-filter]

set -l repo (realpath (status dirname)/..)
set -l voice Neural2_C
set -l filter ''
test (count $argv) -ge 1; and set voice $argv[1]
test (count $argv) -ge 2; and set filter $argv[2]

set -l changed (git -C $repo diff --name-only -- "output/$voice/*.ogg")
if test (count $changed) -eq 0
    echo "nothing rebuilt for $voice"
    exit 1
end

set -l old_dir (mktemp -d)
for path in $changed
    set -l file (path basename $path)
    set -l manifest (path dirname (path dirname $path))/SoundFiles.json
    set -l name (python3 -c "
import json,sys
for e in json.load(open(sys.argv[1])):
    if e['FileName'] == sys.argv[2]:
        print(e['DisplayName']); break
" $repo/$manifest $file)
    test -n "$filter"; and not string match -qi "*$filter*" $name; and continue

    git -C $repo show HEAD:$path > $old_dir/$file; or continue
    echo "$name ($voice, $file)"
    echo "  before"; pw-play $old_dir/$file
    sleep 0.4
    echo "  after "; pw-play $repo/$path
    sleep 0.6
end
rm -rf $old_dir
