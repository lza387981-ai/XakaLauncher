using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XakaLauncher;

public sealed class SettingsWindow : Window
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatus
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatus>();
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatus buffer);

    private readonly Slider _ramSlider;
    private readonly TextBlock _ramValue;
    private readonly TextBlock _memoryInfo;
    private readonly TextBlock _warning;
    public int SelectedRamGb { get; private set; }

    public SettingsWindow(int currentRamGb)
    {
        Title = "XakaLauncher — Настройки";
        Width = 560;
        Height = 440;
        MinWidth = 500;
        MinHeight = 400;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#171020");
        Foreground = Brushes.White;

        var root = new StackPanel { Margin = new Thickness(28) };
        Content = root;

        root.Children.Add(new TextBlock
        {
            Text = "НАСТРОЙКИ",
            FontSize = 26,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#F08BFF"),
            Margin = new Thickness(0, 0, 0, 16)
        });

        root.Children.Add(new TextBlock
        {
            Text = "Оперативная память для Minecraft",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#F4DFFF"),
            Margin = new Thickness(0, 0, 0, 12)
        });

        var installedRamGb = GetInstalledRamGb();
        var recommendedRamGb = Math.Clamp(installedRamGb - 4, 2, 6);
        var hardwareMaximum = Math.Clamp(installedRamGb - 2, 2, 10);

        _memoryInfo = new TextBlock
        {
            Text = installedRamGb > 0
                ? $"На компьютере установлено {installedRamGb} ГБ ОЗУ. Рекомендуем выделить Minecraft около {recommendedRamGb} ГБ, чтобы оставить память Windows и другим программам."
                : $"Не удалось определить объём ОЗУ. Выберите значение вручную и оставьте запас памяти для Windows.",
            Foreground = Brush("#D0BEDF"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 22)
        };
        root.Children.Add(_memoryInfo);

        _ramValue = new TextBlock
        {
            Text = $"{Math.Clamp(currentRamGb, 2, 10)} ГБ",
            FontSize = 34,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#F08BFF"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        root.Children.Add(_ramValue);

        _ramSlider = new Slider
        {
            Minimum = 2,
            Maximum = hardwareMaximum,
            Value = Math.Clamp(currentRamGb, 2, hardwareMaximum),
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            Margin = new Thickness(0, 0, 0, 8)
        };
        _ramSlider.ValueChanged += (_, _) =>
        {
            _ramValue.Text = $"{(int)_ramSlider.Value} ГБ";
            UpdateRamWarning(installedRamGb, recommendedRamGb);
        };
        root.Children.Add(_ramSlider);

        _warning = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 14),
            FontWeight = FontWeights.SemiBold
        };
        root.Children.Add(_warning);
        UpdateRamWarning(installedRamGb, recommendedRamGb);

        var range = new DockPanel { Margin = new Thickness(0, 0, 0, 18) };
        range.Children.Add(new TextBlock { Text = "2 ГБ", Foreground = Brush("#BDAECF") });
        var maxLabel = new TextBlock { Text = $"{hardwareMaximum} ГБ", Foreground = Brush("#BDAECF") };
        DockPanel.SetDock(maxLabel, Dock.Right);
        range.Children.Add(maxLabel);
        root.Children.Add(range);

        root.Children.Add(new TextBlock
        {
            Text = "Максимальное выделение в лаунчере — 10 ГБ. Не рекомендуется отдавать Minecraft всю оперативную память компьютера: Windows и фоновые приложения тоже её используют.",
            Foreground = Brush("#F2C78B"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 24)
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button
        {
            Content = "ОТМЕНА",
            Padding = new Thickness(18, 10, 18, 10),
            Margin = new Thickness(0, 0, 10, 0),
            Background = Brush("#30243C"),
            Foreground = Brushes.White,
            BorderBrush = Brush("#69447F")
        };
        cancel.Click += (_, _) => { DialogResult = false; };
        var save = new Button
        {
            Content = "СОХРАНИТЬ",
            Padding = new Thickness(18, 10, 18, 10),
            Background = Brush("#783B91"),
            Foreground = Brushes.White,
            BorderBrush = Brush("#C45BFF"),
            FontWeight = FontWeights.Bold
        };
        save.Click += (_, _) =>
        {
            SelectedRamGb = (int)_ramSlider.Value;
            DialogResult = true;
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        root.Children.Add(buttons);
    }

    private void UpdateRamWarning(int installedRamGb, int recommendedRamGb)
    {
        var selected = (int)_ramSlider.Value;
        if (installedRamGb <= 0)
        {
            _warning.Text = "Не удалось проверить память компьютера. Не выбирай максимальное значение без необходимости.";
            _warning.Foreground = Brush("#F2C78B");
        }
        else if (selected > recommendedRamGb + 2)
        {
            _warning.Text = $"Внимание: {selected} ГБ — слишком большое выделение для компьютера с {installedRamGb} ГБ ОЗУ. Рекомендуем {recommendedRamGb} ГБ. Оставь запас Windows и другим программам.";
            _warning.Foreground = Brush("#FF8D9A");
        }
        else if (selected > recommendedRamGb)
        {
            _warning.Text = $"Это выше рекомендуемых {recommendedRamGb} ГБ. Убедись, что остальной памяти хватит Windows и другим программам.";
            _warning.Foreground = Brush("#F2C78B");
        }
        else
        {
            _warning.Text = $"Хороший выбор: {selected} ГБ. Рекомендация для этого компьютера — около {recommendedRamGb} ГБ.";
            _warning.Foreground = Brush("#A8E6B0");
        }
    }

    private static int GetInstalledRamGb()
    {
        try
        {
            var status = new MemoryStatus();
            return GlobalMemoryStatusEx(status)
                ? (int)Math.Round(status.TotalPhysical / 1024d / 1024d / 1024d)
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static SolidColorBrush Brush(string color) =>
        new((Color)ColorConverter.ConvertFromString(color));
}
