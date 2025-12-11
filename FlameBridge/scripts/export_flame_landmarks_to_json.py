import os
import json
import numpy as np

def main():
    base_dir = os.path.dirname(__file__)  # FlameBridge\scripts
    npy_path = os.path.join(base_dir, "..", "Analysis", "landmark_embedding.npy")
    npy_path = os.path.abspath(npy_path)

    out_json = os.path.join(base_dir, "..", "Analysis", "flame_canonical_landmarks.json")
    out_json = os.path.abspath(out_json)

    print("[export] landmark_embedding.npy:", npy_path)
    if not os.path.isfile(npy_path):
        print("[export] ERROR: file not found")
        return

    data = np.load(npy_path, allow_pickle=True, encoding="latin1")
    # В DECA/FLAME это dict, упакованный в npy
    if isinstance(data, np.ndarray) and data.dtype == object and data.shape == ():
        data = data.item()

    keys = list(data.keys())
    print("[export] keys:", keys)

    payload = {}
    for k in [
        "static_lmk_faces_idx",
        "static_lmk_bary_coords",
        "dynamic_lmk_faces_idx",
        "dynamic_lmk_bary_coords",
        "full_lmk_faces_idx",
        "full_lmk_bary_coords",
    ]:
        if k not in data:
            print(f"[export] WARN: key '{k}' not in file")
            continue
        arr = np.asarray(data[k])
        print(f"[export] {k}: shape={arr.shape}, dtype={arr.dtype}")
        payload[k] = arr.tolist()

    with open(out_json, "w", encoding="utf-8") as f:
        json.dump(payload, f)

    print("[export] written JSON to:", out_json)

if __name__ == "__main__":
    main()
