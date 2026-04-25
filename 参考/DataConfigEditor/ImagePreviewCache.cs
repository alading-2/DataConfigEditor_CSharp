#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.IO;

namespace Slime.Addons.DataConfigEditor
{
    /// <summary>
    /// 图片预览缓存管理器
    /// 同步加载缩略图并缓存，避免重复加载
    /// </summary>
    public class ImagePreviewCache
    {
        private readonly Dictionary<string, Texture2D?> _cache = new(StringComparer.Ordinal);
        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".svg", ".webp" };

        /// <summary>
        /// 获取路径对应的缩略图，如果未缓存则尝试加载
        /// </summary>
        public Texture2D? GetOrLoad(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            if (_cache.TryGetValue(path, out var cached))
                return cached;

            Texture2D? result = null;
            try
            {
                if (ResourceLoader.Exists(path))
                {
                    // 图片文件直接加载为 Texture2D
                    var ext = Path.GetExtension(path).ToLowerInvariant();
                    if (Array.IndexOf(ImageExtensions, ext) >= 0)
                        result = ResourceLoader.Load<Texture2D>(path);
                }
                else
                {
                    // 尝试绝对路径转换
                    var globalPath = ProjectSettings.GlobalizePath(path);
                    if (File.Exists(globalPath))
                    {
                        var image = Image.LoadFromFile(globalPath);
                        if (image != null)
                        {
                            var tex = ImageTexture.CreateFromImage(image);
                            if (tex != null)
                                result = tex;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[DataConfigEditor] 加载预览失败 {path}: {e.Message}");
            }

            _cache[path] = result;
            return result;
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }
    }
}
#endif
