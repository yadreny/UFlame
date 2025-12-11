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

    # Читаем JSON, Unity пишет UTF-8 с BOM → используем utf-8-sig
    with open(config_path, "r", encoding="utf-8-sig") as f:
        cfg = json.load(f)

    # Обязательные пути
    flame_model_path = cfg["flame_model_path"]
    out_obj_path = cfg["out_obj_path"]

    # Необязательный путь до OBJ с UV
    uv_template_obj_path = cfg.get("uv_template_obj_path", None)

    # Параметры модели
    num_shape = int(cfg.get("num_shape", 300))
    num_expr = int(cfg.get("num_expr", 100))

    # Коэффициенты
    shape_coeffs = cfg.get("shape_coeffs", None)
    expr_coeffs = cfg.get("expr_coeffs", None)

    # Список индексов треугольников для второго сабмеша (пока только заготовка)
    eye_tri_indices = cfg.get("eye_tri_indices", None)
    if eye_tri_indices is not None:
        try:
            eye_tri_indices = [int(i) for i in eye_tri_indices]
        except Exception:
            print("[PY] Warning: eye_tri_indices present but not a list of ints, ignoring.")
            eye_tri_indices = None

    # Абсолютные пути
    flame_model_path = os.path.abspath(flame_model_path)
    out_obj_path = os.path.abspath(out_obj_path)
    if uv_template_obj_path is not None:
        uv_template_obj_path = os.path.abspath(uv_template_obj_path)

    print(f"[PY] Using FLAME model: {flame_model_path}")
    print(f"[PY] Output OBJ: {out_obj_path}")
    if uv_template_obj_path is not None:
        print(f"[PY] UV template OBJ: {uv_template_obj_path}")
    else:
        print("[PY] No UV template OBJ path provided.")

    if eye_tri_indices is not None:
        print(f"[PY] Eye submesh triangle indices count: {len(eye_tri_indices)}")
    else:
        print("[PY] No eye submesh triangle indices provided (single submesh for now).")

    # Создаём FLAME-модель (simple_flame.py должен принимать uv_template_obj_path).
    flame = SimpleFLAME(
        flame_model_path=flame_model_path,
        num_shape=num_shape,
        num_expr=num_expr,
        uv_template_obj_path=uv_template_obj_path
    )

    # Пока eye_tri_indices ни на что не влияет — это заготовка.
    # Позже сюда добавим передачу индексов в export_obj / разделение на сабмеши.
    flame.export_obj(
        out_obj_path,
        shape_coeffs=shape_coeffs,
        expr_coeffs=expr_coeffs,
        # сюда потом добавим eye_tri_indices=...
    )

    print("[PY] Done.")


if __name__ == "__main__":
    main()
