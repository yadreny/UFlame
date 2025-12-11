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

        [Tooltip("Количество shape-компонент (обычно 300).")]
        public int numShape = 300;

        [Tooltip("Количество expression-компонент (обычно 100).")]
        public int numExpr = 100;

        // Прячем реальные массивы от Odin, чтобы он не пытался рисовать их CollectionDrawer'ом.
        [HideInInspector]
        public float[] shapeCoeffs = new float[300];

        [HideInInspector]
        public float[] exprCoeffs = new float[100];

        // Тестовые поля для быстрой проверки: пишутся в 0-й коэффициенты
        [PropertyRange(-3f, 3f)]
        [LabelText("Shape[0]")]
        public float shape0;

        [PropertyRange(-3f, 3f)]
        [LabelText("Expr[0]")]
        public float expr0;

        [Tooltip("Unity-путь к выходному OBJ внутри проекта (например: Assets/FLAME/Generated/head_flame.obj) или абсолютный путь.")]
        public string outObjAssetPath = "Assets/FLAME/Generated/head_flame.obj";

        /// <summary>
        /// Гарантируем, что массивы нужного размера и shape0/expr0 лежат в нулевых элементах.
        /// </summary>
        private void SyncTestFieldsToArrays()
        {
            if (shapeCoeffs == null || shapeCoeffs.Length != numShape)
                shapeCoeffs = new float[numShape];

            if (exprCoeffs == null || exprCoeffs.Length != numExpr)
                exprCoeffs = new float[numExpr];

            if (numShape > 0)
                shapeCoeffs[0] = shape0;

            if (numExpr > 0)
                exprCoeffs[0] = expr0;
        }

        /// <summary>
        /// Конвертит конфиг в текст (JSON) для Python.
        /// </summary>
        public string ToText()
        {
            SyncTestFieldsToArrays();

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            // FLAME model: относительный → абсолютный, абсолютный оставляем как есть.
            string flameModelFullPath = Path.IsPathRooted(flameModelRelativePath)
                ? flameModelRelativePath
                : Path.GetFullPath(Path.Combine(projectRoot, flameModelRelativePath));

            // out OBJ: Unity-путь (Assets/...) → абсолютный; если уже абсолютный — не трогаем.
            string outObjFullPath = Path.IsPathRooted(outObjAssetPath)
                ? outObjAssetPath
                : Path.GetFullPath(Path.Combine(projectRoot, outObjAssetPath));

            var dto = new FlameHeadConfigDto
            {
                flame_model_path = flameModelFullPath.Replace("\\", "/"),
                num_shape = numShape,
                num_expr = numExpr,
                shape_coeffs = shapeCoeffs,
                expr_coeffs = exprCoeffs,
                out_obj_path = outObjFullPath.Replace("\\", "/")
            };

            return JsonUtility.ToJson(dto, true);
        }

        /// <summary>
        /// Сохраняет конфиг как текстовый файл (JSON) по указанному пути.
        /// </summary>
        public void SaveToFile(string path)
        {
            string json = ToText();

            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Было так — с BOM:
            // File.WriteAllText(path, json, Encoding.UTF8);

            // Делаем UTF-8 без BOM:
            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            File.WriteAllText(path, json, utf8NoBom);

            UnityEngine.Debug.Log($"[FlameHeadConfigData] Saved config JSON to: {path}");
        }

        [Serializable]
        private class FlameHeadConfigDto
        {
            public string flame_model_path;
            public int num_shape;
            public int num_expr;
            public float[] shape_coeffs;
            public float[] expr_coeffs;
            public string out_obj_path;
        }
    }

}