#if TOOLS
using Godot;
using System;
using System.IO;

namespace Slime.Addons.DataConfigEditor
{
    /// <summary>
    /// 资源路径编辑框。
    /// 支持拖拽 `res://` / 项目内绝对路径，并对路径存在性做即时校验。
    /// </summary>
    public partial class PathLineEdit : LineEdit
    {
        private readonly Action<string> _onPathChanged;

        public PathLineEdit(string initialText, Action<string> onPathChanged)
        {
            _onPathChanged = onPathChanged;
            Text = initialText;
            TooltipText = initialText;
            TextChanged += OnTextChangedInternal;
            RefreshValidationState(initialText);
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            return !string.IsNullOrWhiteSpace(ExtractPathFromDropData(data));
        }

        public override void _DropData(Vector2 atPosition, Variant data)
        {
            var path = ExtractPathFromDropData(data);
            if (string.IsNullOrWhiteSpace(path))
                return;

            Text = path;
            CaretColumn = Text.Length;
            RefreshValidationState(path);
            _onPathChanged.Invoke(path);
        }

        private void OnTextChangedInternal(string newText)
        {
            RefreshValidationState(newText);
            _onPathChanged.Invoke(newText);
        }

        private void RefreshValidationState(string path)
        {
            var normalizedPath = NormalizePath(path);
            var isValid = IsValidProjectPath(normalizedPath);

            TooltipText = string.IsNullOrWhiteSpace(normalizedPath)
                ? "空路径"
                : isValid
                    ? $"路径有效: {normalizedPath}"
                    : $"路径无效: {normalizedPath}";

            AddThemeColorOverride("font_color", isValid
                ? new Color(0.70f, 0.84f, 0.98f)
                : new Color(1.0f, 0.60f, 0.60f));
        }

        private static string ExtractPathFromDropData(Variant data)
        {
            if (data.VariantType == Variant.Type.String)
                return NormalizePath(data.AsString());

            if (data.VariantType == Variant.Type.PackedStringArray)
            {
                var paths = data.AsStringArray();
                return paths.Length > 0 ? NormalizePath(paths[0]) : "";
            }

            if (data.VariantType == Variant.Type.Dictionary)
            {
                var dict = data.AsGodotDictionary();
                if (dict.ContainsKey("files"))
                {
                    var filesVariant = (Variant)dict["files"];
                    if (filesVariant.VariantType == Variant.Type.PackedStringArray)
                    {
                        var paths = filesVariant.AsStringArray();
                        return paths.Length > 0 ? NormalizePath(paths[0]) : "";
                    }
                }

                if (dict.ContainsKey("path"))
                {
                    var pathVariant = (Variant)dict["path"];
                    if (pathVariant.VariantType == Variant.Type.String)
                        return NormalizePath(pathVariant.AsString());
                }
            }

            return "";
        }

        public static string NormalizePath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
                return "";

            var normalized = rawPath.Replace('\\', '/').Trim();
            if (normalized.StartsWith("res://", StringComparison.Ordinal))
                return normalized;

            var localized = ProjectSettings.LocalizePath(normalized);
            return localized.StartsWith("res://", StringComparison.Ordinal) ? localized : normalized;
        }

        private static bool IsValidProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return true;

            if (path.StartsWith("res://", StringComparison.Ordinal))
            {
                if (ResourceLoader.Exists(path))
                    return true;

                var globalPath = ProjectSettings.GlobalizePath(path);
                return File.Exists(globalPath) || Directory.Exists(globalPath);
            }

            return File.Exists(path) || Directory.Exists(path);
        }
    }
}
#endif
