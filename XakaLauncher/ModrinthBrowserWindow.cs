using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XakaLauncher;

public sealed class ModrinthBrowserWindow : Window
{
    private static readonly HttpClient Http = CreateHttpClient();
    private readonly string _gameDirectory;
    private readonly string _gameVersion;
    private readonly string _loader;
    private readonly TextBox _searchBox = new();
    private readonly ComboBox _typeBox = new();
    private readonly StackPanel _results = new();
    private readonly TextBlock _status = new();
    private readonly Button _searchButton = new();

    public ModrinthBrowserWindow(string gameDirectory, string gameVersion, string loader)
    {
        _gameDirectory = gameDirectory;
        _gameVersion = gameVersion;
        _loader = loader;

        Title = "XakaLauncher — Modrinth";
        Width = 920;
        Height = 700;
        MinWidth = 720;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#171020");
        Foreground = Brushes.White;

        var root = new DockPanel { Margin = new Thickness(22) };
        Content = root;

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        header.Children.Add(new TextBlock
        {
            Text = "MODRINTH • КАТАЛОГ",
            FontSize = 25,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#F08BFF")
        });
        header.Children.Add(new TextBlock
        {
            Text = $"Minecraft {_gameVersion} • {_loader} • загрузка в папку выбранной игры",
            Foreground = Brush("#BDAECF"),
            Margin = new Thickness(0, 5, 0, 14)
        });
        header.Children.Add(new TextBlock
        {
            Text = "Важно: Modrinth модерирует проекты, но это не гарантия отсутствия вредоносных файлов. Скачивай только то, чему доверяешь.",
            Foreground = Brush("#F2C78B"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var controls = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(controls);

        _searchBox.Height = 38;
        _searchBox.Padding = new Thickness(10, 7, 10, 7);
        _searchBox.FontSize = 14;
        _searchBox.VerticalContentAlignment = VerticalAlignment.Center;
        _searchBox.Background = Brush("#251A32");
        _searchBox.Foreground = Brushes.White;
        _searchBox.BorderBrush = Brush("#69447F");
        _searchBox.ToolTip = "Название мода или ресурспака";
        _searchBox.KeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter) await SearchAsync();
        };
        Grid.SetColumn(_searchBox, 0);
        controls.Children.Add(_searchBox);

        _typeBox.Margin = new Thickness(10, 0, 10, 0);
        _typeBox.Height = 38;
        _typeBox.Items.Add("Моды");
        _typeBox.Items.Add("Ресурспаки");
        _typeBox.SelectedIndex = 0;
        _typeBox.Background = Brush("#251A32");
        _typeBox.Foreground = Brushes.White;
        Grid.SetColumn(_typeBox, 1);
        controls.Children.Add(_typeBox);

        _searchButton.Content = "НАЙТИ";
        _searchButton.Padding = new Thickness(18, 8, 18, 8);
        _searchButton.Background = Brush("#783B91");
        _searchButton.Foreground = Brushes.White;
        _searchButton.BorderBrush = Brush("#C45BFF");
        _searchButton.FontWeight = FontWeights.Bold;
        _searchButton.Click += async (_, _) => await SearchAsync();
        Grid.SetColumn(_searchButton, 2);
        controls.Children.Add(_searchButton);

        _status.Text = "Поиск по каталогу Modrinth. Выбери тип и введи запрос.";
        _status.Foreground = Brush("#D0BEDF");
        _status.Margin = new Thickness(0, 0, 0, 10);
        header.Children.Add(_status);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(scroll);
        scroll.Content = _results;

        Loaded += async (_, _) => await SearchAsync();
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("XakaLauncher/0.1 (Modrinth integration)");
        return client;
    }

    private async Task SearchAsync()
    {
        _searchButton.IsEnabled = false;
        _results.Children.Clear();
        _status.Text = "Ищу проекты на Modrinth...";
        try
        {
            var projectType = _typeBox.SelectedIndex == 1 ? "resourcepack" : "mod";
            var facets = new List<string[]> { new[] { $"project_type:{projectType}" } };
            if (!string.IsNullOrWhiteSpace(_gameVersion))
                facets.Add(new[] { $"versions:{_gameVersion}" });
            if (projectType == "mod" && _loader is "Fabric" or "Forge")
                facets.Add(new[] { $"categories:{_loader.ToLowerInvariant()}" });

            var query = Uri.EscapeDataString(_searchBox.Text.Trim());
            var facetsJson = Uri.EscapeDataString(JsonSerializer.Serialize(facets));
            var url = $"https://api.modrinth.com/v2/search?query={query}&facets={facetsJson}&limit=30&index=relevance";
            using var response = await Http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);

            foreach (var hit in json.RootElement.GetProperty("hits").EnumerateArray())
                AddProjectCard(hit, projectType);

            _status.Text = $"Найдено: {_results.Children.Count}. Источник: api.modrinth.com";
            if (_results.Children.Count == 0)
                _status.Text = "Ничего не найдено. Попробуй другой запрос или версию Minecraft.";
        }
        catch (Exception ex)
        {
            _status.Text = $"Ошибка поиска: {ex.Message}";
        }
        finally
        {
            _searchButton.IsEnabled = true;
        }
    }

