import os
import sys
import json
import numpy as np


def main():
    if len(sys.argv) < 3:
        print("Usage: python dump_mediapipe_embedding_to_json.py <mediapipe_landmark_embedding.npz> <out.json>")
        sys.exit(1)

    npz_path = os.path.abspath(sys.argv[1])
    out_json_path = os.path.abspath(sys.argv[2])

    if not os.path.isfile(npz_path):
        print(f"NPZ file not found: {npz_path}")
        sys.exit(1)

    print(f"[INF] Loading NPZ: {npz_path}")
    d = np.load(npz_path)

    # ожидаем эти ключи (ты уже видел их в выводе)
    required_keys = ["lmk_face_idx", "lmk_b_coords", "landmark_indices"]
    for k in required_keys:
        if k not in d:
            print(f"[ERR] Key '{k}' not found in NPZ. Available keys: {list(d.keys())}")
            sys.exit(1)

    lmk_face_idx = d["lmk_face_idx"]        # (105,)
    lmk_b_coords = d["lmk_b_coords"]        # (105,3)
    landmark_indices = d["landmark_indices"]  # (105,)

    if lmk_b_coords.shape[0] != lmk_face_idx.shape[0] or lmk_b_coords.shape[1] != 3:
        print(f"[ERR] Unexpected lmk_b_coords shape: {lmk_b_coords.shape}")
        sys.exit(1)

    num_landmarks = int(lmk_face_idx.shape[0])

    print(f"[INF] num_landmarks: {num_landmarks}")
    print(f"[INF] lmk_face_idx.shape: {lmk_face_idx.shape}")
    print(f"[INF] lmk_b_coords.shape: {lmk_b_coords.shape}")
    print(f"[INF] landmark_indices.shape: {landmark_indices.shape}")

    # собираем всё в простой JSON-формат
    # делаем tolist(), чтобы избавиться от numpy-типов
    payload = {
        "num_landmarks": num_landmarks,
        "lmk_face_idx": lmk_face_idx.tolist(),          # [105]
        "lmk_b_coords": lmk_b_coords.tolist(),          # [105][3]
        "landmark_indices": landmark_indices.tolist()   # [105]
    }

    os.makedirs(os.path.dirname(out_json_path), exist_ok=True)

    with open(out_json_path, "w", encoding="utf-8") as f:
        json.dump(payload, f, indent=2, ensure_ascii=False)

    print(f"[INF] Saved JSON to: {out_json_path}")


if __name__ == "__main__":
    main()
