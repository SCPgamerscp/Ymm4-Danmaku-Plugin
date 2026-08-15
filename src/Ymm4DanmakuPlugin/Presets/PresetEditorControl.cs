using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;
using Ymm4DanmakuPlugin.Parameters;

namespace Ymm4DanmakuPlugin.Presets;

/// <summary>
/// エミッター編集エリアに表示するプリセット操作パネル。
/// <para>
/// XAML を使わずコードで組み立てているのは、
/// ・単一 DLL 配布で BAML の埋め込みを気にしなくてよい<br/>
/// ・Windows 以外でのコンパイル検証 (スタブ SDK) を単純に保てる<br/>
/// という 2 点のため。
/// </para>
/// </summary>
internal sealed class PresetEditorControl : UserControl, IPropertyEditorControl
{
    private readonly ComboBox presetList = new() { Margin = new Thickness(0, 0, 4, 0), MinWidth = 140 };
    private readonly TextBlock status = new()
    {
        Margin = new Thickness(0, 4, 0, 0),
        TextWrapping = TextWrapping.Wrap,
        Opacity = 0.75,
    };

    private EmitterParameter? emitter;

    /// <summary>更新中フラグ。プログラムからの選択変更でイベントを発火させないために使う。</summary>
    private bool suppressSelectionChanged;

    public PresetEditorControl()
    {
        var root = new StackPanel();

        var topRow = new StackPanel { Orientation = Orientation.Horizontal };
        topRow.Children.Add(presetList);
        topRow.Children.Add(CreateButton("適用", "選択したプリセットをこのエミッターへ適用します。", OnApply));
        topRow.Children.Add(CreateButton("更新", "プリセットフォルダを読み直します。", OnRefresh));
        root.Children.Add(topRow);

        var bottomRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        bottomRow.Children.Add(CreateButton("保存", "現在の設定をプリセットフォルダへ保存します。", OnSave));
        bottomRow.Children.Add(CreateButton("読み込み…", "JSON ファイルからプリセットを読み込んで適用します。", OnImport));
        bottomRow.Children.Add(CreateButton("書き出し…", "現在の設定を JSON ファイルへ書き出します。", OnExport));
        bottomRow.Children.Add(CreateButton("サンプル出力", "同梱の東方風サンプル集をプリセットフォルダへ書き出します。", OnExportBuiltIn));
        root.Children.Add(bottomRow);

        root.Children.Add(status);
        Content = root;

        presetList.SelectionChanged += OnSelectionChanged;
        Loaded += (_, _) => ReloadList();
    }

    public event EventHandler? BeginEdit;

    public event EventHandler? EndEdit;

    /// <summary>編集対象のエミッターを差し替える。</summary>
    internal void Attach(EmitterParameter? target)
    {
        emitter = target;
        ReloadList();
    }

    private static Button CreateButton(string text, string tooltip, RoutedEventHandler onClick)
    {
        var button = new Button
        {
            Content = text,
            ToolTip = tooltip,
            Margin = new Thickness(0, 0, 4, 0),
            Padding = new Thickness(8, 2, 8, 2),
            MinWidth = 56,
        };

        button.Click += onClick;
        return button;
    }

    private void ReloadList()
    {
        suppressSelectionChanged = true;

        try
        {
            presetList.ItemsSource = DanmakuPresetManager.Names;
            presetList.SelectedItem = emitter?.PresetName;

            // 保存済みの名前が一覧に無い (フォルダを移した等) 場合は先頭を選ぶ
            if (presetList.SelectedItem is null && presetList.Items.Count > 0) presetList.SelectedIndex = 0;
        }
        finally
        {
            suppressSelectionChanged = false;
        }

        ShowLoadErrors();
    }

    private void ShowLoadErrors()
    {
        var errors = DanmakuPresetManager.LoadErrors;
        if (errors.Count == 0) return;

        status.Text = "プリセット読み込みエラー: " + string.Join(" / ", errors);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSelectionChanged || emitter is null) return;
        if (presetList.SelectedItem is not string name) return;

