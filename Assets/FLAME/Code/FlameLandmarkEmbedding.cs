using Sirenix.OdinInspector;
using System;
using UnityEngine;

namespace AlSo
{
    /// <summary>
    /// Один лендмарк FLAME/Mediapipe:
    /// - FaceIndex: индекс треугольника в массиве faces (triangles / 3).
    /// - LandmarkIndex: "оригинальный" индекс из landmark_indices (на случай, если он чем-то важен).
    /// - Barycentric: барицентрические координаты внутри треугольника (x, y, z).
    /// </summary>
    [Serializable]
    public struct FlameLandmark
    {
        public int FaceIndex;
        public int LandmarkIndex;
        public Vector3 Barycentric;
    }

    /// <summary>
    /// ScriptableObject с embedding'ом Mediapipe-лендмарков для FLAME.
    /// Заполняется из JSON-а, сгенерированного питоном.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MediapipeLandmarkEmbedding",
        menuName = "AlSo/FLAME/Mediapipe Landmark Embedding",
        order = 10)]
    public class FlameLandmarkEmbedding : ScriptableObject
    {
        [Tooltip("Сколько лендмарков описано (обычно 105).")]
        public int NumLandmarks;

        [Tooltip("Все лендмарки с индексами треугольников и барицентрическими координатами.")]
        [InlineProperty]
        public FlameLandmark[] Landmarks;
    }
}