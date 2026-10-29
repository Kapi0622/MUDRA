using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 効果音を鳴らすView（B7）。App（画面・UIの音）と InGame（戦闘の音）に1つずつ置く。
///
/// Audio Random Container は PlayOneShot で鳴らせない（AudioClip しか受け取らない）ため、
/// AudioSource を数個用意して順番に使い回し、resource に入れて Play する。これで重なる音も鳴る。
/// AudioSource は自分で作るので、シーンに置くのはこのコンポーネントだけでよい。
///
/// ポーズは鳴っている音だけを止める（SetPaused）。ポーズ中に新しく鳴らす音（カウントダウンなど）は普通に鳴る。
/// AudioListener.pause（全体の一時停止）を使わないのは、InGame がポーズ中にアンロードされても
/// 全体の状態を戻し忘れる心配がないようにするため。
/// </summary>
public class SePlayer : MonoBehaviour
{
    /// <summary>同時に鳴らせる数。超えたら一番古い音を止めて使い回す</summary>
    private const int PoolSize = 8;
    private const float NormalPitch = 1f;
    // 溜めの音の再生速度の範囲。Unityの上限は3倍。極端な溜め時間でも聞き取れる範囲に収める
    private const float MinHeldPitch = 0.5f;
    private const float MaxHeldPitch = 3f;

    [Tooltip("出力先（MX_Main の SE グループ）。設定画面の効果音の音量はこのグループにかかる（B7）")]
    [SerializeField] private AudioMixerGroup _outputGroup;

    private AudioSource[] _pool;
    private int _nextIndex;

    // 途中で止めたい音（敵の攻撃の溜め）専用。使い回しに巻き込まれて止まらないよう別に持つ
    private AudioSource _heldSource;

    private readonly List<AudioSource> _pausedSources = new();
    private bool _isInitialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// 初期化を1回だけ行う。最初の敵の登場などは BattleInitializer.Awake 内から流れてくるため、
    /// このViewのAwakeより先に公開メソッドが呼ばれることがある。
    /// </summary>
    private void EnsureInitialized()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        _pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
            _pool[i] = CreateSource();

        _heldSource = CreateSource();
    }

    private AudioSource CreateSource()
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.outputAudioMixerGroup = _outputGroup;
        return source;
    }

    /// <summary>
    /// 効果音を1回鳴らす。sound が null（素材が未設定）なら何もしない。
    /// </summary>
    /// <param name="pitch">再生速度の倍率。Audio Random Container のピッチのばらつきとは別に掛かる</param>
    public void Play(AudioResource sound, float pitch = NormalPitch)
    {
        if (sound == null) return;
        EnsureInitialized();

        var source = _pool[_nextIndex];
        _nextIndex = (_nextIndex + 1) % PoolSize;

        source.Stop();
        source.resource = sound;
        source.pitch = pitch;
        source.Play();
    }

    /// <summary>
    /// delay 秒待ってから効果音を1回鳴らす。待つのはゲーム内時間（ポーズ中は待ちも止まる）。
    /// 演出の時間表（PresentationTimingData）に合わせて鳴らす音に使う（咆哮・前進・「結印！」）。
    /// 待っている間にこのオブジェクトが破棄されたら鳴らさない。
    /// </summary>
    public void PlayDelayed(AudioResource sound, float delay)
    {
        if (sound == null) return;
        if (delay <= 0f)
        {
            Play(sound);
            return;
        }

        PlayDelayedAsync(sound, delay, destroyCancellationToken).Forget();
    }

    private async UniTaskVoid PlayDelayedAsync(AudioResource sound, float delay, CancellationToken token)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Play(sound);
    }

    /// <summary>
    /// 途中で止めるかもしれない音を鳴らす（敵の攻撃の溜め）。同時に鳴らせるのは1つだけで、前の音は止める。
    /// duration 秒でちょうど鳴り終わるよう再生速度を変える（せり上がる音の頂点を攻撃の瞬間に合わせるため）。
    /// 長さを読む必要があるので AudioClip だけを受け取る。
    /// </summary>
    public void PlayHeld(AudioClip clip, float duration)
    {
        EnsureInitialized();
        _heldSource.Stop();
        if (clip == null) return;

        _heldSource.resource = clip;
        _heldSource.pitch = duration > 0f ? Mathf.Clamp(clip.length / duration, MinHeldPitch, MaxHeldPitch) : NormalPitch;
        _heldSource.Play();
    }

    /// <summary>PlayHeld で鳴らした音を止める。鳴っていなければ何もしない</summary>
    public void StopHeld()
    {
        EnsureInitialized();
        _heldSource.Stop();
    }

    /// <summary>
    /// ポーズの開始・解除。開始時に鳴っていた音だけを一時停止し、解除で続きから鳴らす。
    /// </summary>
    public void SetPaused(bool isPaused)
    {
        EnsureInitialized();

        if (isPaused)
        {
            PauseIfPlaying(_heldSource);
            foreach (var source in _pool) PauseIfPlaying(source);
            return;
        }

        foreach (var source in _pausedSources) source.UnPause();
        _pausedSources.Clear();
    }

    private void PauseIfPlaying(AudioSource source)
    {
        if (!source.isPlaying) return;
        source.Pause();
        _pausedSources.Add(source);
    }
}
