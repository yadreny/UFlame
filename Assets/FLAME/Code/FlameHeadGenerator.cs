#if UNITY_EDITOR

using System;
using System.Diagnostics;
using System.IO;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace AlSo
{
    /// <summary>
    /// Монобех: висит на объекте, хранит настройки головы и по одиновской кнопке
    /// вызывает Python в локальном venv внутри проекта, генерируя OBJ.
    /// </summary>
    public class FlameHeadGenerator : MonoBehaviour
    {
        [FoldoutGroup("Python")]
        [Tooltip("Относительный путь до python.exe внутри venv (относительно корня проекта) или абсолютный.")]
        public string pythonRelativePath = @"FlameBridge/flame_env/Scripts/python.exe";

        [FoldoutGroup("Python")]
        [Tooltip("Относительный путь до generate_flame_mesh.py (относительно корня проекта) или абсолютный.")]
        public string generateScriptRelativePath = @"FlameBridge/FLAME_PyTorch/scripts/generate_flame_mesh.py";

        [FoldoutGroup("Config")]
        [Tooltip("Сериализуемый конфиг FLAME-головы.")]
        public FlameHeadConfigData config = new FlameHeadConfigData();

        [FoldoutGroup("Config")]
        [Tooltip("Имя JSON-файла с конфигом, который пишется рядом с generate_flame_mesh.py.")]
        public string configFileName = "flame_head_config.json";

        /// <summary>
        /// Одиновская кнопка для перегенерации головы.
        /// </summary>
        [FoldoutGroup("Actions")]
        [Button("Regenerate FLAME Head")]
        public void RegenerateHead()
        {
            RegenerateHeadInternal();
        }

        private void RegenerateHeadInternal()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            // python.exe из venv: относительный → абсолютный
            string pythonExeFullPath = Path.IsPathRooted(pythonRelativePath)
                ? pythonRelativePath
                : Path.GetFullPath(Path.Combine(projectRoot, pythonRelativePath));

            // путь к generate_flame_mesh.py
            string generateScriptFullPath = Path.IsPathRooted(generateScriptRelativePath)
                ? generateScriptRelativePath
                : Path.GetFullPath(Path.Combine(projectRoot, generateScriptRelativePath));

            if (!File.Exists(pythonExeFullPath))
            {
                UnityEngine.Debug.LogError($"[FlameHeadGenerator] python.exe not found at: {pythonExeFullPath}");
                return;
            }

            if (!File.Exists(generateScriptFullPath))
            {
                UnityEngine.Debug.LogError($"[FlameHeadGenerator] generate_flame_mesh.py not found at: {generateScriptFullPath}");
                return;
            }

            // JSON-конфиг лежит рядом с generate_flame_mesh.py
            string cfgDir = Path.GetDirectoryName(generateScriptFullPath) ?? projectRoot;
            string configPath = Path.Combine(cfgDir, configFileName);
            config.SaveToFile(configPath);

            UnityEngine.Debug.Log($"[FlameHeadGenerator] ProjectRoot: {projectRoot}");
            UnityEngine.Debug.Log($"[FlameHeadGenerator] Python:     {pythonExeFullPath}");
            UnityEngine.Debug.Log($"[FlameHeadGenerator] Script:     {generateScriptFullPath}");
            UnityEngine.Debug.Log($"[FlameHeadGenerator] Config:     {configPath}");
            UnityEngine.Debug.Log($"[FlameHeadGenerator] OBJ path (Unity string): {config.outObjAssetPath}");

            var psi = new ProcessStartInfo
            {
                FileName = pythonExeFullPath,
                Arguments = $"\"{generateScriptFullPath}\" \"{configPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = cfgDir
            };

            try
            {
                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        UnityEngine.Debug.LogError("[FlameHeadGenerator] Failed to start python process.");
                        return;
                    }

                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(stdout))
                    {
                        UnityEngine.Debug.Log("[FlameHeadGenerator] Python stdout:\n" + stdout);
                    }

                    if (!string.IsNullOrEmpty(stderr))
                    {
                        UnityEngine.Debug.LogWarning("[FlameHeadGenerator] Python stderr:\n" + stderr);
                    }

                    if (process.ExitCode != 0)
                    {
                        UnityEngine.Debug.LogError($"[FlameHeadGenerator] Python exited with code {process.ExitCode}");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[FlameHeadGenerator] Error running python: {ex}");
                return;
            }

            // Точечный реимпорт OBJ, если он в Assets
            string objPath = config.outObjAssetPath;
            if (string.IsNullOrEmpty(objPath))
            {
                UnityEngine.Debug.LogWarning("[FlameHeadGenerator] outObjAssetPath is empty, nothing to reimport.");
                return;
            }

            // Если пришёл абсолютный путь — делаем его относительным к projectRoot
            if (Path.IsPathRooted(objPath))
            {
                string full = Path.GetFullPath(objPath);
                if (full.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    objPath = full.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
            }

            if (!objPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                UnityEngine.Debug.LogWarning($"[FlameHeadGenerator] OBJ path '{objPath}' does not start with 'Assets', skip reimport.");
                return;
            }

            UnityEditor.AssetDatabase.ImportAsset(objPath, ImportAssetOptions.ForceUpdate);
            UnityEngine.Debug.Log($"[FlameHeadGenerator] Reimported OBJ asset: {objPath}");
        }
    }
}

#endif
