using System;

using UnityEngine;

namespace AlSo
{

    /// <summary>
    /// Семантические имена для 68-точечной iBUG-схемы.
    /// Значения индексов совпадают с LandmarkIndex (0..67).
    /// </summary>
    public enum FlameLandmarkId
    {
        Unknown = -1,

        // 0–16: контур / jawline (от правого края подбородка по дуге к левому)
        Jaw_00 = 0,
        Jaw_01 = 1,
        Jaw_02 = 2,
        Jaw_03 = 3,
        Jaw_04 = 4,
        Jaw_05 = 5,
        Jaw_06 = 6,
        Jaw_07 = 7,
        Jaw_08 = 8,
        Jaw_09 = 9,
        Jaw_10 = 10,
        Jaw_11 = 11,
        Jaw_12 = 12,
        Jaw_13 = 13,
        Jaw_14 = 14,
        Jaw_15 = 15,
        Jaw_16 = 16,

        // 17–21: правая бровь (от внешнего края к переносью)
        RightBrow_Outer = 17,
        RightBrow_OuterMid = 18,
        RightBrow_Center = 19,
        RightBrow_InnerMid = 20,
        RightBrow_Inner = 21,

        // 22–26: левая бровь (от переносицы к внешнему краю)
        LeftBrow_Inner = 22,
        LeftBrow_InnerMid = 23,
        LeftBrow_Center = 24,
        LeftBrow_OuterMid = 25,
        LeftBrow_Outer = 26,

        // 27–30: переносица (сверху вниз)
        NoseBridge_Top = 27,
        NoseBridge_Upper = 28,
        NoseBridge_Lower = 29,
        NoseBridge_Base = 30,

        // 31–35: низ носа (справа налево)
        Nose_Right = 31,
        Nose_RightMid = 32,
        Nose_Tip = 33,
        Nose_LeftMid = 34,
        Nose_Left = 35,

        // 36–41: правый глаз (по часовой, с внешнего угла)
        RightEye_Outer = 36,
        RightEye_UpperOuter = 37,
        RightEye_UpperInner = 38,
        RightEye_Inner = 39,
        RightEye_LowerInner = 40,
        RightEye_LowerOuter = 41,

        // 42–47: левый глаз (по часовой, с внутреннего угла)
        LeftEye_Inner = 42,
        LeftEye_UpperInner = 43,
        LeftEye_UpperOuter = 44,
        LeftEye_Outer = 45,
        LeftEye_LowerOuter = 46,
        LeftEye_LowerInner = 47,

        // 48–59: внешний контур губ (право -> верх -> лево -> низ)
        MouthOuter_RightCorner = 48,
        MouthOuter_UpperRight = 49,
        MouthOuter_UpperRightMid = 50,
        MouthOuter_UpperCenter = 51,
        MouthOuter_UpperLeftMid = 52,
        MouthOuter_UpperLeft = 53,
        MouthOuter_LeftCorner = 54,
        MouthOuter_LowerLeft = 55,
        MouthOuter_LowerLeftMid = 56,
        MouthOuter_LowerCenter = 57,
        MouthOuter_LowerRightMid = 58,
        MouthOuter_LowerRight = 59,

        // 60–67: внутренний контур губ (право -> верх -> лево -> низ)
        MouthInner_RightCorner = 60,
        MouthInner_UpperRight = 61,
        MouthInner_UpperCenter = 62,
        MouthInner_UpperLeft = 63,
        MouthInner_LeftCorner = 64,
        MouthInner_LowerLeft = 65,
        MouthInner_LowerCenter = 66,
        MouthInner_LowerRight = 67
    }

    /// <summary>
    /// Один FLAME-лендмарк.
    /// </summary>
    [Serializable]
    public struct FlameLandmark
    {
        public int FaceIndex;
        public int LandmarkIndex;
        public Vector3 Barycentric;

        /// <summary>
        /// Семантический Id (enum), если 0..67, иначе Unknown.
        /// </summary>
        public FlameLandmarkId Id
        {
            get
            {
                if (LandmarkIndex >= 0 && LandmarkIndex <= 67)
                {
                    return (FlameLandmarkId)LandmarkIndex;
                }

                return FlameLandmarkId.Unknown;
            }
        }

        /// <summary>
        /// Текстовая метка для дебага/визуализации.
        /// </summary>
        public string Label => Id != FlameLandmarkId.Unknown
            ? Id.ToString()
            : $"LM_{LandmarkIndex}";
    }

    /// <summary>
    /// ScriptableObject с embedding'ом FLAME-лендмарков (обычно 68).
    /// </summary>
    [CreateAssetMenu(
        fileName = "FlameLandmarkEmbedding",
        menuName = "AlSo/FLAME/Landmark Embedding",
        order = 10)]
    public class FlameLandmarkEmbedding : ScriptableObject
    {
        [Tooltip("Сколько лендмарков описано (например, 68).")]
        public int NumLandmarks;

        [Tooltip("Все лендмарки с индексами треугольников и барицентрическими координатами.")]
        public FlameLandmark[] Landmarks;
    }
}