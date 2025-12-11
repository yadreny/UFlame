#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AlSo
{
    /// <summary>
    /// Импортёр FLAME-лендмарков (68 точек) из JSON,
    /// который мы получили из landmark_embedding.npy.
    ///
    /// Ожидаемый формат JSON (см. export_flame_landmarks_to_json.py):
    /// {
    ///   "static_lmk_faces_idx": [51],
    ///   "static_lmk_bary_coords": [[51][3]],
    ///   "dynamic_lmk_faces_idx": [[79][17]],
    ///   "dynamic_lmk_bary_coords": [[[79][17][3]]],
    ///   "full_lmk_faces_idx": [[68]],
    ///   "full_lmk_bary_coords": [[[68][3]]]
    /// }
    ///
    /// Мы используем только full_* (68 "полных" лендмарков).
    /// Создаёт/обновляет ассет FlameLandmarkEmbedding по пути:
    ///   Assets/FLAME/FlameLandmarkEmbedding.asset
    /// </summary>
    public static class FlameLandmarkEmbeddingImporter
    {
        private const string DefaultJsonPath =
            @"D:\work25\flame\FlameBridge\Analysis\flame_canonical_landmarks.json";

        private const string DefaultAssetPath = "Assets/FLAME/FlameLandmarkEmbedding.asset";

        [MenuItem("AlSo/FLAME/Import Canonical FLAME 68 Landmarks (from JSON)")]
        public static void ImportFromJson()
        {
            if (!File.Exists(DefaultJsonPath))
            {
                UnityEngine.Debug.LogError(
                    "[FlameLandmarkEmbeddingImporter] JSON file not found at path: " + DefaultJsonPath);
                return;
            }

            string json;
            try
            {
                json = File.ReadAllText(DefaultJsonPath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError(
                    "[FlameLandmarkEmbeddingImporter] Failed to read JSON file: " + DefaultJsonPath + "\n" + ex);
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
                    "[FlameLandmarkEmbeddingImporter] Failed to parse JSON or create asset.\n" + ex);
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
            string facesArray2D = ExtractArrayString(json, "full_lmk_faces_idx");
            int[] faces = ParseIntArrayFromArrayString(facesArray2D);

            string baryArray3D = ExtractArrayString(json, "full_lmk_bary_coords");
            string baryInner = StripOuterBrackets(baryArray3D);
            Vector3[] bary = ParseBaryArrayFromArrayString(baryInner);

            int num = faces.Length;
            if (bary.Length != num)
            {
                throw new Exception(
                    "[FlameLandmarkEmbeddingImporter] Mismatch: faces=" +
                    faces.Length + ", bary=" + bary.Length);
            }

            int[] landmarkIdx = new int[num];
            for (int i = 0; i < num; i++)
            {
                landmarkIdx[i] = i;
            }

            return new RawEmbedding
            {
                NumLandmarks = num,
                FaceIndices = faces,
                LandmarkIndices = landmarkIdx,
                BaryCoords = bary
            };
        }

        private static string ExtractArrayString(string json, string key)
        {
            string keyPattern = "\"" + key + "\"";
            int keyPos = json.IndexOf(keyPattern, StringComparison.Ordinal);
            if (keyPos < 0)
            {
                throw new Exception("[FlameLandmarkEmbeddingImporter] Key '" + key + "' not found in JSON.");
            }

            int bracketStart = json.IndexOf('[', keyPos);
            if (bracketStart < 0)
            {
                throw new Exception("[FlameLandmarkEmbeddingImporter] '[' not found after key '" + key + "'.");
            }

            int depth = 0;
            for (int i = bracketStart; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '[')
                    depth++;
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0)
                        return json.Substring(bracketStart, i - bracketStart + 1);
                }
            }

            throw new Exception(
                "[FlameLandmarkEmbeddingImporter] Could not find matching ']' for array '" + key + "'.");
        }

        private static string StripOuterBrackets(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            int first = s.IndexOf('[');
            int last = s.LastIndexOf(']');
            if (first < 0 || last <= first)
                return s;

            return s.Substring(first + 1, last - first - 1);
        }

        private static int[] ParseIntArrayFromArrayString(string arrayStr)
        {
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

        private static Vector3[] ParseBaryArrayFromArrayString(string arrayStr)
        {
            List<Vector3> result = new List<Vector3>();

            int depth = 0;
            List<float> currentFloats = null;
            StringBuilder num = new StringBuilder();

            Action flushNumber = () =>
            {
                if (num.Length == 0 || currentFloats == null)
                    return;

                if (float.TryParse(num.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                {
                    currentFloats.Add(val);
                }

                num.Length = 0;
            };

            foreach (char c in arrayStr)
            {
                if (c == '[')
                {
                    depth++;
                    if (depth == 2)
                    {
                        currentFloats = new List<float>();
                        num.Length = 0;
                    }
                }
                else if (c == ']')
                {
                    if (depth == 2)
                    {
                        flushNumber();
                        if (currentFloats == null || currentFloats.Count < 3)
                            throw new Exception(
                                "[FlameLandmarkEmbeddingImporter] Barycentric sub-array does not have 3 elements.");

                        result.Add(new Vector3(currentFloats[0], currentFloats[1], currentFloats[2]));
                        currentFloats = null;
                    }

                    depth--;
                }
                else
                {
                    if (depth == 2)
                    {
                        if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
                            num.Append(c);
                        else
                            flushNumber();
                    }
                }
            }

            return result.ToArray();
        }

        private static void CreateOrUpdateAsset(RawEmbedding raw)
        {
            string dir = Path.GetDirectoryName(DefaultAssetPath);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                string[] parts = dir.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
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
                    asset.Landmarks[i] = new FlameLandmark
                    {
                        FaceIndex = raw.FaceIndices[i],
                        LandmarkIndex = raw.LandmarkIndices[i],
                        Barycentric = raw.BaryCoords[i]
                    };
                }

                AssetDatabase.CreateAsset(asset, DefaultAssetPath);
                AssetDatabase.ImportAsset(DefaultAssetPath, ImportAssetOptions.ForceUpdate);

                UnityEngine.Debug.Log(
                    "[FlameLandmarkEmbeddingImporter] Created new FlameLandmarkEmbedding asset at: " + DefaultAssetPath);
            }
            else
            {
                asset.NumLandmarks = raw.NumLandmarks;
                asset.Landmarks = new FlameLandmark[raw.NumLandmarks];

                for (int i = 0; i < raw.NumLandmarks; i++)
                {
                    asset.Landmarks[i] = new FlameLandmark
                    {
                        FaceIndex = raw.FaceIndices[i],
                        LandmarkIndex = raw.LandmarkIndices[i],
                        Barycentric = raw.BaryCoords[i]
                    };
                }

                EditorUtility.SetDirty(asset);
                AssetDatabase.ImportAsset(DefaultAssetPath, ImportAssetOptions.ForceUpdate);

                UnityEngine.Debug.Log(
                    "[FlameLandmarkEmbeddingImporter] Updated existing FlameLandmarkEmbedding asset at: " + DefaultAssetPath);
            }
        }
    }
}
#endif