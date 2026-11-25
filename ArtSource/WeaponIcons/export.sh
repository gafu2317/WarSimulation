#!/bin/sh
set -eu

source_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
output_dir="$source_dir/../../Assets/Images/Icons"

export_icon() {
    magick "$source_dir/WeaponCategories.png" -crop "496x496+$2+$3" +repage \
        -gravity center -background none -extent 512x512 "PNG32:$output_dir/$1.png"
}

export_icon sword 13 2
export_icon shield 516 2
export_icon staff 1016 2
export_icon grimoire 15 503
export_icon holybook 514 503
export_icon rosary 1010 503
