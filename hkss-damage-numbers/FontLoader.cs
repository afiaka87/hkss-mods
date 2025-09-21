using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HKSS.DamageNumbers
{
    public static class FontLoader
    {
        private static readonly Dictionary<string, Font> fontCache = new Dictionary<string, Font>();
        private static Dictionary<string, Font> unityFonts = null;
        private static bool discoveryComplete = false;

        private static readonly Dictionary<string, string[]> fontFamilies = new Dictionary<string, string[]>
        {
            // Basic/Rudimentary fonts (10)
            ["Arial"] = new[] { "Arial", "ArialMT", "Arial-BoldMT" },
            ["Helvetica"] = new[] { "Helvetica", "Helvetica Neue", "HelveticaNeue" },
            ["Verdana"] = new[] { "Verdana", "Verdana-Bold", "Verdana-Regular" },
            ["Tahoma"] = new[] { "Tahoma", "Tahoma-Bold" },
            ["Trebuchet"] = new[] { "Trebuchet MS", "TrebuchetMS", "Trebuchet" },
            ["Calibri"] = new[] { "Calibri", "Calibri-Bold", "CalibriBold" },
            ["Segoe"] = new[] { "Segoe UI", "SegoeUI", "Segoe Print" },
            ["Futura"] = new[] { "Futura", "Futura PT", "FuturaPT" },
            ["Century"] = new[] { "Century Gothic", "CenturyGothic", "Century" },
            ["Franklin"] = new[] { "Franklin Gothic", "FranklinGothic", "Franklin Gothic Medium" },

            // Gothic/Fantasy fonts that work well with Hollow Knight (10)
            ["Trajan"] = new[] { "TrajanPro-Regular", "Trajan Pro", "TrajanPro", "Trajan" },
            ["Georgia"] = new[] { "Georgia", "Georgia-Bold", "Georgia-Regular" },
            ["Times"] = new[] { "Times New Roman", "TimesNewRomanPSMT", "Times" },
            ["Garamond"] = new[] { "Garamond", "EB Garamond", "Adobe Garamond", "AGaramond" },
            ["Baskerville"] = new[] { "Baskerville", "Libre Baskerville", "Baskerville Old Face" },
            ["Palatino"] = new[] { "Palatino", "Palatino Linotype", "Book Antiqua", "URW Palladio" },
            ["Bookman"] = new[] { "Bookman", "Bookman Old Style", "ITC Bookman" },
            ["Perpetua"] = new[] { "Perpetua", "Perpetua Titling", "PerpetuaTitlingMT" },
            ["Copperplate"] = new[] { "Copperplate", "Copperplate Gothic", "CopperplateGothic" },
            ["Didot"] = new[] { "Didot", "Theano Didot", "GFS Didot" }
        };

        public static Font GetFont(string fontName)
        {
            // Check cache first
            if (fontCache.ContainsKey(fontName))
            {
                return fontCache[fontName];
            }

            Font font = null;

            try
            {
                if (fontName == "Default")
                {
                    // Return null to signal that default GUIStyle font should be used
                    // We can't access GUI.skin.font outside of OnGUI context
                    return null;
                }
                else if (fontName == "Unity")
                {
                    // Use Unity's default font from the game
                    font = GetUnityDefaultFont();
                }
                else if (fontName == "Custom")
                {
                    // Load custom font from path
                    string customPath = DamageNumbersPlugin.CustomFontPath.Value;
                    if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
                    {
                        font = LoadFontFromFile(customPath);
                        if (font == null)
                        {
                            DamageNumbersPlugin.Log.LogWarning($"Failed to load custom font from {customPath}, using default");
                            return null; // Use default
                        }
                    }
                    else
                    {
                        DamageNumbersPlugin.Log.LogWarning("Custom font path not specified or file not found, using default");
                        return null; // Use default
                    }
                }
                else
                {
                    // Try to find the font in Unity's loaded fonts first
                    font = FindUnityFont(fontName);

                    if (font == null && fontFamilies.ContainsKey(fontName))
                    {
                        // Fallback to system fonts
                        font = FindSystemFont(fontFamilies[fontName]);
                        if (font == null)
                        {
                            // Fallback to a more stylish default
                            DamageNumbersPlugin.Log.LogInfo($"Font {fontName} not found in system, using stylized default");
                            font = CreateStylizedFont();
                        }
                    }
                    else if (font == null)
                    {
                        // Unknown font name, use default
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                DamageNumbersPlugin.Log.LogError($"Error loading font {fontName}: {ex.Message}");
                return null; // Use default
            }

            // Cache the result
            if (font != null)
            {
                fontCache[fontName] = font;
            }

            return font;
        }

        private static Font FindSystemFont(string[] fontNames)
        {
            var availableFonts = Font.GetOSInstalledFontNames();

            foreach (var name in fontNames)
            {
                // Try exact match first
                if (availableFonts.Contains(name))
                {
                    var font = Font.CreateDynamicFontFromOSFont(name, 24);
                    if (font != null)
                    {
                        DamageNumbersPlugin.Log.LogInfo($"Loaded system font: {name}");
                        return font;
                    }
                }

                // Try case-insensitive partial match
                var match = availableFonts.FirstOrDefault(f => f.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
                if (match != null)
                {
                    var font = Font.CreateDynamicFontFromOSFont(match, 24);
                    if (font != null)
                    {
                        DamageNumbersPlugin.Log.LogInfo($"Loaded system font: {match} (matched {name})");
                        return font;
                    }
                }
            }

            return null;
        }

        private static Font LoadFontFromFile(string path)
        {
            try
            {
                // Read the font file as bytes
                byte[] fontData = File.ReadAllBytes(path);

                // Create a Font object from the data
                // Note: This requires the font to be in a format Unity can understand
                var font = new Font(Path.GetFileNameWithoutExtension(path));

                // Unfortunately, Unity's Font constructor doesn't directly support loading from bytes
                // We'll need to use Resources or AssetBundle for custom fonts
                // For now, we'll try to load as OS font if it's installed
                var fontName = Path.GetFileNameWithoutExtension(path);
                return Font.CreateDynamicFontFromOSFont(fontName, 24);
            }
            catch (Exception ex)
            {
                DamageNumbersPlugin.Log.LogError($"Failed to load font from file {path}: {ex.Message}");
                return null;
            }
        }

        private static Font CreateStylizedFont()
        {
            // Try to find any stylish font that might be available
            string[] stylishFonts = new[]
            {
                "Garamond", "Baskerville", "Palatino", "Book Antiqua",
                "Cambria", "Constantia", "Didot", "Bodoni"
            };

            var availableFonts = Font.GetOSInstalledFontNames();
            foreach (var fontName in stylishFonts)
            {
                var match = availableFonts.FirstOrDefault(f => f.IndexOf(fontName, StringComparison.OrdinalIgnoreCase) >= 0);
                if (match != null)
                {
                    var font = Font.CreateDynamicFontFromOSFont(match, 24);
                    if (font != null)
                    {
                        DamageNumbersPlugin.Log.LogInfo($"Using stylized fallback font: {match}");
                        return font;
                    }
                }
            }

            // Final fallback to null (will use default GUIStyle font)
            return null;
        }

        private static Font FindUnityFont(string fontName)
        {
            DiscoverUnityFonts();

            if (unityFonts == null || unityFonts.Count == 0)
            {
                return null;
            }

            // Try exact match first
            if (unityFonts.ContainsKey(fontName))
            {
                DamageNumbersPlugin.Log.LogInfo($"Found Unity font: {fontName}");
                return unityFonts[fontName];
            }

            // Try case-insensitive partial match
            var match = unityFonts.Keys.FirstOrDefault(name =>
                name.IndexOf(fontName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (match != null)
            {
                DamageNumbersPlugin.Log.LogInfo($"Found Unity font: {match} (matched {fontName})");
                return unityFonts[match];
            }

            return null;
        }

        private static void DiscoverUnityFonts()
        {
            if (discoveryComplete)
                return;

            try
            {
                DamageNumbersPlugin.Log.LogInfo("Discovering Unity fonts...");

                // Find all Font objects loaded in Unity
                Font[] allFonts = Resources.FindObjectsOfTypeAll<Font>();
                unityFonts = new Dictionary<string, Font>(StringComparer.OrdinalIgnoreCase);

                foreach (Font font in allFonts)
                {
                    if (font != null && !string.IsNullOrEmpty(font.name))
                    {
                        // Skip duplicate names (keep first found)
                        if (!unityFonts.ContainsKey(font.name))
                        {
                            unityFonts[font.name] = font;
                            DamageNumbersPlugin.Log.LogInfo($"Unity font discovered: '{font.name}'");
                        }
                    }
                }

                DamageNumbersPlugin.Log.LogInfo($"Unity font discovery complete. Found {unityFonts.Count} fonts.");
                discoveryComplete = true;
            }
            catch (Exception ex)
            {
                DamageNumbersPlugin.Log.LogError($"Error during Unity font discovery: {ex.Message}");
                unityFonts = new Dictionary<string, Font>();
                discoveryComplete = true;
            }
        }

        private static Font GetUnityDefaultFont()
        {
            DiscoverUnityFonts();

            if (unityFonts == null || unityFonts.Count == 0)
            {
                return null;
            }

            // Common Unity default font names to look for
            string[] defaultFontNames = new[]
            {
                "Arial", // Unity's most common default
                "LegacyRuntime", // Unity's built-in
                "Unity Sans", // Unity's default UI font
                "Resources.unity default resources" // Sometimes appears
            };

            foreach (string defaultName in defaultFontNames)
            {
                var font = unityFonts.Values.FirstOrDefault(f =>
                    f.name.IndexOf(defaultName, StringComparison.OrdinalIgnoreCase) >= 0);

                if (font != null)
                {
                    DamageNumbersPlugin.Log.LogInfo($"Using Unity default font: {font.name}");
                    return font;
                }
            }

            // If no specific default found, use the first available Unity font
            var firstFont = unityFonts.Values.FirstOrDefault();
            if (firstFont != null)
            {
                DamageNumbersPlugin.Log.LogInfo($"Using first available Unity font: {firstFont.name}");
                return firstFont;
            }

            return null;
        }

        // Method to get list of available Unity fonts for configuration
        public static string[] GetAvailableUnityFonts()
        {
            DiscoverUnityFonts();
            return unityFonts?.Keys.ToArray() ?? new string[0];
        }

        public static void ClearCache()
        {
            fontCache.Clear();
            unityFonts = null;
            discoveryComplete = false;
        }

        // Performance: Preload fonts to avoid first-hit stutter
        public static void PreloadFont(string fontName)
        {
            // Trigger font discovery early if needed
            if (!discoveryComplete && fontName == "Unity")
            {
                DiscoverUnityFonts();
            }

            // Cache the font early
            GetFont(fontName);
        }
    }
}