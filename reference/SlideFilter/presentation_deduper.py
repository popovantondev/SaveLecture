#!/usr/bin/env python3
"""Find visually duplicate presentation screenshots.

The script deliberately ignores the Teams chat, participant tiles and live
captions by comparing only the shared-content rectangle.  It is safe by
default: it writes a report/preview and never removes source images.  With
--move-rejected it moves rejected frames to a review folder.

Dependencies: Python 3.10+, Pillow, NumPy.
"""

from __future__ import annotations

import argparse
import csv
import math
import re
import shutil
import sys
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Iterable, Sequence

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont, ImageOps


IMAGE_EXTENSIONS = {".jpg", ".jpeg", ".png", ".webp", ".bmp"}
FILENAME_DATES = (
    re.compile(r"(?P<d>\d{4}-\d{2}-\d{2})_(?P<t>\d{2}-\d{2}-\d{2})"),
    re.compile(r"(?P<d>\d{4}-\d{2}-\d{2})[ _](?P<t>\d{6})"),
)


@dataclass
class Frame:
    path: Path
    timestamp: datetime
    crop_box: tuple[int, int, int, int]
    feature: np.ndarray
    sharpness: float
    content_score: float
    decision: str = ""
    reason: str = ""
    group: int = -1
    similarity: float | None = None
    best_shift: tuple[int, int] = (0, 0)


@dataclass
class Group:
    number: int
    frames: list[Frame] = field(default_factory=list)
    representative: Frame | None = None


def parse_timestamp(path: Path) -> datetime:
    name = path.stem
    for index, pattern in enumerate(FILENAME_DATES):
        match = pattern.search(name)
        if not match:
            continue
        raw_date = match.group("d")
        raw_time = match.group("t")
        fmt = "%Y-%m-%d_%H-%M-%S" if index == 0 else "%Y-%m-%d_%H%M%S"
        try:
            return datetime.strptime(f"{raw_date}_{raw_time}", fmt)
        except ValueError:
            pass
    return datetime.fromtimestamp(path.stat().st_mtime)


def parse_roi(value: str) -> tuple[float, float, float, float]:
    try:
        coords = tuple(float(item.strip()) for item in value.split(","))
    except ValueError as exc:
        raise argparse.ArgumentTypeError("ROI must contain four decimal numbers") from exc
    if len(coords) != 4:
        raise argparse.ArgumentTypeError("ROI must be x1,y1,x2,y2")
    x1, y1, x2, y2 = coords
    if not (0 <= x1 < x2 <= 1 and 0 <= y1 < y2 <= 1):
        raise argparse.ArgumentTypeError("ROI coordinates must be between 0 and 1")
    return coords


def choose_crop_box(
    image: Image.Image,
    teams_roi: tuple[float, float, float, float],
    full_screen_min_width: int,
) -> tuple[int, int, int, int]:
    width, height = image.size
    # The current automatic Teams captures are 1920x1080.  Older PNG files in
    # this archive are already cropped to the shared screen, so only trim their
    # browser/title/taskbar margins.
    if width >= full_screen_min_width and width / max(height, 1) >= 1.6:
        relative = teams_roi
    else:
        relative = (0.03, 0.035, 0.97, 0.94)
    x1, y1, x2, y2 = relative
    return (
        round(width * x1),
        round(height * y1),
        round(width * x2),
        round(height * y2),
    )


def build_feature(crop: Image.Image, size: tuple[int, int]) -> tuple[np.ndarray, float, float]:
    gray = ImageOps.grayscale(crop)
    gray = ImageEnhance.Contrast(gray).enhance(1.08)
    gray = gray.filter(ImageFilter.GaussianBlur(radius=0.6))
    gray = ImageOps.fit(gray, size, method=Image.Resampling.LANCZOS)
    array = np.asarray(gray, dtype=np.float32) / 255.0

    gx = np.diff(array, axis=1, append=array[:, -1:])
    gy = np.diff(array, axis=0, append=array[-1:, :])
    gradient = np.sqrt(gx * gx + gy * gy)
    sharpness = float(np.mean(gradient))

    # A nearly uniform rectangle is not a useful slide.
    # This score conservatively catches blank and stopped-sharing frames.
    tone_spread = float(np.percentile(array, 95) - np.percentile(array, 5))
    edge_density = float(np.mean(gradient > 0.055))
    content_score = min(1.0, tone_spread * 0.75 + edge_density * 1.7)
    return array, sharpness, content_score


