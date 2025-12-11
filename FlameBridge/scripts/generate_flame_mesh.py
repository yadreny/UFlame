import json
import sys
import os

from simple_flame import SimpleFLAME


def main():
    if len(sys.argv) < 2:
        print("Usage: python generate_flame_mesh.py config.json")
        sys.exit(1)

    config_path = sys.argv[1]
    if not os.path.isfile(config_path):
        print(f"Config file not found: {config_path}")
        sys.exit(1)

    with open(config_path, "r", encoding="utf-8") as f:
        cfg = json.load(f)

    flame_model_path = cfg["flame_model_path"]
    num_shape = int(cfg.get("num_shape", 300))
    num_expr = int(cfg.get("num_expr", 100))

    shape_coeffs = cfg.get("shape_coeffs", None)
    expr_coeffs = cfg.get("expr_coeffs", None)
    out_obj_path = cfg["out_obj_path"]

    flame_model_path = os.path.abspath(flame_model_path)
    out_obj_path = os.path.abspath(out_obj_path)

    print(f"[PY] Using FLAME model: {flame_model_path}")
    print(f"[PY] Output OBJ: {out_obj_path}")

    flame = SimpleFLAME(flame_model_path, num_shape=num_shape, num_expr=num_expr)
    flame.export_obj(out_obj_path, shape_coeffs=shape_coeffs, expr_coeffs=expr_coeffs)

    print("[PY] Done.")


if __name__ == "__main__":
    main()
