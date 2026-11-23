using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace XakaLauncher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        GameFolderBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".xakalauncher", "game");
        Directory.CreateDirectory(GameFolderBox.Text);
    }

    private void RamSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RamValueText is not null)
            RamValueText.Text = $"{(int)RamSlider.Value} ГБ";
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Выберите папку Minecraft",
            InitialDirectory = Directory.Exists(GameFolderBox.Text)
                ? GameFolderBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        };

        if (dialog.ShowDialog() == true)
        {
            GameFolderBox.Text = dialog.FolderName;
            StatusText.Text = "Папка игры выбрана.";
        }
    }

    private void OpenDownloads_Click(object sender, RoutedEventArgs e)
    {
        var version = (VersionBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "1.21.4";
        var loader = (LoaderBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Fabric";
        var gameDirectory = GameFolderBox.Text.Trim();
        var browser = new ModrinthBrowserWindow(gameDirectory, version, loader)
        {
            Owner = this
        };
        browser.ShowDialog();
    }

    private void OpenModsFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var gameDirectory = GameFolderBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(gameDirectory))
            {
                StatusText.Text = "Сначала выберите папку Minecraft.";
                return;
            }

            var modsDirectory = Path.Combine(gameDirectory, "mods");
            Directory.CreateDirectory(modsDirectory);

            Process.Start(new ProcessStartInfo
            {
                FileName = modsDirectory,
                UseShellExecute = true
            });

            StatusText.Text = "Открыта папка mods. Перетащите туда .jar-файлы модов.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Не удалось открыть папку mods: {ex.Message}";
        }
    }

    private void OpenResourcePacksFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var gameDirectory = GameFolderBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(gameDirectory))
            {
                StatusText.Text = "Сначала выберите папку Minecraft.";
                return;
            }

            var resourcePacksDirectory = Path.Combine(gameDirectory, "resourcepacks");
            Directory.CreateDirectory(resourcePacksDirectory);

            Process.Start(new ProcessStartInfo
            {
                FileName = resourcePacksDirectory,
                UseShellExecute = true
            });

            StatusText.Text = "Открыта папка resourcepacks. Перетащите туда .zip-файлы ресурспаков.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Не удалось открыть папку resourcepacks: {ex.Message}";
        }
    }

    private async void CheckJava_Click(object sender, RoutedEventArgs e)
    {
        JavaStatusText.Text = "Проверяю Java...";
        StatusText.Text = "Поиск Java в JAVA_HOME и PATH.";
        var javaPath = FindJava();

        if (javaPath is null)
        {
            JavaStatusText.Text = "Java не найдена. Для Minecraft 1.21.4 обычно нужна Java 21.";
            StatusText.Text = "Установите Java 21 и повторите проверку.";
            return;
        }

        try
        {
            var start = new ProcessStartInfo(javaPath, "-version")
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(start);
            if (process is null)
                throw new InvalidOperationException("Не удалось запустить Java.");

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            var match = Regex.Match(output, @"version ""(?<version>[^""]+)""");
            var version = match.Success ? match.Groups["version"].Value : "версия не определена";
            JavaStatusText.Text = $"Найдена Java: {version}\n{javaPath}";
            StatusText.Text = process.ExitCode == 0 ? "Java доступна." : "Java обнаружена, но завершила проверку с ошибкой.";
        }
        catch (Exception ex)
        {
            JavaStatusText.Text = "Ошибка проверки Java.";
            StatusText.Text = ex.Message;
        }
    }

    private static string? FindJava()
    {
        var home = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(home))
        {
            var candidate = Path.Combine(home, "bin", "java.exe");
            if (File.Exists(candidate)) return candidate;
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim('"'), "java.exe");
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }
}