def detect_non_presentation_screen(image: Image.Image) -> str | None:
    """Recognize common full-screen captures that are not a presentation.

    The thresholds are intentionally separated from the observed presentation
    range.  Smaller legacy images are already cropped captures and are not
    subjected to Teams-layout checks.
    """
    width, height = image.size
    if width < 1500 or width / max(height, 1) < 1.6:
        return None

    rgb = np.asarray(image, dtype=np.float32) / 255.0
    gray = (
        rgb[:, :, 0] * 0.299
        + rgb[:, :, 1] * 0.587
        + rgb[:, :, 2] * 0.114
    )

    # A captured Windows desktop has no Teams title/control strip.  In the
    # supplied desktop example this density is 0.001; every normal Teams frame
    # in the calibration set is above 0.095.
    header = gray[: max(1, round(height * 0.083)), :]
    header_gx = np.abs(np.diff(header, axis=1))
    header_gy = np.abs(np.diff(header, axis=0))
    header_edge_density = float(
        np.mean(header_gx > 0.03) + np.mean(header_gy > 0.03)
    )
    if header_edge_density < 0.035:
        return "desktop or non-Teams screen"

    # When sharing stops, the Teams stage becomes a grid of large pastel
    # participant tiles.  Measure the fraction of that characteristic color in
    # the stage, not in the chat/captions.  The supplied no-share frame scores
    # 0.517; the closest real presentation in the calibration set scores 0.362.
    stage = rgb[
        round(height * 0.09) : round(height * 0.69),
        round(width * 0.06) : round(width * 0.77),
    ]
    maximum = np.max(stage, axis=2)
    minimum = np.min(stage, axis=2)
    saturation = (maximum - minimum) / np.maximum(maximum, 0.001)
    pastel_fraction = float(
        np.mean(
            (maximum > 0.65)
            & (maximum < 0.99)
            & (saturation > 0.025)
            & (saturation < 0.22)
        )
    )
    colored_fraction = float(np.mean(saturation > 0.025))
    if pastel_fraction > 0.43 and colored_fraction > 0.53:
        return "Teams participant gallery; no active screen share"

    return None


def load_frame(
    path: Path,
    teams_roi: tuple[float, float, float, float],
    full_screen_min_width: int,
    feature_size: tuple[int, int],
) -> Frame:
    with Image.open(path) as source:
        image = ImageOps.exif_transpose(source).convert("RGB")
        non_presentation_reason = detect_non_presentation_screen(image)
        crop_box = choose_crop_box(image, teams_roi, full_screen_min_width)
        crop = image.crop(crop_box)
        feature, sharpness, content_score = build_feature(crop, feature_size)
    frame = Frame(
        path=path,
        timestamp=parse_timestamp(path),
        crop_box=crop_box,
        feature=feature,
        sharpness=sharpness,
        content_score=content_score,
    )
    if non_presentation_reason:
        frame.decision = "not_content"
        frame.reason = non_presentation_reason
    return frame


def overlap_for_shift(
    first: np.ndarray, second: np.ndarray, dx: int, dy: int
) -> tuple[np.ndarray, np.ndarray]:
    height, width = first.shape
    ax1, ax2 = max(0, dx), min(width, width + dx)
    ay1, ay2 = max(0, dy), min(height, height + dy)
    bx1, bx2 = max(0, -dx), min(width, width - dx)
    by1, by2 = max(0, -dy), min(height, height - dy)
    return first[ay1:ay2, ax1:ax2], second[by1:by2, bx1:bx2]


