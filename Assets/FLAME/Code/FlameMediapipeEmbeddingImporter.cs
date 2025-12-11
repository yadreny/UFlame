#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AlSo
{
    /// <summary>
    /// Импортёр JSON-embedding Mediapipe-лендмарков в ScriptableObject.
    /// </summary>
    public static class FlameMediapipeEmbeddingImporter
    {
        // Жёстко заданный путь к JSON, как ты просила:
        // D:\work25\flame\FlameBridge\Analysis\mediapipe_landmark_embedding.json
        private const string DefaultJsonPath =
            @"D:\work25\flame\FlameBridge\Analysis\mediapipe_landmark_embedding.json";

        // Куда сохранять ассет
        private const string DefaultAssetPath = "Assets/FLAME/MediapipeLandmarkEmbedding.asset";

        [MenuItem("AlSo/FLAME/Import Mediapipe Landmark Embedding (from JSON)")]
        public static void ImportFromJson()
        {
            string jsonPath = DefaultJsonPath;

            if (!File.Exists(jsonPath))
            {
                UnityEngine.Debug.LogError(
                    $"[FlameMediapipeEmbeddingImporter] JSON file not found at path: {jsonPath}");
                return;
            }

            string json;
            try
            {
                json = File.ReadAllText(jsonPath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError(
                    $"[FlameMediapipeEmbeddingImporter] Failed to read JSON file: {jsonPath}\n{ex}");
                return;
            }

            try
            {
                RawEmbedding raw = ParseRawEmbedding(json);
                CreateOrUpdateAsset(raw);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError(
                    $"[FlameMediapipeEmbeddingImporter] Failed to parse JSON or create asset.\n{ex}");
            }
        }

        private class RawEmbedding
        {
            public int NumLandmarks;
            public int[] FaceIndices;
            public int[] LandmarkIndices;
            public Vector3[] BaryCoords;
        }

        private static RawEmbedding ParseRawEmbedding(string json)
        {
            // Ожидаемый формат:
            //
            // {
            //   "num_landmarks": 105,
            //   "lmk_face_idx": [ ... ],
            //   "lmk_b_coords": [ [a,b,c], [d,e,f], ...],
            //   "landmark_indices": [ ... ]
            // }

            int numLandmarks = ParseIntValue(json, "num_landmarks");
            int[] faceIdx = ParseIntArray(json, "lmk_face_idx");
            int[] landmarkIdx = ParseIntArray(json, "landmark_indices");
            Vector3[] baryCoords = ParseBaryArray(json, "lmk_b_coords");

            if (faceIdx.Length != numLandmarks ||
                landmarkIdx.Length != numLandmarks ||
                baryCoords.Length != numLandmarks)
            {
                throw new Exception(
                    $"[FlameMediapipeEmbeddingImporter] Mismatch: num_landmarks={numLandmarks}, " +
                    $"faceIdx={faceIdx.Length}, landmarkIdx={landmarkIdx.Length}, baryCoords={baryCoords.Length}");
            }

            RawEmbedding result = new RawEmbedding
            {
                NumLandmarks = numLandmarks,
                FaceIndices = faceIdx,
                LandmarkIndices = landmarkIdx,
                BaryCoords = baryCoords
            };
            return result;
        }

        private static int ParseIntValue(string json, string key)
        {
            // ищем "key": <int>
            string pattern = "\"" + key + "\"\\s*:\\s*([-0-9]+)";
            Match m = Regex.Match(json, pattern);
            if (!m.Success)
            {
                throw new Exception($"[FlameMediapipeEmbeddingImporter] Key '{key}' not found or not an int.");
            }

            string valueStr = m.Groups[1].Value;
            if (!int.TryParse(valueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int val))
            {
                throw new Exception($"[FlameMediapipeEmbeddingImporter] Failed to parse int value for '{key}'.");
            }

            return val;
        }

        private static int[] ParseIntArray(string json, string key)
        {
            string arrayStr = ExtractArrayString(json, key);

            // arrayStr вида: [1, 2, 3, 4, ...]
            List<int> result = new List<int>();
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < arrayStr.Length; i++)
            {
                char c = arrayStr[i];
                if ((c >= '0' && c <= '9') || c == '-' || c == '+')
                {
                    sb.Append(c);
                }
                else
                {
                    if (sb.Length > 0)
                    {
                        string s = sb.ToString();
                        sb.Length = 0;
                        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int val))
                        {
                            result.Add(val);
                        }
                    }
                }
            }

            // вдруг в конце что-то накопилось
            if (sb.Length > 0)
            {
                string s = sb.ToString();
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int val))
                {
                    result.Add(val);
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Парсит массив барицентрических координат:
        /// "lmk_b_coords": [ [a,b,c], [d,e,f], ... ]
        /// Возвращает Vector3[numLandmarks], каждый с (a,b,c).
        /// </summary>
        private static Vector3[] ParseBaryArray(string json, string key)
        {
            // Вытаскиваем строку вида:
            // "lmk_b_coords": [ [a,b,c], [d,e,f], ... ]
            string arrayStr = ExtractArrayString(json, key);

            var result = new List<Vector3>();

            int depth = 0;
            List<float> currentFloats = null;
            var num = new StringBuilder();

            void FlushNumber()
            {
                if (num.Length == 0 || currentFloats == null)
                    return;

                if (float.TryParse(num.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                {
                    currentFloats.Add(val);
                }

                num.Length = 0;
            }

            foreach (char c in arrayStr)
            {
                if (c == '[')
                {
                    depth++;

                    // depth == 1 -> это внешний [ ... ]
                    // depth == 2 -> это внутренний [a,b,c] для одного лендмарка
                    if (depth == 2)
                    {
                        currentFloats = new List<float>();
                        num.Length = 0;
                    }
                }
                else if (c == ']')
                {
                    // перед закрытием скобки сбрасываем накопленное число
                    if (depth == 2)
                    {
                        FlushNumber();

                        if (currentFloats == null || currentFloats.Count < 3)
                        {
                            throw new Exception(
                                "[FlameMediapipeEmbeddingImporter] Barycentric sub-array does not have 3 elements.");
                        }

                        // Берём только первые три компоненты
                        result.Add(new Vector3(currentFloats[0], currentFloats[1], currentFloats[2]));
                        currentFloats = null;
                    }

                    depth--;
                }
                else
                {
                    // Читаем числа только на глубине == 2 (внутри конкретного [a,b,c])
                    if (depth == 2)
                    {
                        if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
                        {
                            num.Append(c);
                        }
                        else
                        {
                            // любой разделитель (пробел, запятая, \n, \r, и т.п.)
                            FlushNumber();
                        }
                    }
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Вытаскивает JSON-массив (в том числе вложенный) по ключу, с учётом вложенных скобок.
        /// Возвращает строку начиная с '[' и заканчивая соответствующей ']'.
        /// </summary>
        private static string ExtractArrayString(string json, string key)
        {
            string keyPattern = "\"" + key + "\"";
            int keyPos = json.IndexOf(keyPattern, StringComparison.Ordinal);
            if (keyPos < 0)
            {
                throw new Exception($"[FlameMediapipeEmbeddingImporter] Key '{key}' not found in JSON.");
            }

            int bracketStart = json.IndexOf('[', keyPos);
            if (bracketStart < 0)
            {
                throw new Exception($"[FlameMediapipeEmbeddingImporter] '[' not found after key '{key}'.");
            }

            int depth = 0;
            for (int i = bracketStart; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        // включаем закрывающую ']'
                        return json.Substring(bracketStart, i - bracketStart + 1);
                    }
                }
            }

            throw new Exception($"[FlameMediapipeEmbeddingImporter] Could not find matching ']' for array '{key}'.");
        }

        private static void CreateOrUpdateAsset(RawEmbedding raw)
        {
            // Создаём папку Assets/FLAME, если её нет
            string dir = Path.GetDirectoryName(DefaultAssetPath);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                // Разбиваем путь Assets/FLAME/Smth
                string[] parts = dir.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }

            FlameLandmarkEmbedding asset =
                AssetDatabase.LoadAssetAtPath<FlameLandmarkEmbedding>(DefaultAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<FlameLandmarkEmbedding>();
                asset.NumLandmarks = raw.NumLandmarks;
                asset.Landmarks = new FlameLandmark[raw.NumLandmarks];

                for (int i = 0; i < raw.NumLandmarks; i++)
                {
                    FlameLandmark lm = new FlameLandmark
                    {
                        FaceIndex = raw.FaceIndices[i],
                        LandmarkIndex = raw.LandmarkIndices[i],
                        Barycentric = raw.BaryCoords[i]
                    };
                    asset.Landmarks[i] = lm;
                }

                AssetDatabase.CreateAsset(asset, DefaultAssetPath);
                AssetDatabase.ImportAsset(DefaultAssetPath, ImportAssetOptions.ForceUpdate);

                UnityEngine.Debug.Log(
                    $"[FlameMediapipeEmbeddingImporter] Created new FlameLandmarkEmbedding asset at: {DefaultAssetPath}");
            }
            else
            {
                asset.NumLandmarks = raw.NumLandmarks;
                asset.Landmarks = new FlameLandmark[raw.NumLandmarks];

                for (int i = 0; i < raw.NumLandmarks; i++)
                {
                    FlameLandmark lm = new FlameLandmark
                    {
                        FaceIndex = raw.FaceIndices[i],
                        LandmarkIndex = raw.LandmarkIndices[i],
                        Barycentric = raw.BaryCoords[i]
                    };
                    asset.Landmarks[i] = lm;
                }

                EditorUtility.SetDirty(asset);
                AssetDatabase.ImportAsset(DefaultAssetPath, ImportAssetOptions.ForceUpdate);

                UnityEngine.Debug.Log(
                    $"[FlameMediapipeEmbeddingImporter] Updated existing FlameLandmarkEmbedding asset at: {DefaultAssetPath}");
            }
        }
    }
}
#endif