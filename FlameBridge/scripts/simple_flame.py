import os
import numpy as np
import pickle


class SimpleFLAME:
    """
    Minimal FLAME loader:
    - reads FLAME model from .pkl or .npz
    - computes vertices from shape/expr coefficients
    - ignores pose & skinning (pose = 0)
    """

    def __init__(self, flame_model_path: str,
                 num_shape: int = 300,
                 num_expr: int = 100):
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

        # extract v_template, shapedirs, faces
        if isinstance(data, dict):
            keys = list(data.keys())
            print("Loaded FLAME .pkl model, keys:", keys)

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
        else:
            # npz-like object
            print("Loaded FLAME .npz model, keys:", data.files)
            if "v_template" in data.files:
                self.v_template = data["v_template"].astype(np.float32)
            else:
                raise KeyError("v_template not found in FLAME .npz")

            if "shapedirs" in data.files:
                shapedirs = data["shapedirs"].astype(np.float32)
            else:
                raise KeyError("shapedirs not found in FLAME .npz")

            if "f" in data.files:
                self.faces = data["f"].astype(np.int32)
            elif "faces" in data.files:
                self.faces = data["faces"].astype(np.int32)
            else:
                raise KeyError("faces / f not found in FLAME .npz")

        # shapedirs to [V,3,N]
        if shapedirs.ndim == 2:
            # [V*3, N]
            num_verts = self.v_template.shape[0]
            v3, n = shapedirs.shape
            if v3 != num_verts * 3:
                raise ValueError(
                    f"shapedirs shape {shapedirs.shape} inconsistent with "
                    f"v_template {self.v_template.shape}"
                )
            shapedirs = shapedirs.reshape(num_verts, 3, n)
        elif shapedirs.ndim == 3:
            n = shapedirs.shape[2]
        else:
            raise ValueError(
                f"Unexpected shapedirs ndim={shapedirs.ndim}, shape={shapedirs.shape}"
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

        print(f"v_template: {self.v_template.shape}, "
              f"shapedirs: {self.shapedirs.shape}, faces: {self.faces.shape}")
        print(f"Using first {num_shape} comps as shape, "
              f"next {num_expr} as expr.")

    def compute_vertices(self,
                         shape_coeffs=None,
                         expr_coeffs=None):
        """
        shape_coeffs: [num_shape] or None (zeros)
        expr_coeffs:  [num_expr]  or None (zeros)
        returns: vertices [V, 3]
        """
        if shape_coeffs is None:
            shape_coeffs = np.zeros(self.num_shape, dtype=np.float32)
        else:
            shape_coeffs = np.asarray(shape_coeffs, dtype=np.float32)
            assert shape_coeffs.shape[0] == self.num_shape

        if expr_coeffs is None:
            expr_coeffs = np.zeros(self.num_expr, dtype=np.float32)
        else:
            expr_coeffs = np.asarray(expr_coeffs, dtype=np.float32)
            assert expr_coeffs.shape[0] == self.num_expr

        betas = np.zeros(self.num_shape_total, dtype=np.float32)
        betas[:self.num_shape] = shape_coeffs
        betas[self.num_shape:self.num_shape + self.num_expr] = expr_coeffs

        v_offsets = np.tensordot(self.shapedirs, betas, axes=([2], [0]))
        vertices = self.v_template + v_offsets
        return vertices

    def export_obj(self, path: str,
                   shape_coeffs=None,
                   expr_coeffs=None):
        """
        Export OBJ with:
        - vertices (v)
        - vertex normals (vn)
        - faces referencing normals (f v//vn ...)
        """
        vertices = self.compute_vertices(shape_coeffs, expr_coeffs)
        faces = self.faces

        v_count = vertices.shape[0]
        n_accum = np.zeros((v_count, 3), dtype=np.float32)

        # accumulate face normals
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

        os.makedirs(os.path.dirname(path), exist_ok=True)

        with open(path, "w", encoding="utf-8") as f:
            for v in vertices:
                f.write(f"v {v[0]} {v[1]} {v[2]}\n")

            for n in n_accum:
                f.write(f"vn {n[0]} {n[1]} {n[2]}\n")

            for tri in faces:
                i0, i1, i2 = int(tri[0]) + 1, int(tri[1]) + 1, int(tri[2]) + 1
                f.write(f"f {i0}//{i0} {i1}//{i1} {i2}//{i2}\n")

        print(f"Saved OBJ to: {path}")
