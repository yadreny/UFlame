using Sirenix.OdinInspector;
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace AlSo
{
    /// <summary>
    /// Сериализуемый конфиг для FLAME-головы.
    /// Умеет явно конвертиться в текст (JSON) и сохраняться в файл.
    /// </summary>
    [Serializable]
    public class FlameHeadConfigData
    {
        [Tooltip("Путь к файлу модели FLAME (обычно flame2023.pkl). Относительно корня проекта или абсолютный.")]
        public string flameModelRelativePath = "FlameBridge/models/flame2023.pkl";

        [Tooltip("Путь до OBJ-шаблона с UV. Относительно корня проекта (например Assets/FLAME/UV_Knower.obj) или абсолютный.")]
        public string uvTemplateRelativePath = "Assets/FLAME/UV_Knower.obj";

        [Tooltip("Количество shape-компонент (обычно 300).")]
        public int numShape = 300;

        [Tooltip("Количество expression-компонент (обычно 100).")]
        public int numExpr = 100;

        [HideInInspector]
        public float[] shapeCoeffs = new float[300];

        [HideInInspector]
        public float[] exprCoeffs = new float[100];

        [PropertyRange(-3f, 3f)]
        [LabelText("Shape[0]")]
        public float shape0;

        [PropertyRange(-3f, 3f)]
        [LabelText("Expr[0]")]
        public float expr0;

        [Tooltip("Unity-путь к выходному OBJ внутри проекта (например: Assets/FLAME/Generated/head_flame.obj) или абсолютный путь.")]
        public string outObjAssetPath = "Assets/FLAME/Generated/head_flame.obj";

        [Tooltip("Необязательный список индексов треугольников (по массиву faces FLAME), "
                 + "которые должны быть вынесены во второй сабмеш (например, для глаз). Пока только заготовка.")]
        public int[] eyeTriangles;

        private void SyncTestFieldsToArrays()
        {
            if (shapeCoeffs == null || shapeCoeffs.Length != numShape)
            {
                shapeCoeffs = new float[numShape];
            }

            if (exprCoeffs == null || exprCoeffs.Length != numExpr)
            {
                exprCoeffs = new float[numExpr];
            }

            if (numShape > 0)
            {
                shapeCoeffs[0] = shape0;
            }

            if (numExpr > 0)
            {
                exprCoeffs[0] = expr0;
            }
        }

        public string ToText()
        {
            SyncTestFieldsToArrays();

            // Корень проекта: папка выше Assets.
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            // FLAME model: относительный → абсолютный; если уже абсолютный — не трогаем.
            string flameModelFullPath = Path.IsPathRooted(flameModelRelativePath)
                ? flameModelRelativePath
                : Path.GetFullPath(Path.Combine(projectRoot, flameModelRelativePath));

            // UV template OBJ: относительный → абсолютный; если пусто или не задано — null в JSON.
            string uvTemplateFullPath = null;
            if (!string.IsNullOrWhiteSpace(uvTemplateRelativePath))
            {
                uvTemplateFullPath = Path.IsPathRooted(uvTemplateRelativePath)
                    ? uvTemplateRelativePath
                    : Path.GetFullPath(Path.Combine(projectRoot, uvTemplateRelativePath));
            }

            // out OBJ: Unity-путь (Assets/...) → абсолютный; если уже абсолютный — не трогаем.
            string outObjFullPath = Path.IsPathRooted(outObjAssetPath)
                ? outObjAssetPath
                : Path.GetFullPath(Path.Combine(projectRoot, outObjAssetPath));

            var dto = new FlameHeadConfigDto
            {
                flame_model_path = flameModelFullPath.Replace("\\", "/"),
                uv_template_obj_path = uvTemplateFullPath != null
                    ? uvTemplateFullPath.Replace("\\", "/")
                    : null,
                num_shape = numShape,
                num_expr = numExpr,
                shape_coeffs = shapeCoeffs,
                expr_coeffs = exprCoeffs,
                out_obj_path = outObjFullPath.Replace("\\", "/"),
                eye_tri_indices = eyeTriangles != null
                    ? (int[])eyeTriangles.Clone()
                    : Array.Empty<int>()
            };

            return JsonUtility.ToJson(dto, true);
        }

        public void SaveToFile(string path)
        {
            string json = ToText();

            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Пишем UTF-8 (с BOM); Python читает utf-8-sig.
            File.WriteAllText(path, json, Encoding.UTF8);
            UnityEngine.Debug.Log($"[FlameHeadConfigData] Saved config JSON to: {path}");
        }

        [Serializable]
        private class FlameHeadConfigDto
        {
            public string flame_model_path;
            public string uv_template_obj_path;
            public int num_shape;
            public int num_expr;
            public float[] shape_coeffs;
            public float[] expr_coeffs;
            public string out_obj_path;
            public int[] eye_tri_indices;
        }
    }
}
