using Ymm4DanmakuPlugin.Core.Audio;

namespace Ymm4DanmakuPlugin.Core.Tests;

/// <summary>プレビュー切替で前の弾幕音声が残らないことの回帰テスト。</summary>
public class DanmakuAudioAssignmentExtraTests
{
    [Fact]
    public void 終盤の弾幕は次の個別音声に割り当てない()
    {
        var first = new object();
        var second = new object();
        var candidates = new[]
        {
            new DanmakuAudioCandidate(first, 0, 5, 100, 1, LastItemFrame: 90, TotalFrame: 100),
            new DanmakuAudioCandidate(second, 5, 5, 0, 2, LastItemFrame: 0, TotalFrame: 100),
        };

        var selection = DanmakuAudioAssignment.Select(candidates, null, new HashSet<object>(), 5);

        Assert.Same(second, selection);
    }

    [Fact]
    public void 終盤判定は再生位置が85パーセント以上のときだけ真()
    {
        var playing = new DanmakuAudioCandidate(new object(), 0, 5, 1, 1, LastItemFrame: 50, TotalFrame: 100);
        var expiring = new DanmakuAudioCandidate(new object(), 0, 5, 1, 1, LastItemFrame: 85, TotalFrame: 100);
        var unknown = new DanmakuAudioCandidate(new object(), 0, 0, 0, 1);

        Assert.False(DanmakuAudioAssignment.IsExpiring(playing));
        Assert.True(DanmakuAudioAssignment.IsExpiring(expiring));
        Assert.False(DanmakuAudioAssignment.IsExpiring(unknown));
    }
}