def safe_correlation(first: np.ndarray, second: np.ndarray) -> float:
    a = first.ravel()
    b = second.ravel()
    a = a - float(a.mean())
    b = b - float(b.mean())
    denominator = math.sqrt(float(np.dot(a, a)) * float(np.dot(b, b)))
    if denominator < 1e-9:
        return 1.0 if float(np.mean(np.abs(first - second))) < 0.01 else 0.0
    return float(np.dot(a, b) / denominator)


def gradient_map(array: np.ndarray) -> np.ndarray:
    gx = np.diff(array, axis=1, append=array[:, -1:])
    gy = np.diff(array, axis=0, append=array[-1:, :])
    return np.sqrt(gx * gx + gy * gy)


def comparison_score(first: np.ndarray, second: np.ndarray, max_shift: int) -> tuple[float, tuple[int, int]]:
    """Return a translation-tolerant visual similarity in the range 0..1."""
    best_score = -1.0
    best_shift = (0, 0)
    first_edges = gradient_map(first)
    second_edges = gradient_map(second)

    def evaluate(dx: int, dy: int) -> None:
        nonlocal best_score, best_shift
        a, b = overlap_for_shift(first, second, dx, dy)
        ae, be = overlap_for_shift(first_edges, second_edges, dx, dy)
        if a.size == 0:
            return

        gray_corr = max(-1.0, min(1.0, safe_correlation(a, b)))
        edge_corr = max(-1.0, min(1.0, safe_correlation(ae, be)))
        difference = np.abs(a - b)
        # Ignore the noisiest 12% of pixels.  A mouse pointer or a context
        # menu then cannot turn an unchanged slide into a new slide.
        robust_limit = float(np.percentile(difference, 88))
        robust_similarity = max(0.0, 1.0 - robust_limit / 0.34)

        score = (
            0.50 * ((gray_corr + 1.0) / 2.0)
            + 0.30 * ((edge_corr + 1.0) / 2.0)
            + 0.20 * robust_similarity
        )
        if score > best_score:
            best_score = score
            best_shift = (dx, dy)

    # Search coarsely over a larger range, then refine around the best offset.
    # This tolerates a small mouse-wheel scroll without making every comparison
    # quadratic in a large max_shift value.
    coarse_step = max(1, max_shift // 6)
    coarse_values = set(range(-max_shift, max_shift + 1, coarse_step))
    coarse_values.update({-max_shift, 0, max_shift})
    for dy in sorted(coarse_values):
        for dx in sorted(coarse_values):
            evaluate(dx, dy)
    coarse_best = best_shift
    if coarse_step > 1:
        for dy in range(
            max(-max_shift, coarse_best[1] - coarse_step + 1),
            min(max_shift, coarse_best[1] + coarse_step - 1) + 1,
        ):
            for dx in range(
                max(-max_shift, coarse_best[0] - coarse_step + 1),
                min(max_shift, coarse_best[0] + coarse_step - 1) + 1,
            ):
                evaluate(dx, dy)
    return max(0.0, min(1.0, best_score)), best_shift


def build_groups(
    frames: Sequence[Frame],
    threshold: float,
    transient_threshold: float,
    max_shift: int,
) -> list[Group]:
    """Group neighboring screenshots that show the same presentation content."""
    if not frames:
        return []

    valid = [frame for frame in frames if frame.decision != "not_content"]
    if not valid:
        return []

    groups: list[Group] = []
    index = 0
    while index < len(valid):
        anchor = valid[index]
        group = Group(number=len(groups) + 1, frames=[anchor], representative=anchor)
        anchor.group = group.number
        index += 1

        while index < len(valid):
            current = valid[index]
            score, shift = comparison_score(group.representative.feature, current.feature, max_shift)
            current.similarity = score
            current.best_shift = shift
            if score >= threshold:
                current.group = group.number
                group.frames.append(current)
                index += 1
                continue

            # A single transient frame often means a context menu, mouse-over
            # UI, or a momentary share interruption.  Compare the frames on
            # both sides before deciding it is a real new slide.
            if index + 1 < len(valid):
                following = valid[index + 1]
                bridge_score, _ = comparison_score(
                    group.representative.feature, following.feature, max_shift
                )
                current_to_anchor, _ = comparison_score(
                    group.representative.feature, current.feature, max_shift
                )
                if bridge_score >= threshold and current_to_anchor >= transient_threshold:
                    current.group = group.number
                    current.decision = "transient"
                    current.reason = "neighbors show the same slide"
                    group.frames.append(current)
                    following.group = group.number
                    following.similarity = bridge_score
                    group.frames.append(following)
                    index += 2
                    continue
            break

        groups.append(group)
    return groups


def choose_representatives(
    groups: Sequence[Group], max_shift: int, strategy: str = "latest"
) -> None:
    for group in groups:
        candidates = [frame for frame in group.frames if frame.decision != "transient"]
        if not candidates:
            candidates = list(group.frames)
        if len(candidates) == 1:
            representative = candidates[0]
        elif strategy == "latest":
            # PowerPoint often reveals a slide bullet by bullet.  The last
            # stable frame normally contains the most complete version.
            representative = candidates[-1]
        else:
            # The medoid is normally the clean frame, while a frame containing
            # a menu or tooltip is visually farther from the rest of the group.
            quality: list[tuple[float, Frame]] = []
            for frame in candidates:
                similarities = []
                for other in candidates:
                    if other is frame:
                        continue
                    score, _ = comparison_score(frame.feature, other.feature, max_shift)
                    similarities.append(score)
                centrality = float(np.mean(similarities)) if similarities else 1.0
                quality.append((centrality + min(frame.sharpness, 0.20) * 0.15, frame))
            representative = max(quality, key=lambda item: item[0])[1]
        group.representative = representative
        for frame in group.frames:
            if frame is representative:
                frame.decision = "keep"
                frame.reason = (
                    "latest stable frame in neighboring group"
                    if strategy == "latest"
                    else "visual medoid of neighboring group"
                )
            elif frame.decision == "transient":
                frame.decision = "reject"
            else:
                frame.decision = "reject"
                frame.reason = f"similar to kept frame {representative.path.name}"


def suppress_global_repeats(groups: Sequence[Group], threshold: float, max_shift: int) -> None:
    kept: list[Frame] = []
    for group in groups:
        representative = group.representative
        if representative is None or representative.decision != "keep":
            continue
        duplicate_of: Frame | None = None
        best_score = -1.0
        for earlier in kept:
            score, _ = comparison_score(earlier.feature, representative.feature, max_shift)
            if score > best_score:
                best_score = score
                duplicate_of = earlier
        if duplicate_of is not None and best_score >= threshold:
            representative.decision = "reject"
            representative.similarity = best_score
            representative.reason = f"repeats earlier slide {duplicate_of.path.name}"
        else:
            kept.append(representative)


def write_report(frames: Sequence[Frame], destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    with destination.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.writer(handle, delimiter=";")
        writer.writerow(
            [
                "file",
                "timestamp",
                "decision",
                "group",
                "similarity",
                "shift_x",
                "shift_y",
                "content_score",
                "reason",
            ]
        )
        for frame in frames:
            writer.writerow(
                [
                    frame.path.name,
                    frame.timestamp.isoformat(sep=" "),
                    frame.decision,
                    frame.group if frame.group >= 0 else "",
                    "" if frame.similarity is None else f"{frame.similarity:.4f}",
                    frame.best_shift[0],
                    frame.best_shift[1],
                    f"{frame.content_score:.4f}",
                    frame.reason,
                ]
            )


def thumbnail_for_frame(frame: Frame, size: tuple[int, int]) -> Image.Image:
    with Image.open(frame.path) as source:
        image = ImageOps.exif_transpose(source).convert("RGB")
        crop = image.crop(frame.crop_box)
        thumb = ImageOps.fit(crop, size, method=Image.Resampling.LANCZOS)
    return thumb


def write_contact_sheets(
    frames: Sequence[Frame], destination: Path, title: str, columns: int = 4, rows: int = 4
) -> list[Path]:
    destination.mkdir(parents=True, exist_ok=True)
    tile_size = (320, 190)
    label_height = 34
    margin = 12
    page_size = columns * rows
    outputs: list[Path] = []
    font = ImageFont.load_default(size=14)

    for page_index in range(0, len(frames), page_size):
        page_frames = frames[page_index : page_index + page_size]
        canvas = Image.new(
            "RGB",
            (
                columns * (tile_size[0] + margin) + margin,
                rows * (tile_size[1] + label_height + margin) + 42,
            ),
            "white",
        )
        draw = ImageDraw.Draw(canvas)
        draw.text((margin, 12), title, fill="black", font=font)
        for offset, frame in enumerate(page_frames):
            row, column = divmod(offset, columns)
            x = margin + column * (tile_size[0] + margin)
            y = 42 + row * (tile_size[1] + label_height + margin)
            canvas.paste(thumbnail_for_frame(frame, tile_size), (x, y))
            label = f"{frame.path.stem[-12:]}  {frame.decision}  G{frame.group}"
            draw.text((x, y + tile_size[1] + 4), label, fill="black", font=font)
        output = destination / f"{title.lower().replace(' ', '_')}_{page_index // page_size + 1:02d}.jpg"
        canvas.save(output, quality=90)
        outputs.append(output)
    return outputs


def move_rejected(frames: Iterable[Frame], destination: Path) -> int:
    destination.mkdir(parents=True, exist_ok=True)
    count = 0
    for frame in frames:
        if frame.decision not in {"reject", "not_content"}:
            continue
        target = destination / frame.path.name
        if target.exists():
            suffix_number = 2
            while target.exists():
                target = destination / (
                    f"{frame.path.stem}__{suffix_number}{frame.path.suffix}"
                )
                suffix_number += 1
        shutil.move(str(frame.path), str(target))
        count += 1
    return count


def find_images(folder: Path) -> list[Path]:
    return sorted(
        (
            path
            for path in folder.iterdir()
            if path.is_file() and path.suffix.lower() in IMAGE_EXTENSIONS
        ),
        key=lambda path: (parse_timestamp(path), path.name.lower()),
    )


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Find duplicate presentation screenshots while ignoring Teams chat/captions."
    )
    parser.add_argument("folder", type=Path, help="Media folder")
    parser.add_argument(
        "--roi",
        type=parse_roi,
        default=(0.08, 0.09, 0.625, 0.69),
        help="Teams presentation rectangle as x1,y1,x2,y2 fractions",
    )
    parser.add_argument(
        "--full-screen-min-width",
        type=int,
        default=1500,
        help="Images at least this wide are treated as full Teams screenshots",
    )
    parser.add_argument(
        "--threshold",
        type=float,
        default=0.905,
        help="Neighbor similarity threshold (default: 0.905)",
    )
    parser.add_argument(
        "--global-threshold",
        type=float,
        default=0.935,
        help="Similarity threshold for --global-unique (default: 0.935)",
    )
    parser.add_argument(
        "--transient-threshold",
        type=float,
        default=0.82,
        help="Minimum similarity for a one-frame menu/overlay (default: 0.82)",
    )
    parser.add_argument(
        "--max-shift",
        type=int,
        default=12,
        help="Alignment tolerance after downscaling (default: 12 pixels)",
    )
    parser.add_argument(
        "--min-content-score",
        type=float,
        default=0.10,
        help="Below this value a frame is treated as blank/no content",
    )
    parser.add_argument(
        "--global-unique",
        action="store_true",
        help="Also reject a slide if it repeats much later",
    )
    parser.add_argument(
        "--representative",
        choices=("latest", "medoid"),
        default="latest",
        help="Frame kept from a duplicate group (default: latest)",
    )
    parser.add_argument(
        "--exclude-reference",
        type=Path,
        action="append",
        default=[],
        help="Example frame without a presentation; may be supplied more than once",
    )
    parser.add_argument(
        "--exclude-threshold",
        type=float,
        default=0.90,
        help="Similarity to an excluded reference (default: 0.90)",
    )
    parser.add_argument(
        "--review-dir",
        type=Path,
        help="Report/preview folder (default: <Media>_dedup_review)",
    )
    parser.add_argument(
        "--no-preview",
        action="store_true",
        help="Do not create contact sheets",
    )
    parser.add_argument(
        "--move-rejected",
        type=Path,
        help="Move rejected images here after creating the report",
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    # Windows may default redirected console output to cp1252.  Selected folder
    # names can contain Cyrillic, so keep progress output Unicode-safe.
    for stream in (sys.stdout, sys.stderr):
        if stream is not None and hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="replace")
    args = build_parser().parse_args(argv)
    folder = args.folder.resolve()
    if not folder.is_dir():
        print(f"Folder not found: {folder}", file=sys.stderr)
        return 2
    paths = find_images(folder)
    if not paths:
        print(f"No images found in {folder}", file=sys.stderr)
        return 2

    frames: list[Frame] = []
    feature_size = (160, 100)
    excluded_features: list[tuple[Path, np.ndarray]] = []
    for reference_path in args.exclude_reference:
        reference_path = reference_path.resolve()
        if not reference_path.is_file():
            print(f"Exclude reference not found: {reference_path}", file=sys.stderr)
            return 2
        reference = load_frame(
            reference_path,
            args.roi,
            args.full_screen_min_width,
            feature_size,
        )
        excluded_features.append((reference_path, reference.feature))
    for number, path in enumerate(paths, start=1):
        try:
            frame = load_frame(
                path,
                args.roi,
                args.full_screen_min_width,
                feature_size,
            )
        except Exception as exc:  # keep a bad image from stopping a long scan
            print(f"Cannot read {path.name}: {exc}", file=sys.stderr)
            continue
        if frame.decision != "not_content" and frame.content_score < args.min_content_score:
            frame.decision = "not_content"
            frame.reason = "presentation crop is nearly blank"
        elif frame.decision != "not_content" and excluded_features:
            for reference_path, reference_feature in excluded_features:
                score, shift = comparison_score(
                    frame.feature, reference_feature, args.max_shift
                )
                if score >= args.exclude_threshold:
                    frame.decision = "not_content"
                    frame.reason = f"matches excluded screen {reference_path.name}"
                    frame.similarity = score
                    frame.best_shift = shift
                    break
        frames.append(frame)
        if number % 25 == 0 or number == len(paths):
            print(f"Loaded {number}/{len(paths)}", file=sys.stderr)

    groups = build_groups(
        frames,
        threshold=args.threshold,
        transient_threshold=args.transient_threshold,
        max_shift=args.max_shift,
    )
    choose_representatives(groups, args.max_shift, args.representative)
    if args.global_unique:
        suppress_global_repeats(groups, args.global_threshold, args.max_shift)

    review_dir = (
        args.review_dir.resolve()
        if args.review_dir
        else folder.with_name(f"{folder.name}_dedup_review")
    )
    report = review_dir / "report.csv"
    write_report(frames, report)

    kept = [frame for frame in frames if frame.decision == "keep"]
    rejected = [frame for frame in frames if frame.decision == "reject"]
    not_content = [frame for frame in frames if frame.decision == "not_content"]
    if not args.no_preview:
        write_contact_sheets(kept, review_dir / "preview_kept", "Kept slides")
        write_contact_sheets(rejected, review_dir / "preview_rejected", "Rejected frames")

    moved = 0
    if args.move_rejected:
        destination = args.move_rejected.resolve()
        if destination == folder or folder in destination.parents:
            # A child review folder is allowed, but it must not be scanned on a
            # future run because find_images is deliberately non-recursive.
            pass
        moved = move_rejected(frames, destination)

    print(f"Scanned: {len(frames)}")
    print(f"Keep: {len(kept)}")
    print(f"Reject as duplicate/transient: {len(rejected)}")
    print(f"Blank/no-content: {len(not_content)}")
    print(f"Report: {report}")
    if moved:
        print(f"Moved for review: {moved}")
    else:
        print("Source images were not changed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
