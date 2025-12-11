using UnityEngine;

namespace AlSo
{
    /// <summary>
    /// Вешается на голову (SkinnedMeshRenderer или MeshFilter), умеет отображать лендмарки гизмами.
    /// </summary>
    [ExecuteAlways]
    public class FlameLandmarkVisualizer : MonoBehaviour
    {
        [Tooltip("Embedding с описанием лендмарков FLAME / Mediapipe.")]
        public FlameLandmarkEmbedding Embedding;

        [Tooltip("Если задан, будет использован этот SkinnedMeshRenderer. Иначе возьмём с текущего объекта.")]
        public SkinnedMeshRenderer SkinnedMesh;

        [Tooltip("Если задан и SkinnedMesh не найден, будем использовать этот MeshFilter.")]
        public MeshFilter MeshFilter;

        [Tooltip("Цвет гизмов лендмарков.")]
        public Color GizmoColor = Color.cyan;

        [Tooltip("Радиус шариков гизмов.")]
        public float GizmoRadius = 0.002f;

        private void OnDrawGizmos()
        {
            DrawLandmarkGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            DrawLandmarkGizmos();
        }

        private void DrawLandmarkGizmos()
        {
            if (Embedding == null || Embedding.Landmarks == null || Embedding.Landmarks.Length == 0)
            {
                return;
            }

            Mesh mesh = null;
            Transform meshTransform = transform;

            var smr = SkinnedMesh != null ? SkinnedMesh : GetComponent<SkinnedMeshRenderer>();
            if (smr != null && smr.sharedMesh != null)
            {
                mesh = smr.sharedMesh;
                meshTransform = smr.transform;
            }
            else
            {
                var mf = MeshFilter != null ? MeshFilter : GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    mesh = mf.sharedMesh;
                    meshTransform = mf.transform;
                }
            }

            if (mesh == null)
            {
                return;
            }

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            if (triangles == null || triangles.Length == 0)
            {
                return;
            }

            int faceCount = triangles.Length / 3;

            Gizmos.color = GizmoColor;

            var landmarks = Embedding.Landmarks;
            for (int i = 0; i < landmarks.Length; i++)
            {
                FlameLandmark lm = landmarks[i];

                if (lm.FaceIndex < 0 || lm.FaceIndex >= faceCount)
                {
                    // некорректный индекс треугольника
                    continue;
                }

                int triStart = lm.FaceIndex * 3;
                int i0 = triangles[triStart];
                int i1 = triangles[triStart + 1];
                int i2 = triangles[triStart + 2];

                if (i0 < 0 || i0 >= vertices.Length ||
                    i1 < 0 || i1 >= vertices.Length ||
                    i2 < 0 || i2 >= vertices.Length)
                {
                    continue;
                }

                Vector3 v0 = vertices[i0];
                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];

                Vector3 b = lm.Barycentric;
                float bw = 1.0f - b.x - b.y;

                // barycentric: L = b.x * v0 + b.y * v1 + bw * v2
                Vector3 localPos = b.x * v0 + b.y * v1 + bw * v2;
                Vector3 worldPos = meshTransform.TransformPoint(localPos);

                Gizmos.DrawSphere(worldPos, GizmoRadius);
            }
        }
    }
}

