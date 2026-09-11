using System.Collections.Generic;
using Godot;

namespace PaleKnight;

/// <summary>
/// Pooled one-shot SFX voices + a single looping music player.
/// Autoload named "Audio". SFX live at res://assets/sfx/&lt;name&gt;.wav.
/// </summary>
public partial class AudioManager : Node
{
    public static AudioManager? Instance { get; private set; }

    private const int VoiceCount = 14;

    private readonly List<AudioStreamPlayer> _voices = new();
    private readonly Dictionary<string, AudioStreamWav> _cache = new();
    private int _nextVoice;
    private AudioStreamPlayer? _musicPlayer;
    private Tween? _musicTween;
    private string _currentMusic = "";

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        for (int i = 0; i < VoiceCount; i++)
        {
            var voice = new AudioStreamPlayer { Bus = "Master", Name = $"Voice{i}" };
            AddChild(voice);
            _voices.Add(voice);
        }

        _musicPlayer = new AudioStreamPlayer { Bus = "Master", Name = "Music", VolumeDb = -6f };
        AddChild(_musicPlayer);
    }

    private AudioStreamWav? LoadWav(string name)
    {
        if (_cache.TryGetValue(name, out AudioStreamWav? cached))
            return cached;
        string path = $"res://assets/sfx/{name}.wav";
        var stream = GD.Load<AudioStreamWav>(path);
        if (stream == null)
        {
            GD.PushWarning($"AudioManager: missing audio file '{path}'.");
            return null;
        }
        _cache[name] = stream;
        return stream;
    }

    /// <summary>Play a one-shot SFX by file name (without path/extension).</summary>
    public void Play(string name, float pitch = 1f, float volDb = 0f)
    {
        var stream = LoadWav(name);
        if (stream == null || _voices.Count == 0)
            return;

        AudioStreamPlayer? free = null;
        for (int i = 0; i < _voices.Count; i++)
        {
            int idx = (_nextVoice + i) % _voices.Count;
            if (!_voices[idx].Playing)
            {
                free = _voices[idx];
                _nextVoice = (idx + 1) % _voices.Count;
                break;
            }
        }
        free ??= _voices[_nextVoice];
        _nextVoice = (_nextVoice + 1) % _voices.Count;

        free.Stream = stream;
        free.PitchScale = pitch;
        free.VolumeDb = volDb;
        free.Play();
    }

    /// <summary>Play looping music. No-op if the same track is already playing.</summary>
    public void PlayMusic(string name)
    {
        if (_currentMusic == name || _musicPlayer == null)
            return;
        var stream = LoadWav(name);
        if (stream == null)
            return;

        _currentMusic = name;
        stream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        stream.LoopBegin = 0;
        stream.LoopEnd = stream.Data.Length / 2; // 16-bit mono: 2 bytes per frame

        _musicTween?.Kill();
        _musicPlayer.Stream = stream;
        _musicPlayer.VolumeDb = -24f;
        _musicPlayer.Play();
        _musicTween = CreateTween();
        _musicTween.TweenProperty(_musicPlayer, "volume_db", -6f, 2.5f);
    }

    /// <summary>Fade the music out and stop it.</summary>
    public void StopMusic(float fade = 1f)
    {
        if (_musicPlayer == null)
            return;
        _currentMusic = "";
        _musicTween?.Kill();
        var player = _musicPlayer;
        _musicTween = CreateTween();
        _musicTween.TweenProperty(player, "volume_db", -40f, fade);
        _musicTween.TweenCallback(Callable.From(() => player.Stop()));
    }
}
