using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace dot_net_fm;

/// <summary>
/// Loads theme configuration from Config/theme-config.json and applies it
/// as WPF resources (Color, SolidColorBrush, CornerRadius, Thickness, double).
/// Call LoadAndApply() during App startup before any Window is created.
/// </summary>
public static class ThemeService
{
    private const string ConfigFileName = "theme-config.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string ConfigPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", ConfigFileName);

    /// <summary>
    /// Loads theme config and merges all resources into Application.Current.Resources.
    /// Duplicate keys are silently skipped (App.xaml hardcoded resources take precedence).
    /// </summary>
    public static void LoadAndApply()
    {
        var config = LoadConfig();
        if (config == null)
            return;

        var resources = Application.Current.Resources;

        // --- Colors ---
        foreach (var kvp in config.Colors)
        {
            if (resources.Contains(kvp.Key))
                continue;

            var color = (Color)ColorConverter.ConvertFromString(kvp.Value);
            resources.Add(kvp.Key, color);
        }

        // --- Brushes (SolidColorBrush resources) ---
        foreach (var kvp in config.Brushes)
        {
            if (resources.Contains(kvp.Key))
                continue;

            var color = (Color)ColorConverter.ConvertFromString(kvp.Value);
            resources.Add(kvp.Key, new SolidColorBrush(color));
        }

        // --- Radius -> CornerRadius (keyed as "CornerRadius{Name}") ---
        foreach (var kvp in config.Radius)
        {
            string key = "CornerRadius" + kvp.Key;
            if (resources.Contains(key))
                continue;

            resources.Add(key, new CornerRadius(kvp.Value));
        }

        // --- Spacing -> Thickness or double ---
        foreach (var kvp in config.Spacing)
        {
            string key = "Spacing" + kvp.Key;
            if (resources.Contains(key))
                continue;

            if (kvp.Value.ValueKind == JsonValueKind.String)
            {
                resources.Add(key, ParseThickness(kvp.Value.GetString()));
            }
            else if (kvp.Value.ValueKind == JsonValueKind.Number)
            {
                resources.Add(key, kvp.Value.GetDouble());
            }
        }

        // --- Sizing -> double ---
        foreach (var kvp in config.Sizing)
        {
            string key = "Size" + kvp.Key;
            if (resources.Contains(key))
                continue;

            if (kvp.Value.ValueKind == JsonValueKind.Number)
            {
                resources.Add(key, kvp.Value.GetDouble());
            }
        }

        // --- Opacity -> double ---
        foreach (var kvp in config.Opacity)
        {
            string key = "Opacity" + kvp.Key;
            if (resources.Contains(key))
                continue;

            if (kvp.Value.ValueKind == JsonValueKind.Number)
            {
                resources.Add(key, kvp.Value.GetDouble());
            }
        }
    }

    private static Thickness ParseThickness(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return new Thickness(0);

        var parts = s.Split(',');
        if (parts.Length == 1 && double.TryParse(parts[0], out double all))
            return new Thickness(all);
        if (parts.Length == 2 &&
            double.TryParse(parts[0], out double h) &&
            double.TryParse(parts[1], out double v))
            return new Thickness(h, v, h, v);
        if (parts.Length == 4 &&
            double.TryParse(parts[0], out double l) &&
            double.TryParse(parts[1], out double t) &&
            double.TryParse(parts[2], out double r) &&
            double.TryParse(parts[3], out double b))
            return new Thickness(l, t, r, b);

        return new Thickness(0);
    }

    private static ThemeConfig? LoadConfig()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                string json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<ThemeConfig>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}

/// <summary>
/// Deserialization model for theme-config.json.
/// Spacing, Sizing, Opacity use JsonElement to handle mixed types (string/number).
/// </summary>
public class ThemeConfig
{
    public Dictionary<string, string> Colors { get; set; } = new();
    public Dictionary<string, string> Brushes { get; set; } = new();
    public Dictionary<string, double> Radius { get; set; } = new();
    public Dictionary<string, JsonElement> Spacing { get; set; } = new();
    public Dictionary<string, JsonElement> Sizing { get; set; } = new();
    public Dictionary<string, JsonElement> Opacity { get; set; } = new();
}
