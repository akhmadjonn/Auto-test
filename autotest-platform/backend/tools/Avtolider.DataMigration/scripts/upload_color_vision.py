"""
Upload Ishihara color vision test plate images to MinIO.

Usage:
  1. Create folder: data/visual_assets/color_vision/
  2. Place 12 images named: plate-01.webp through plate-12.webp
     (or .png/.jpg — they'll be converted to webp naming in MinIO)
  3. Run: python scripts/upload_color_vision.py

Expected answers per plate (from ColorVisionPlates.cs):
  plate-01: 12    plate-07: 74
  plate-02: 8     plate-08: 6
  plate-03: 29    plate-09: 45
  plate-04: 5     plate-10: 5
  plate-05: 3     plate-11: 7
  plate-06: 15    plate-12: 16

You can find free Ishihara test plate images online.
Search for "Ishihara color test plates" and download 12 standard plates.
"""

import os
import sys

try:
    import boto3
    from botocore.client import Config
except ImportError:
    print("Install boto3: pip install boto3")
    sys.exit(1)

MINIO_ENDPOINT = os.getenv("MINIO_ENDPOINT", "http://localhost:9000")
MINIO_ACCESS_KEY = os.getenv("MINIO_ACCESS_KEY", "minioadmin")
MINIO_SECRET_KEY = os.getenv("MINIO_SECRET_KEY", "minioadmin")
BUCKET = os.getenv("MINIO_BUCKET", "autotest-images")

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.path.join(SCRIPT_DIR, "..", "data", "visual_assets", "color_vision")

PLATES = [f"plate-{i:02d}" for i in range(1, 13)]
IMAGE_EXTS = [".webp", ".png", ".jpg", ".jpeg"]


def main():
    if not os.path.isdir(DATA_DIR):
        print(f"Directory not found: {DATA_DIR}")
        print(f"Create it and place 12 plate images (plate-01.webp ... plate-12.webp)")
        print(f"Or .png/.jpg files with the same naming.")
        sys.exit(1)

    s3 = boto3.client(
        "s3",
        endpoint_url=MINIO_ENDPOINT,
        aws_access_key_id=MINIO_ACCESS_KEY,
        aws_secret_access_key=MINIO_SECRET_KEY,
        config=Config(signature_version="s3v4"),
        region_name="us-east-1",
    )

    # Ensure bucket exists
    try:
        s3.head_bucket(Bucket=BUCKET)
    except Exception:
        print(f"Bucket '{BUCKET}' not found. Creating...")
        s3.create_bucket(Bucket=BUCKET)

    uploaded = 0
    missing = []

    for plate_name in PLATES:
        # Find the image file (try all extensions)
        found = None
        for ext in IMAGE_EXTS:
            path = os.path.join(DATA_DIR, f"{plate_name}{ext}")
            if os.path.isfile(path):
                found = path
                break

        if not found:
            missing.append(plate_name)
            continue

        # Upload with the exact key expected by the backend
        key = f"color-vision/{plate_name}.webp"
        content_type = "image/webp"
        if found.endswith(".png"):
            content_type = "image/png"
        elif found.endswith((".jpg", ".jpeg")):
            content_type = "image/jpeg"

        print(f"  Uploading {os.path.basename(found)} -> {key}")
        s3.upload_file(
            found, BUCKET, key,
            ExtraArgs={"ContentType": content_type}
        )
        uploaded += 1

    print()
    print(f"Uploaded: {uploaded}/12")
    if missing:
        print(f"Missing:  {', '.join(missing)}")
        print(f"Place missing images in: {DATA_DIR}")
    else:
        print("All 12 plates uploaded successfully!")


if __name__ == "__main__":
    main()
