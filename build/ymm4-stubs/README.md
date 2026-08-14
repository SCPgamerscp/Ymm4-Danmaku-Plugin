# YMM4 スタブアセンブリ (CI / Linux ビルド検証用)

## これは何か

`Ymm4DanmakuPlugin` (配布用プラグイン) は YMM4 本体の DLL
(`YukkuriMovieMaker.Plugin.dll` / `YukkuriMovieMaker.Controls.dll`) を参照します。
これらは **YMM4 のインストールフォルダにのみ存在し、再配布できません**。

そのため YMM4 が入っていない環境 (CI、Linux の開発サンドボックスなど) では
プラグイン本体をビルドできず、「コンパイルが通るか」すら検証できません。

このフォルダは、その問題を解決するための **API 形状だけを真似た空実装 (スタブ)** です。

- YMM4 の公開 API と**同じ名前空間・同じ型名・同じシグネチャ**を持つ
- 中身は実装されていない (呼び出すと `NotSupportedException`)
- **ビルド検証専用**。これを使ってビルドした DLL は YMM4 では動きません

## なぜ必要か

プラグイン層のコードは分量が多く (図形プラグイン、Direct2D 描画、プロパティ UI、音声エフェクト)、
型の綴り間違いやシグネチャ不一致は「YMM4 のある Windows 機でビルドするまで発見できない」ことになります。

スタブを用意しておくと、YMM4 が無い環境でも

- 名前空間・型名・メソッドシグネチャの誤り
- Core エンジンとプラグイン層の結線ミス
- C# の構文・型エラー

を検出できます。

## 使い方

```bash
# スタブを生成する (bin/ に DLL が出力される)
dotnet build build/ymm4-stubs/Ymm4StubSdk.csproj

# スタブを参照してプラグイン層をビルド検証する
dotnet build src/Ymm4DanmakuPlugin/Ymm4DanmakuPlugin.csproj -p:UseYmm4Stubs=true
```

`UseYmm4Stubs=true` のときだけ参照先がスタブへ切り替わります。
**実際に配布用 DLL を作るときは、このオプションを付けずに**
Windows + YMM4 実 DLL でビルドしてください (`Directory.Build.props` の `YMM4DirPath` を使用)。

## 注意

- スタブは「ビルドを通すための最小限」しか定義していません。
  プラグイン層で新しい YMM4 API を使い始めたら、ここにも対応する定義を追加する必要があります。
- スタブの実装は空なので、ユニットテストの対象にはなりません。
  ロジックのテストは YMM4 に依存しない `Ymm4DanmakuPlugin.Core` 側で行います。
