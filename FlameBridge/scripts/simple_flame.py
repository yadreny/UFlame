import os
import numpy as np
import pickle


class SimpleFLAME:
    """
    Minimal FLAME loader:
    - reads FLAME model from .pkl or .npz
    - computes vertices from shape/expr coefficients
    - ignores pose & skinning (pose = 0)
    - optionally loads UVs:
      1) из самой FLAME-модели (keys: 'uv', 'vt', 'texcoords')
      2) из внешнего OBJ-шаблона (uv_template_obj_path), если в модели нет UV
         (UV берутся по схеме vIndex -> vtIndex из f-строк)
    """

    def __init__(self,
                 flame_model_path,
                 num_shape=300,
                 num_expr=100,
                 uv_template_obj_path=None):
        self.model_path = flame_model_path

        if not os.path.isfile(flame_model_path):
            raise FileNotFoundError(f"FLAME model not found: {flame_model_path}")

        ext = os.path.splitext(flame_model_path)[1].lower()

        # load .npz or .pkl
        if ext == ".npz":
            data = np.load(flame_model_path, allow_pickle=True)
        elif ext == ".pkl":
            with open(flame_model_path, "rb") as f:
                # FLAME models are often pickled with latin1
                data = pickle.load(f, encoding="latin1")
        else:
            raise ValueError(f"Unsupported FLAME model format: {ext}")

        self.v_template = None        # [V, 3]
        shapedirs = None              # [V, 3, N]
        self.faces = None             # [F, 3] (int)
        self.uvs = None               # [V, 2] or None

        # --- helper: привести сырые UV к виду [V,2] ---
        def process_uv_array(raw_uv, v_count):
            if raw_uv is None:
                return None

            uvs = np.asarray(raw_uv, dtype=np.float32)
            if uvs.ndim != 2:
                print("SimpleFLAME: UVs found but not 2D array, ignore.")
                return None

            # варианты:
            # 1) [V, 2] или [V, >=2]
            if uvs.shape[0] == v_count and uvs.shape[1] >= 2:
                return uvs[:, :2]

            # 2) [2, V] или [>=2, V]
            if uvs.shape[1] == v_count and uvs.shape[0] >= 2:
                return uvs[:2, :].T

            print(f"SimpleFLAME: UVs shape {uvs.shape} "
                  f"does not match vertex count {v_count}, ignore.")
            return None

        # --- разбор dict / npz ---
        if isinstance(data, dict):
            keys = list(data.keys())
            print("SimpleFLAME: Loaded FLAME .pkl model, keys:", keys)

            # v_template
            if "v_template" in data:
                self.v_template = np.asarray(data["v_template"], dtype=np.float32)
            else:
                raise KeyError("v_template not found in FLAME .pkl")

            # shapedirs
            if "shapedirs" in data:
                shapedirs = np.asarray(data["shapedirs"], dtype=np.float32)
            else:
                raise KeyError("shapedirs not found in FLAME .pkl")

            # faces
            if "f" in data:
                self.faces = np.asarray(data["f"], dtype=np.int32)
            elif "faces" in data:
                self.faces = np.asarray(data["faces"], dtype=np.int32)
            elif "triangles" in data:
                self.faces = np.asarray(data["triangles"], dtype=np.int32)
            else:
                raise KeyError("faces / f / triangles not found in FLAME .pkl")

            # UVs (optional, внутри модели)
            raw_uv = None
            if "uv" in data:
                raw_uv = data["uv"]
                print("SimpleFLAME: UVs candidate from key 'uv'")
            elif "vt" in data:
                raw_uv = data["vt"]
                print("SimpleFLAME: UVs candidate from key 'vt'")
            elif "texcoords" in data:
                raw_uv = data["texcoords"]
                print("SimpleFLAME: UVs candidate from key 'texcoords'")

            if raw_uv is not None:
                self.uvs = process_uv_array(raw_uv, self.v_template.shape[0])
            else:
                self.uvs = None

        else:
            # npz-like object
            files = list(getattr(data, "files", []))
            print("SimpleFLAME: Loaded FLAME .npz model, keys:", files)

            # v_template
            if "v_template" in files:
                self.v_template = data["v_template"].astype(np.float32)
            else:
                raise KeyError("v_template not found in FLAME .npz")

            # shapedirs
            if "shapedirs" in files:
                shapedirs = data["shapedirs"].astype(np.float32)
            else:
                raise KeyError("shapedirs not found in FLAME .npz")

            # faces
            if "f" in files:
                self.faces = data["f"].astype(np.int32)
            elif "faces" in files:
                self.faces = data["faces"].astype(np.int32)
            elif "triangles" in files:
                self.faces = data["triangles"].astype(np.int32)
            else:
                raise KeyError("faces / f / triangles not found in FLAME .npz")

            # UVs (optional, внутри модели)
            raw_uv = None
            if "uv" in files:
                raw_uv = data["uv"]
                print("SimpleFLAME: UVs candidate from key 'uv'")
            elif "vt" in files:
                raw_uv = data["vt"]
                print("SimpleFLAME: UVs candidate from key 'vt'")
            elif "texcoords" in files:
                raw_uv = data["texcoords"]
                print("SimpleFLAME: UVs candidate from key 'texcoords'")

            if raw_uv is not None:
                self.uvs = process_uv_array(raw_uv, self.v_template.shape[0])
            else:
                self.uvs = None

        # финальные проверки по геометрии
        if self.v_template is None:
            raise RuntimeError("SimpleFLAME: v_template was not loaded.")
        if shapedirs is None:
            raise RuntimeError("SimpleFLAME: shapedirs was not loaded.")
        if self.faces is None:
            raise RuntimeError("SimpleFLAME: faces were not loaded.")

        # shapedirs ожидаем [V, 3, N]
        if shapedirs.ndim != 3 or shapedirs.shape[1] != 3:
            raise ValueError(
                f"SimpleFLAME: expected shapedirs with shape [V,3,N], "
                f"got {shapedirs.shape}"
            )

        self.shapedirs = shapedirs
        self.num_shape_total = self.shapedirs.shape[2]

        if num_shape + num_expr > self.num_shape_total:
            raise ValueError(
                f"Requested num_shape+num_expr={num_shape+num_expr}, "
                f"but model has only {self.num_shape_total} components in shapedirs"
            )

        self.num_shape = num_shape
        self.num_expr = num_expr

        print(f"SimpleFLAME: v_template: {self.v_template.shape}, "
              f"shapedirs: {self.shapedirs.shape}, faces: {self.faces.shape}")
        print(f"SimpleFLAME: Using first {num_shape} comps as shape, "
              f"next {num_expr} as expr.")

        # если UV не нашли внутри модели — пробуем подтянуть из OBJ-шаблона
        if self.uvs is None and uv_template_obj_path is not None:
            self._try_load_uv_from_template_obj(uv_template_obj_path)

        if self.uvs is not None:
            print(f"SimpleFLAME: UVs loaded, shape: {self.uvs.shape}")
        else:
            print("SimpleFLAME: No UVs found in FLAME model "
                  "and no valid UVs loaded from template. "
                  "OBJ will be exported without vt.")

    # --- подгрузка UV из внешнего OBJ-шаблона (через f v/vt/...) ---
    def _try_load_uv_from_template_obj(self, obj_path):
        if not os.path.isfile(obj_path):
            print(f"SimpleFLAME: UV template OBJ not found: {obj_path}")
            return

        v_positions = []
        vt_list = []

        try:
            # Первый проход: собираем все v и vt (без faces)
            with open(obj_path, "r", encoding="utf-8") as f:
                for line in f:
                    line = line.strip()
                    if not line:
                        continue

                    if line.startswith("v "):
                        parts = line.split()
                        if len(parts) >= 4:
                            try:
                                x = float(parts[1])
                                y = float(parts[2])
                                z = float(parts[3])
                                v_positions.append((x, y, z))
                            except ValueError:
                                continue

                    elif line.startswith("vt "):
                        parts = line.split()
                        if len(parts) >= 3:
                            try:
                                u = float(parts[1])
                                v = float(parts[2])
                                vt_list.append((u, v))
                            except ValueError:
                                continue

            v_count_in_obj = len(v_positions)
            vt_count = len(vt_list)
            v_count_in_model = self.v_template.shape[0]

            print(f"SimpleFLAME: UV template OBJ '{obj_path}': "
                  f"v_count={v_count_in_obj}, vt_count={vt_count}")

            if v_count_in_obj != v_count_in_model:
                print(
                    f"SimpleFLAME: UV template vertex count {v_count_in_obj} "
                    f"!= FLAME vertex count {v_count_in_model}, cannot use."
                )
                return

            if vt_count == 0:
                print("SimpleFLAME: UV template has no vt entries, cannot use.")
                return

            # Второй проход: парсим faces и накапливаем UV по вершинам через vIndex/vtIndex.
            sum_uv = np.zeros((v_count_in_obj, 2), dtype=np.float32)
            count_uv = np.zeros((v_count_in_obj,), dtype=np.int32)

            with open(obj_path, "r", encoding="utf-8") as f:
                for line in f:
                    line = line.strip()
                    if not line or not line.startswith("f "):
                        continue

                    # Пример: f v1/vt1/vn1 v2/vt2/vn2 v3/vt3/vn3
                    # или:    f v1/vt1 v2/vt2 v3/vt3
                    parts = line.split()[1:]
                    for p in parts:
                        tokens = p.split("/")
                        if len(tokens) < 2 or tokens[1] == "":
                            # нет vt
                            continue
                        try:
                            v_idx = int(tokens[0]) - 1  # OBJ 1-based
                            vt_idx = int(tokens[1]) - 1
                        except ValueError:
                            continue

                        if 0 <= v_idx < v_count_in_obj and 0 <= vt_idx < vt_count:
                            u, v = vt_list[vt_idx]
                            sum_uv[v_idx, 0] += u
                            sum_uv[v_idx, 1] += v
                            count_uv[v_idx] += 1

            # Среднее UV по каждому v.
            uvs = np.zeros((v_count_in_obj, 2), dtype=np.float32)
            missing = 0
            for i in range(v_count_in_obj):
                if count_uv[i] > 0:
                    uvs[i, 0] = sum_uv[i, 0] / float(count_uv[i])
                    uvs[i, 1] = sum_uv[i, 1] / float(count_uv[i])
                else:
                    # Вершина нигде не использована в faces с vt → UV (0,0)
                    missing += 1

            if missing > 0:
                print(f"SimpleFLAME: Warning: {missing} vertices had no UV in faces; "
                      f"their UV set to (0,0).")

            self.uvs = uvs
            print("SimpleFLAME: UVs successfully loaded from template OBJ (per-vertex from faces).")

        except Exception as ex:
            print(f"SimpleFLAME: Failed to read UV template OBJ: {ex}")
            return

    # --- вычисление вершин ---
    def compute_vertices(self,
                         shape_coeffs=None,
                         expr_coeffs=None):
        """
        shape_coeffs: [num_shape] or None
        expr_coeffs:  [num_expr] or None

        Returns:
            vertices [V, 3]
        """
        v = self.v_template.astype(np.float32).copy()

        # общий вектор betas длиной num_shape_total
        betas = np.zeros((self.num_shape_total,), dtype=np.float32)

        if shape_coeffs is not None:
            shape_coeffs = np.asarray(shape_coeffs, dtype=np.float32).ravel()
            if shape_coeffs.shape[0] > self.num_shape:
                raise ValueError(
                    f"shape_coeffs length {shape_coeffs.shape[0]} "
                    f"exceeds num_shape={self.num_shape}"
                )
            betas[:shape_coeffs.shape[0]] = shape_coeffs

        if expr_coeffs is not None:
            expr_coeffs = np.asarray(expr_coeffs, dtype=np.float32).ravel()
            if expr_coeffs.shape[0] > self.num_expr:
                raise ValueError(
                    f"expr_coeffs length {expr_coeffs.shape[0]} "
                    f"exceeds num_expr={self.num_expr}"
                )
            start = self.num_shape
            betas[start:start + expr_coeffs.shape[0]] = expr_coeffs

        # blendshape: tensordot по оси N
        if np.any(betas != 0.0):
            blend = np.tensordot(self.shapedirs, betas, axes=[2, 0])
            v = v + blend

        return v

    # --- экспорт OBJ ---
    def export_obj(self, path,
                   shape_coeffs=None,
                   expr_coeffs=None):
        """
        Export OBJ with:
        - vertices (v)
        - optional UVs (vt) if available
        - vertex normals (vn)
        - faces referencing normals/uvs:
          * если UV есть:  f v/vt/vn
          * если UV нет:   f v//vn
        """
        vertices = self.compute_vertices(shape_coeffs, expr_coeffs)
        faces = self.faces

        v_count = vertices.shape[0]
        n_accum = np.zeros((v_count, 3), dtype=np.float32)

        # накапливаем нормали
        for tri in faces:
            i0, i1, i2 = int(tri[0]), int(tri[1]), int(tri[2])
            p0 = vertices[i0]
            p1 = vertices[i1]
            p2 = vertices[i2]

            e1 = p1 - p0
            e2 = p2 - p0
            n = np.cross(e1, e2)

            n_accum[i0] += n
            n_accum[i1] += n
            n_accum[i2] += n

        # нормализация
        norms = np.linalg.norm(n_accum, axis=1, keepdims=True)
        norms[norms == 0.0] = 1.0
        n_accum /= norms

        has_uv = self.uvs is not None and self.uvs.shape[0] == v_count

        os.makedirs(os.path.dirname(path), exist_ok=True)

        with open(path, "w", encoding="utf-8") as f:
            # позиции
            for v in vertices:
                f.write(f"v {v[0]} {v[1]} {v[2]}\n")

            # UV
            if has_uv:
                for uv in self.uvs:
                    f.write(f"vt {uv[0]} {uv[1]}\n")

            # нормали
            for n in n_accum:
                f.write(f"vn {n[0]} {n[1]} {n[2]}\n")

            # индексы
            for tri in faces:
                i0, i1, i2 = int(tri[0]) + 1, int(tri[1]) + 1, int(tri[2]) + 1
                if has_uv:
                    # v/vt/vn
                    f.write(f"f {i0}/{i0}/{i0} {i1}/{i1}/{i1} {i2}/{i2}/{i2}\n")
                else:
                    # v//vn
                    f.write(f"f {i0}//{i0} {i1}//{i1} {i2}//{i2}\n")

        print(f"SimpleFLAME: Saved OBJ to: {path} (has_uv={has_uv})")
        if not has_uv:
            print("SimpleFLAME: OBJ exported without UVs because none were found "
                  "in model or valid template.")
