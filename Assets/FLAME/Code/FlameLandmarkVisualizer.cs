#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace AlSo
{
    /// <summary>
    /// Вешается на голову (SkinnedMeshRenderer или MeshFilter) и рисует гизмами FLAME-лендмарки.
    /// Работает только в редакторе (полностью под UNITY_EDITOR).
    /// </summary>
    [ExecuteAlways]
    public class FlameLandmarkVisualizer : MonoBehaviour
    {
        [Header("Источник данных")]
        [Tooltip("Embedding с описанием FLAME-лендмарков (обычно 68).")]
        public FlameLandmarkEmbedding Embedding;

        [Tooltip("Если задан, будет использован этот SkinnedMeshRenderer. Иначе возьмём с текущего объекта.")]
        public SkinnedMeshRenderer SkinnedMesh;

        [Tooltip("Если задан и SkinnedMesh не найден, будем использовать этот MeshFilter.")]
        public MeshFilter MeshFilter;

        [Header("Отображение")]
        [Tooltip("Цвет шариков-гизмов.")]
        public Color GizmoColor = Color.cyan;

        [Tooltip("Радиус шариков-гизмов в мировых единицах.")]
        public float GizmoRadius = 0.002f;

        [Header("Фильтр по индексам (iBUG 0..67)")]
        [Range(0, 67)]
        public int MinShownIndex = 0;

        [Range(0, 67)]
        public int MaxShownIndex = 67;

        [Header("Подписи")]
        [Tooltip("Рисовать ли подписи (Label) рядом с точками.")]
        public bool ShowLabels = true;

        [Tooltip("Вертикальный сдвиг подписи относительно точки (в метрах).")]
        public float LabelVerticalOffset = 0.005f;

        private static GUIStyle _labelStyle;

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

            // Подготовим стиль для подписей (однократно)
            if (ShowLabels && _labelStyle == null)
            {
                _labelStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 10,
                    normal = { textColor = Color.yellow }
                };
            }

            var landmarks = Embedding.Landmarks;

            for (int i = 0; i < landmarks.Length; i++)
            {
                FlameLandmark lm = landmarks[i];

                int idx = lm.LandmarkIndex;
                if (idx < MinShownIndex || idx > MaxShownIndex)
                {
                    continue;
                }

                if (lm.FaceIndex < 0 || lm.FaceIndex >= faceCount)
                {
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
                // классическая формула: L = b0*v0 + b1*v1 + b2*v2
                Vector3 localPos = b.x * v0 + b.y * v1 + b.z * v2;
                Vector3 worldPos = meshTransform.TransformPoint(localPos);

                Gizmos.DrawSphere(worldPos, GizmoRadius);

                if (ShowLabels && _labelStyle != null)
                {
                    Vector3 labelPos = worldPos + Vector3.up * LabelVerticalOffset;
                    Handles.Label(labelPos, lm.Label, _labelStyle);
                }
            }
        }

        [ContextMenu("Debug Bary Sums")]
        private void DebugBarySums()
        {
            if (Embedding == null || Embedding.Landmarks == null)
            {
                return;
            }

            for (int i = 0; i < Embedding.Landmarks.Length; i++)
            {
                var b = Embedding.Landmarks[i].Barycentric;
                float s = b.x + b.y + b.z;
                if (Mathf.Abs(s - 1f) > 0.001f)
                {
                    UnityEngine.Debug.LogError(
                        $"[FlameLandmarkVisualizer] LM {i}: bary sum = {s}");
                }
            }

            UnityEngine.Debug.Log("[FlameLandmarkVisualizer] Bary sums checked.");
        }
    }
}
#endif