        // 選択しただけでは適用しない。名前だけ覚えておく (プロジェクト保存対象)。
        Edit(() => emitter.PresetName = name);
        status.Text = $"「{name}」を選択しました。[適用] で反映します。";
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        if (emitter is null) return;
        if (presetList.SelectedItem is not string name)
        {
            status.Text = "適用するプリセットを選んでください。";
            return;
        }

        var applied = false;
        Edit(() =>
        {
            emitter.PresetName = name;
            applied = DanmakuPresetManager.Apply(name, emitter);
        });

        status.Text = applied
            ? $"「{name}」を適用しました。"
            : $"「{name}」が見つかりませんでした。[更新] を試してください。";
    }

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        DanmakuPresetManager.Refresh();
        ReloadList();
        status.Text = $"プリセットを {DanmakuPresetManager.All.Count} 件読み込みました。";
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (emitter is null) return;

        var name = string.IsNullOrWhiteSpace(emitter.PresetName) ? emitter.Name : emitter.PresetName;

        try
        {
            var path = DanmakuPresetManager.Save(emitter, name);
            Edit(() => emitter.PresetName = name);
            ReloadList();
            status.Text = $"保存しました: {path}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            status.Text = $"保存に失敗しました: {ex.Message}";
        }
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        if (emitter is null) return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "弾幕プリセットの読み込み",
            Filter = "弾幕プリセット (*.json)|*.json|すべてのファイル (*.*)|*.*",
            InitialDirectory = SafeUserDirectory(),
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            Core.Presets.DanmakuPreset? preset = null;
            Edit(() => preset = DanmakuPresetManager.Import(dialog.FileName, emitter));

            ReloadList();
            status.Text = preset is null
                ? "プリセットを読み取れませんでした。ファイル形式を確認してください。"
                : $"「{preset.Name}」を読み込んで適用しました。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            status.Text = $"読み込みに失敗しました: {ex.Message}";
        }
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        if (emitter is null) return;

        var name = string.IsNullOrWhiteSpace(emitter.PresetName) ? emitter.Name : emitter.PresetName;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "弾幕プリセットの書き出し",
            Filter = "弾幕プリセット (*.json)|*.json",
            FileName = DanmakuPresetManager.SanitizeFileName(name) + ".json",
            InitialDirectory = SafeUserDirectory(),
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            DanmakuPresetManager.ExportTo(emitter, dialog.FileName, name);
            status.Text = $"書き出しました: {dialog.FileName}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            status.Text = $"書き出しに失敗しました: {ex.Message}";
        }
    }

    private void OnExportBuiltIn(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = DanmakuPresetManager.ExportBuiltIn();
            ReloadList();
            status.Text = $"サンプル集を書き出しました: {path}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            status.Text = $"書き出しに失敗しました: {ex.Message}";
        }
    }

    private static string SafeUserDirectory()
    {
        try
        {
            return DanmakuPresetManager.EnsureUserDirectory();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 元に戻す/やり直しの 1 単位として編集を実行する。
    /// YMM4 は <see cref="BeginEdit"/> / <see cref="EndEdit"/> の対で変更を束ねる。
    /// </summary>
    private void Edit(Action action)
    {
        BeginEdit?.Invoke(this, EventArgs.Empty);

        try
        {
            action();
        }
        finally
        {
            EndEdit?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>
/// エミッター編集エリアにプリセット操作パネルを表示する属性。
/// <c>string</c> 型のプロパティ (選択中プリセット名) に付ける。
/// </summary>
internal sealed class PresetSelectorAttribute : PropertyEditorAttribute
{
    public PresetSelectorAttribute()
    {
        PropertyEditorSize = PropertyEditorSize.FullWidth;
    }

    public override FrameworkElement Create() => new PresetEditorControl();

    public override void SetBindings(
        FrameworkElement control,
        object item,
        object propertyOwner,
        PropertyInfo propertyInfo)
    {
        if (control is PresetEditorControl editor)
        {
            var target = propertyOwner as EmitterParameter
                         ?? (propertyOwner as DanmakuShapeParameter)?.MainEmitter;
            editor.Attach(target);
        }
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is PresetEditorControl editor) editor.Attach(null);
    }
}
