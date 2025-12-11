import os
import numpy as np
import pickle


class SimpleFLAME:
    """
    Minimal FLAME loader:
    - reads FLAME model from .pkl or .npz
    - computes vertices from shape/expr coefficients
    - ignores pose & skinning (pose = 0)
    - UV support:
      1) Если UV есть внутри FLAME-модели (uv / vt / texcoords) — используем их (per-vertex).
      2) Если задан uv_template_obj_path — читаем UV и vt-индексы из OBJ-шаблона (per-face-vertex),
         чтобы сохранить швы 1-в-1 как в UV_Knower.obj.
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

        if ext == ".npz":
            data = np.load(flame_model_path, allow_pickle=True)
        elif ext == ".pkl":
            with open(flame_model_path, "rb") as f:
                data = pickle.load(f, encoding="latin1")
        else:
            raise ValueError(f"Unsupported FLAME model format: {ext}")

        self.v_template = None        # [V, 3]
        shapedirs = None              # [V, 3, N]
        self.faces = None             # [F, 3] (int)

        # per-vertex UV (если вдруг есть в модели)
        self.uvs = None               # [V, 2] or None

        # per-face-vertex UV из OBJ-шаблона
        self.vt_coords = None         # [VT, 2]
        self.face_vt_indices = None   # [F, 3] (int, vt-индексы на каждый угол треугольника)

        def process_uv_array(raw_uv, v_count):
            if raw_uv is None:
                return None

            uvs = np.asarray(raw_uv, dtype=np.float32)
            if uvs.ndim != 2:
                print("SimpleFLAME: UVs found but not 2D array, ignore.")
                return None

            # [V, >=2]
            if uvs.shape[0] == v_count and uvs.shape[1] >= 2:
                return uvs[:, :2]

            # [>=2, V]
            if uvs.shape[1] == v_count and uvs.shape[0] >= 2:
                return uvs[:2, :].T

            print(f"SimpleFLAME: UVs shape {uvs.shape} "
                  f"does not match vertex count {v_count}, ignore.")
            return None

        # --- dict / npz ---
        if isinstance(data, dict):
            keys = list(data.keys())
            print("SimpleFLAME: Loaded FLAME .pkl model, keys:", keys)

            if "v_template" in data:
                self.v_template = np.asarray(data["v_template"], dtype=np.float32)
            else:
                raise KeyError("v_template not found in FLAME .pkl")

            if "shapedirs" in data:
                shapedirs = np.asarray(data["shapedirs"], dtype=np.float32)
            else:
                raise KeyError("shapedirs not found in FLAME .pkl")

            if "f" in data:
                self.faces = np.asarray(data["f"], dtype=np.int32)
            elif "faces" in data:
                self.faces = np.asarray(data["faces"], dtype=np.int32)
            elif "triangles" in data:
                self.faces = np.asarray(data["triangles"], dtype=np.int32)
            else:
                raise KeyError("faces / f / triangles not found in FLAME .pkl")

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
            files = list(getattr(data, "files", []))
            print("SimpleFLAME: Loaded FLAME .npz model, keys:", files)

            if "v_template" in files:
                self.v_template = data["v_template"].astype(np.float32)
            else:
                raise KeyError("v_template not found in FLAME .npz")

            if "shapedirs" in files:
                shapedirs = data["shapedirs"].astype(np.float32)
            else:
                raise KeyError("shapedirs not found in FLAME .npz")

            if "f" in files:
                self.faces = data["f"].astype(np.int32)
            elif "faces" in files:
                self.faces = data["faces"].astype(np.int32)
            elif "triangles" in files:
                self.faces = data["triangles"].astype(np.int32)
            else:
                raise KeyError("faces / f / triangles not found in FLAME .npz")

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

        if self.v_template is None:
            raise RuntimeError("SimpleFLAME: v_template was not loaded.")
        if shapedirs is None:
            raise RuntimeError("SimpleFLAME: shapedirs was not loaded.")
        if self.faces is None:
            raise RuntimeError("SimpleFLAME: faces were not loaded.")

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

        # Если UV внутри модели нет — пробуем подтянуть из OBJ-шаблона
        if self.uvs is None and uv_template_obj_path is not None:
            self._try_load_uv_from_template_obj(uv_template_obj_path)

        if self.uvs is not None:
            print(f"SimpleFLAME: Per-vertex UVs loaded, shape: {self.uvs.shape}")
        elif self.vt_coords is not None and self.face_vt_indices is not None:
            print(f"SimpleFLAME: Per-face-vertex UVs loaded from template: "
                  f"vt={self.vt_coords.shape}, face_vt={self.face_vt_indices.shape}")
        else:
            print("SimpleFLAME: No UVs found in model and no valid UVs loaded from template. "
                  "OBJ will be exported without vt.")

    # --- Загружаем UV из OBJ-шаблона (UV_Knower.obj), сохраняем vt и vt-индексы на каждый треугольник ---
    def _try_load_uv_from_template_obj(self, obj_path):
        if not os.path.isfile(obj_path):
            print(f"SimpleFLAME: UV template OBJ not found: {obj_path}")
            return

        v_count_in_model = self.v_template.shape[0]
        f_count_in_model = self.faces.shape[0]

        v_positions = []
        vt_list = []

        try:
            # Первый проход: считаем v и vt (для инфы)
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

            print(f"SimpleFLAME: UV template OBJ '{obj_path}': "
                  f"v_count={v_count_in_obj}, vt_count={vt_count}")

            if v_count_in_obj != v_count_in_model:
                print(
                    f"SimpleFLAME: Warning: UV template vertex count {v_count_in_obj} "
                    f"!= FLAME vertex count {v_count_in_model}. "
                    f"Assuming topology is still compatible (same face order)."
                )

            if vt_count == 0:
                print("SimpleFLAME: UV template has no vt entries, cannot use.")
                return

            # Второй проход: разбираем f-строки и собираем vt-индексы для каждого треугольника
            face_vt_indices = []

            with open(obj_path, "r", encoding="utf-8") as f:
                for line in f:
                    line = line.strip()
                    if not line or not line.startswith("f "):
                        continue

                    parts = line.split()[1:]
                    vt_for_face = []
                    for p in parts:
                        # форматы: v/vt, v/vt/vn, v//vn, v
                        tokens = p.split("/")
                        if len(tokens) < 2 or tokens[1] == "":
                            # нет vt — не подходит для UV-шаблона
                            vt_for_face = []
                            break
                        try:
                            vt_idx = int(tokens[1]) - 1  # OBJ 1-based
                        except ValueError:
                            vt_for_face = []
                            break
                        vt_for_face.append(vt_idx)

                    if len(vt_for_face) >= 3:
                        # треугольник — берём первые три vt
                        face_vt_indices.append(vt_for_face[:3])

            face_vt_indices = np.asarray(face_vt_indices, dtype=np.int32)
            f_count_in_obj = face_vt_indices.shape[0]

            print(f"SimpleFLAME: UV template has {f_count_in_obj} faces with vt indices.")

            if f_count_in_obj != f_count_in_model:
                print(
                    f"SimpleFLAME: Face count mismatch (template={f_count_in_obj}, model={f_count_in_model}), "
                    f"cannot reliably use template UV."
                )
                return

            self.vt_coords = np.asarray(vt_list, dtype=np.float32)
            self.face_vt_indices = face_vt_indices

            print("SimpleFLAME: Per-face-vertex UV mapping successfully loaded from template OBJ.")

        except Exception as ex:
            print(f"SimpleFLAME: Failed to read UV template OBJ: {ex}")
            self.vt_coords = None
            self.face_vt_indices = None
            return

    # --- вычисление вершин ---
    def compute_vertices(self,
                         shape_coeffs=None,
                         expr_coeffs=None):
        v = self.v_template.astype(np.float32).copy()

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

        if np.any(betas != 0.0):
            blend = np.tensordot(self.shapedirs, betas, axes=[2, 0])
            v = v + blend

        return v

    # --- экспорт OBJ ---
    def export_obj(self, path,
                   shape_coeffs=None,
                   expr_coeffs=None):
        vertices = self.compute_vertices(shape_coeffs, expr_coeffs)
        faces = self.faces

        v_count = vertices.shape[0]
        n_accum = np.zeros((v_count, 3), dtype=np.float32)

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

        norms = np.linalg.norm(n_accum, axis=1, keepdims=True)
        norms[norms == 0.0] = 1.0
        n_accum /= norms

        use_template_uv = (
            self.vt_coords is not None
            and self.face_vt_indices is not None
            and self.face_vt_indices.shape[0] == faces.shape[0]
        )

        use_per_vertex_uv = (
            self.uvs is not None
            and self.uvs.shape[0] == v_count
        )

        has_uv = use_template_uv or use_per_vertex_uv

        os.makedirs(os.path.dirname(path), exist_ok=True)

        with open(path, "w", encoding="utf-8") as f:
            # позиции вершин
            for v in vertices:
                f.write(f"v {v[0]} {v[1]} {v[2]}\n")

            if use_template_uv:
                # Пишем vt из OBJ-шаблона
                for uv in self.vt_coords:
                    f.write(f"vt {uv[0]} {uv[1]}\n")
            elif use_per_vertex_uv:
                # Пер-вершинные UV (если вдруг есть в самой модели)
                for uv in self.uvs:
                    f.write(f"vt {uv[0]} {uv[1]}\n")

            # нормали
            for n in n_accum:
                f.write(f"vn {n[0]} {n[1]} {n[2]}\n")

            if use_template_uv:
                # f v/vt/vn, где vt берём из face_vt_indices,
                # v и vn — из FLAME (один индекс на вершину/нормаль).
                for face_idx, tri in enumerate(faces):
                    i0 = int(tri[0]) + 1
                    i1 = int(tri[1]) + 1
                    i2 = int(tri[2]) + 1

                    vt0 = int(self.face_vt_indices[face_idx, 0]) + 1
                    vt1 = int(self.face_vt_indices[face_idx, 1]) + 1
                    vt2 = int(self.face_vt_indices[face_idx, 2]) + 1

                    f.write(f"f {i0}/{vt0}/{i0} {i1}/{vt1}/{i1} {i2}/{vt2}/{i2}\n")

            elif use_per_vertex_uv:
                # старый режим: один uv на вершину
                for tri in faces:
                    i0 = int(tri[0]) + 1
                    i1 = int(tri[1]) + 1
                    i2 = int(tri[2]) + 1
                    f.write(f"f {i0}/{i0}/{i0} {i1}/{i1}/{i1} {i2}/{i2}/{i2}\n")
            else:
                # без UV: f v//vn
                for tri in faces:
                    i0 = int(tri[0]) + 1
                    i1 = int(tri[1]) + 1
                    i2 = int(tri[2]) + 1
                    f.write(f"f {i0}//{i0} {i1}//{i1} {i2}//{i2}\n")

        print(f"SimpleFLAME: Saved OBJ to: {path} (has_uv={has_uv}, "
              f"template_uv={use_template_uv}, per_vertex_uv={use_per_vertex_uv})")
        if not has_uv:
            print("SimpleFLAME: OBJ exported without UVs because none were found in model or template.")
