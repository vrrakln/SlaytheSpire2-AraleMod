using Godot;
using MegaCrit.Sts2.Core.Saves;
using System;
namespace Arale.Scripts.Combat;

// Stationary companions; no controls, labels or hitboxes cover Arale.
public partial class AraleLuluVfx : Node2D
{
    private readonly AraleLuluPresentation _state = new();
    private Node2D _portraits = null!;
    private AudioStreamPlayer _music = null!;

    /// <summary>音量倍率缓存（配置页改了音量后最多 0.5 秒生效）。</summary>
    private float _volumeScale = 1f;
    private int _volumeRefreshCountdown;
    public override void _Ready()
    {
        _portraits = new Node2D { Name = "Companions", Visible = false };
        AddChild(_portraits);
        AddCompanion("red", -227);
        AddCompanion("blue", 227);
        _music = new AudioStreamPlayer {
            Name = "LuluMusic", Bus = "Master",
            Stream = ResourceLoader.Load<AudioStream>("res://arale_mod/audio/lulu_enter.ogg")
        };
        AddChild(_music);
        RefreshVolumeScale();
    }
    private void AddCompanion(string color, float x)
    {
        var texture = ResourceLoader.Load<Texture2D>($"res://arale_mod/images/lulu/{color}.png");
        // 512x768 assets share the same ground anchor (256,744).
        _portraits.AddChild(new Sprite2D {
            Name = color, Texture = texture, Centered = false,
            Offset = new Vector2(-256, -744), Position = new Vector2(x, -3),
            Scale = Vector2.One * .50f
        });
    }
    /// <summary>读取配置页里的「魔法露露音量」（0 ~ 1.5 倍）。</summary>
    private void RefreshVolumeScale()
    {
        try
        {
            _volumeScale = (float)AraleSettings.LuluVolume;
        }
        catch (Exception)
        {
            // 配置还没注册好时按原音量播放，绝不让音频影响战斗
            _volumeScale = 1f;
        }
    }
    public void UpdateState(double delta, bool active)
    {
        bool entering = active && !_state.Active;
        _state.Tick(delta, active);
        _portraits.Visible = _state.Opacity > 0;
        _portraits.Modulate = new Color(1, 1, 1, _state.Opacity);
        // Master bus already applies master volume and background muting.
        // 再乘上配置页里的「魔法露露音量」（默认 100%）。
        if (_volumeRefreshCountdown-- <= 0)
        {
            _volumeRefreshCountdown = 30;
            RefreshVolumeScale();
        }
        float musicVolume = SaveManager.Instance.SettingsSave.VolumeBgm;
        _music.VolumeLinear = musicVolume * musicVolume * _state.Opacity * _volumeScale;
        if (entering) _music.Play(); // Once per entry; no restart while active.
        if (!active && _state.Opacity <= 0) _music.Stop();
    }
    public override void _ExitTree() { _music?.Stop(); }
}
