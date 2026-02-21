using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace StartingClassMod
{
    /// <summary>
    /// Loads PNG textures embedded in the assembly as EmbeddedResources.
    /// Resources are expected at: Resources/Textures/Abilities/{Name}.png
    /// Embedded resource names follow the pattern: StartingClassMod.Resources.Textures.Abilities.{Name}.png
    /// Uses reflection to call ImageConversion.LoadImage (avoids netstandard version mismatch).
    /// </summary>
    public static class TextureLoader
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();
        private static readonly Assembly ModAssembly = Assembly.GetExecutingAssembly();

        // ImageConversion.LoadImage(Texture2D, byte[]) via reflection
        private static readonly MethodInfo LoadImageMethod = ResolveLoadImage();

        private static MethodInfo ResolveLoadImage()
        {
            var type = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            return type?.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
        }

        /// <summary>
        /// Load an ability icon texture by name (e.g. "MarkedByFate").
        /// Returns null if the resource is not found.
        /// </summary>
        public static Texture2D LoadAbilityIcon(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            if (Cache.TryGetValue(name, out Texture2D cached))
                return cached;

            if (LoadImageMethod == null)
            {
                StartingClassPlugin.LogWarning("TextureLoader: ImageConversion.LoadImage not found via reflection.");
                return null;
            }

            string resourceName = $"StartingClassMod.Resources.Textures.Abilities.{name}.png";
            using (Stream stream = ModAssembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    StartingClassPlugin.LogWarning($"TextureLoader: Embedded resource '{resourceName}' not found.");
                    return null;
                }

                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                bool loaded = (bool)LoadImageMethod.Invoke(null, new object[] { tex, data });
                if (loaded)
                {
                    tex.name = name;
                    Cache[name] = tex;
                    return tex;
                }

                UnityEngine.Object.Destroy(tex);
                StartingClassPlugin.LogWarning($"TextureLoader: Failed to decode '{resourceName}'.");
                return null;
            }
        }

        /// <summary>
        /// Create (or return cached) a Sprite from a loaded ability icon texture.
        /// Sprites are cached for the session lifetime so multiple callers share the same object.
        /// </summary>
        public static Sprite LoadAbilitySprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            if (SpriteCache.TryGetValue(name, out Sprite cached))
                return cached;

            Texture2D tex = LoadAbilityIcon(name);
            if (tex == null) return null;

            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            SpriteCache[name] = sprite;
            return sprite;
        }
    }
}
