using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    /// <summary>
    /// Manages theme switching between light and dark modes.
    /// </summary>
    public static class ThemeManager
    {
        private const string PREF_KEY = "TaskCanvas_Theme";
        private const string DARK_THEME = "dark";
        private const string LIGHT_THEME = "light";

        private static StyleSheet _darkStyleSheet;
        private static StyleSheet _lightStyleSheet;

        public static bool IsDarkTheme
        {
            get => EditorPrefs.GetString(PREF_KEY, DARK_THEME) == DARK_THEME;
            set => EditorPrefs.SetString(PREF_KEY, value ? DARK_THEME : LIGHT_THEME);
        }

        public static StyleSheet GetCurrentStyleSheet()
        {
            LoadStyleSheets();
            return IsDarkTheme ? _darkStyleSheet : _lightStyleSheet;
        }

        public static void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
        }

        public static string GetThemeIcon()
        {
            return IsDarkTheme ? "☀️" : "🌙";
        }

        private static void LoadStyleSheets()
        {
            if (_darkStyleSheet == null)
            {
                var guids = AssetDatabase.FindAssets("Styles-Dark t:StyleSheet");
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _darkStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                }
            }

            if (_lightStyleSheet == null)
            {
                var guids = AssetDatabase.FindAssets("Styles-Light t:StyleSheet");
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _lightStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                }
            }
        }

        public static void ApplyTheme(VisualElement root)
        {
            LoadStyleSheets();

            // Remove old stylesheets
            if (_darkStyleSheet != null && root.styleSheets.Contains(_darkStyleSheet))
                root.styleSheets.Remove(_darkStyleSheet);
            if (_lightStyleSheet != null && root.styleSheets.Contains(_lightStyleSheet))
                root.styleSheets.Remove(_lightStyleSheet);

            // Add current theme
            var currentSheet = GetCurrentStyleSheet();
            if (currentSheet != null)
                root.styleSheets.Add(currentSheet);
        }
    }
}