    private void AddProjectCard(JsonElement hit, string projectType)
    {
        var projectId = hit.GetProperty("project_id").GetString() ?? "";
        var title = hit.GetProperty("title").GetString() ?? "Без названия";
        var author = hit.TryGetProperty("author", out var authorValue) ? authorValue.GetString() ?? "?" : "?";
        var description = hit.TryGetProperty("description", out var descValue) ? descValue.GetString() ?? "" : "";
        var downloads = hit.TryGetProperty("downloads", out var downloadValue) ? downloadValue.GetInt64() : 0;
        var card = new Border
        {
            Background = Brush("#21182D"),
            BorderBrush = Brush("#49345F"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(15),
            Margin = new Thickness(0, 0, 0, 11)
        };
        var panel = new DockPanel();
        card.Child = panel;

        var needsLoader = projectType == "mod" && _loader == "Vanilla";
        var button = new Button
        {
            Content = needsLoader ? "НУЖЕН FABRIC/FORGE" : "СКАЧАТЬ",
            IsEnabled = !needsLoader,
            Tag = (projectId, projectType),
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(14, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brush("#783B91"),
            Foreground = Brushes.White,
            BorderBrush = Brush("#C45BFF"),
            FontWeight = FontWeights.Bold
        };
        button.Click += async (_, _) =>
        {
            if (button.Tag is ValueTuple<string, string> data)
                await DownloadProjectAsync(data.Item1, data.Item2, button);
        };
        DockPanel.SetDock(button, Dock.Right);
        panel.Children.Add(button);

        var textPanel = new StackPanel();
        textPanel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#F4DFFF")
        });
        textPanel.Children.Add(new TextBlock
        {
            Text = $"Автор: {author}  •  Загрузок: {downloads:N0}",
            Foreground = Brush("#C6B3D7"),
            FontSize = 11,
            Margin = new Thickness(0, 3, 0, 5)
        });
        textPanel.Children.Add(new TextBlock
        {
            Text = description,
            Foreground = Brush("#E0D6E9"),
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 42
        });
        panel.Children.Add(textPanel);
        _results.Children.Add(card);
    }

    private async Task DownloadProjectAsync(string projectId, string projectType, Button button)
    {
        button.IsEnabled = false;
        button.Content = "ПОЛУЧАЮ...";
        try
        {
            var versionsUrl = $"https://api.modrinth.com/v2/project/{Uri.EscapeDataString(projectId)}/version?game_versions={Uri.EscapeDataString(JsonSerializer.Serialize(new[] { _gameVersion }))}";
            if (projectType == "mod" && _loader is "Fabric" or "Forge")
                versionsUrl += $"&loaders={Uri.EscapeDataString(JsonSerializer.Serialize(new[] { _loader.ToLowerInvariant() }))}";

            using var response = await Http.GetAsync(versionsUrl);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var versions = await JsonDocument.ParseAsync(stream);
            var version = versions.RootElement.EnumerateArray().FirstOrDefault();
            if (version.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("Нет версии, совместимой с выбранной версией Minecraft и загрузчиком.");

            var files = version.GetProperty("files");
            var file = files.EnumerateArray().FirstOrDefault(f =>
                f.TryGetProperty("primary", out var primary) && primary.GetBoolean());
            if (file.ValueKind != JsonValueKind.Object)
                file = files.EnumerateArray().FirstOrDefault();
            if (file.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("У этой версии нет файла для скачивания.");

            var downloadUrl = file.GetProperty("url").GetString() ?? "";
            var uri = new Uri(downloadUrl);
            if (uri.Scheme != Uri.UriSchemeHttps || uri.Host != "cdn.modrinth.com")
                throw new InvalidOperationException("Ссылка на файл не прошла проверку домена Modrinth.");

            var fileName = file.GetProperty("filename").GetString() ?? "download";
            var expectedExtension = projectType == "mod" ? ".jar" : ".zip";
            if (!string.Equals(Path.GetExtension(fileName), expectedExtension, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Ожидался файл {expectedExtension}, получен другой формат.");

            var targetFolderName = projectType == "mod" ? "mods" : "resourcepacks";
            var targetFolder = Path.Combine(_gameDirectory, targetFolderName);
            Directory.CreateDirectory(targetFolder);
            var safeName = Path.GetFileName(fileName);
            var targetPath = Path.Combine(targetFolder, safeName);
            var baseName = Path.GetFileNameWithoutExtension(safeName);
            var extension = Path.GetExtension(safeName);
            var suffix = 2;
            while (File.Exists(targetPath))
                targetPath = Path.Combine(targetFolder, $"{baseName} ({suffix++}){extension}");

            var temporaryPath = targetPath + ".download";
            try
            {
                using var download = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
                download.EnsureSuccessStatusCode();
                await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await download.Content.CopyToAsync(output);
                File.Move(temporaryPath, targetPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }

            _status.Text = $"Скачано: {Path.GetFileName(targetPath)} → {targetFolderName}. Перед запуском проверь совместимость и источник проекта.";
            button.Content = "ГОТОВО";
        }
        catch (Exception ex)
        {
            _status.Text = $"Не удалось скачать: {ex.Message}";
            button.Content = "ПОВТОРИТЬ";
            button.IsEnabled = true;
        }
    }

    private static SolidColorBrush Brush(string color) =>
        new((Color)ColorConverter.ConvertFromString(color));
}
