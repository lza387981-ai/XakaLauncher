using Microsoft.Win32;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace XakaLauncher;

public partial class MainWindow : Window
{
    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XakaLauncher", "settings.json");

    public MainWindow()
    {
        InitializeComponent();
        LoadSettings();
        Directory.CreateDirectory(GameFolderBox.Text);
        Closing += (_, _) => SaveSettings();
    }

    private void LoadSettings()
    {
        var defaultGameDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".xakalauncher", "game");

        try
        {
            if (!File.Exists(SettingsPath))
            {
                GameFolderBox.Text = defaultGameDirectory;
                return;
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<LauncherSettings>(json);
            if (settings is null)
            {
                GameFolderBox.Text = defaultGameDirectory;
                return;
            }

            NicknameBox.Text = Regex.IsMatch(settings.Nickname ?? "", @"^[A-Za-z0-9_]{1,16}$")
                ? settings.Nickname
                : "Player";

            var versionItem = VersionBox.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Content?.ToString() == settings.MinecraftVersion);
            if (versionItem is not null) VersionBox.SelectedItem = versionItem;

            var loaderItem = LoaderBox.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Content?.ToString() == settings.Loader);
            if (loaderItem is not null) LoaderBox.SelectedItem = loaderItem;

            RamSlider.Value = Math.Clamp(settings.RamGb, 2, 10);
            GameFolderBox.Text = !string.IsNullOrWhiteSpace(settings.GameDirectory) &&
                Path.IsPathRooted(settings.GameDirectory)
                    ? settings.GameDirectory
                    : defaultGameDirectory;
        }
        catch
        {
            GameFolderBox.Text = defaultGameDirectory;
            StatusText.Text = "Не удалось прочитать настройки. Использованы значения по умолчанию.";
        }
    }

    private void SaveSettings()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(directory);
            var settings = new LauncherSettings
            {
                Nickname = NicknameBox.Text.Trim(),
                MinecraftVersion = (VersionBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "1.21.4",
                Loader = (LoaderBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Vanilla",
                RamGb = (int)RamSlider.Value,
                GameDirectory = GameFolderBox.Text.Trim()
            };
            var temporaryPath = SettingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
            File.Move(temporaryPath, SettingsPath, true);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Не удалось сохранить настройки: {ex.Message}";
        }
    }

    private sealed class LauncherSettings
    {
        public string Nickname { get; set; } = "Player";
        public string MinecraftVersion { get; set; } = "1.21.4";
        public string Loader { get; set; } = "Vanilla";
        public int RamGb { get; set; } = 4;
        public string GameDirectory { get; set; } = "";
    }


    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        var nickname = NicknameBox.Text.Trim();
        if (!Regex.IsMatch(nickname, @"^[A-Za-z0-9_]{1,16}$"))
        {
            StatusText.Text = "Никнейм должен содержать 1–16 символов: латинские буквы, цифры или _.";
            return;
        }

        var version = (VersionBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "1.21.4";
        var loader = (LoaderBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Vanilla";
        if (!string.Equals(loader, "Vanilla", StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text = $"{loader} пока не подключён к установщику. Сейчас доступен запуск Vanilla; поддержка Fabric и Forge будет следующим этапом.";
            return;
        }

        var gameDirectory = GameFolderBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(gameDirectory))
        {
            StatusText.Text = "Сначала выбери папку игры.";
            return;
        }

        PlayButton.IsEnabled = false;
        PlayButton.Content = "ЗАГРУЗКА...";
        try
        {
            SaveSettings();
            Directory.CreateDirectory(gameDirectory);
            StatusText.Text = $"Подготавливаю Minecraft {version}. При первом запуске загружу игровые файлы — это может занять несколько минут.";

            var minecraftPath = new MinecraftPath(gameDirectory);
            var launcher = new MinecraftLauncher(minecraftPath);
            await launcher.InstallAsync(version);

            StatusText.Text = "Файлы установлены. Подготавливаю запуск Minecraft...";
            var process = await launcher.BuildProcessAsync(version, new MLaunchOption
            {
                Session = MSession.CreateOfflineSession(nickname),
                MaximumRamMb = (int)RamSlider.Value * 1024
            });

            process.Start();
            StatusText.Text = "Minecraft запущен. Офлайн-профиль подходит только для одиночной игры и серверов, разрешающих офлайн-вход.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Не удалось установить или запустить Minecraft: {ex.Message}";
        }
        finally
        {
            PlayButton.IsEnabled = true;
            PlayButton.Content = "ИГРАТЬ";
        }
    }

    private void RamSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RamValueText is not null)
            RamValueText.Text = $"{(int)RamSlider.Value} ГБ";
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow((int)RamSlider.Value)
        {
            Owner = this
        };

        if (settingsWindow.ShowDialog() == true)
        {
            RamSlider.Value = settingsWindow.SelectedRamGb;
            RamValueText.Text = $"{(int)RamSlider.Value} ГБ";
            SaveSettings();
            StatusText.Text = $"Выделение ОЗУ для Minecraft: {(int)RamSlider.Value} ГБ. Настройка сохранена.";
        }
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