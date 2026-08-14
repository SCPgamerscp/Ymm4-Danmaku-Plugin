using Ymm4DanmakuPlugin.Core.Scripting;

namespace Ymm4DanmakuPlugin.Core.Importers;

/// <summary>BulletML (XML) を読み込むインポーター。</summary>
public sealed class BulletMlDanmakuImporter : IDanmakuImporter
{
    public string Name => "BulletML";

    public IReadOnlyList<string> SupportedExtensions { get; } = [".xml", ".bulletml", ".bml"];

    public bool CanImport(string text)
    {
        var span = text.AsSpan().TrimStart();
        if (span.Length == 0 || span[0] != '<') return false;
        return text.Contains("<bulletml", StringComparison.OrdinalIgnoreCase);
    }

    public DanmakuImportResult Import(string text)
    {
        try
        {
            var program = BulletMlParser.Parse(text);
            var warnings = new List<string>();

            if (program.TopActions.Count == 0)
                warnings.Add("label が \"top\" で始まる <action> が見つかりません。弾は発射されません。");

            foreach (var action in program.Actions.Values)
                ValidateReferences(program, action, warnings);

            return new DanmakuImportResult { BulletMl = program, Warnings = warnings };
        }
        catch (BulletMlParseException e)
        {
            return DanmakuImportResult.Failure(e.Message);
        }
    }

    /// <summary>未解決のラベル参照を警告として収集する。</summary>
    private static void ValidateReferences(BulletMlProgram program, BulletMlAction action, List<string> warnings)
    {
        foreach (var command in action.Commands)
        {
            switch (command)
            {
                case BulletMlActionRef { Inline: null, Label: { } label } when !program.Actions.ContainsKey(label):
                    warnings.Add($"actionRef の参照先 '{label}' が見つかりません。");
                    break;

                case BulletMlFireRef { Inline: null, Label: { } label } when !program.Fires.ContainsKey(label):
                    warnings.Add($"fireRef の参照先 '{label}' が見つかりません。");
                    break;

                case BulletMlRepeat repeat when repeat.Action is { Inline: null, Label: { } label } &&
                                                !program.Actions.ContainsKey(label):
                    warnings.Add($"repeat 内 actionRef の参照先 '{label}' が見つかりません。");
                    break;
            }
        }
    }
}
