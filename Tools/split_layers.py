#!/usr/bin/env python3
"""Split one RGBA image into N random cell layers."""
""" python split_layers.py my_art.png --layers 8 """

from PIL import Image
import numpy as np
import random
import os
import argparse


def split_into_layers(input_path, output_dir, num_layers=6, cell_size=1):
    if num_layers < 1:
        raise ValueError("num_layers must be at least 1")

    os.makedirs(output_dir, exist_ok=True)

    img = Image.open(input_path).convert("RGBA")
    img_array = np.array(img)
    layers = [np.zeros_like(img_array) for _ in range(num_layers)]

    height, width = img_array.shape[:2]
    last_layer = num_layers - 1

    for y in range(0, height, cell_size):
        for x in range(0, width, cell_size):
            cell_pixels = img_array[y : min(y + cell_size, height), x : min(x + cell_size, width)]
            if np.any(cell_pixels[:, :, 3] > 0):
                layer_index = random.randint(0, last_layer)
                layers[layer_index][y : min(y + cell_size, height), x : min(x + cell_size, width)] = cell_pixels

    for i, layer in enumerate(layers):
        Image.fromarray(layer).save(os.path.join(output_dir, f"layer_{i}.png"))


def main():
    parser = argparse.ArgumentParser(description="Split one image into random layers.")
    parser.add_argument("image", help="Path to input PNG (or any image PIL can open)")
    parser.add_argument(
        "-n", "--layers",
        type=int,
        default=6,
        metavar="N",
        help="Number of layers to split into (default: 6)",
    )
    parser.add_argument(
        "-o", "--output",
        default=None,
        help="Output folder (default: split_layers/<image_stem>/)",
    )
    parser.add_argument(
        "--cell-size",
        type=int,
        default=1,
        help="Cell size in pixels (default: 1)",
    )
    args = parser.parse_args()

    if args.layers < 1:
        raise SystemExit("--layers must be at least 1")

    input_path = os.path.abspath(args.image)
    if not os.path.isfile(input_path):
        raise SystemExit(f"File not found: {input_path}")

    if args.output:
        output_dir = os.path.abspath(args.output)
    else:
        stem = os.path.splitext(os.path.basename(input_path))[0]
        output_dir = os.path.join(os.path.dirname(input_path), "split_layers", stem)

    print(f"Input:  {input_path}")
    print(f"Layers: {args.layers}")
    print(f"Output: {output_dir}/")
    split_into_layers(input_path, output_dir, num_layers=args.layers, cell_size=args.cell_size)
    print(f"Done — wrote layer_0.png … layer_{args.layers - 1}.png")


if __name__ == "__main__":
    main()
