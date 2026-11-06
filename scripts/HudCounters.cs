using System.Collections.Generic;
using Godot;

namespace HackerOrSoldier;

// A HUD count that rolls up to a gain instead of jumping, lit gold while it
// rolls, so a pickup reads in the corner as well as over the agent. A drop
// (a shot fired) shows at once: only gains are news.
public partial class RollingLabel : Label
{
    private const float Catchup = 8f;     // share of the remaining gap closed per second
    private const float MinSpeed = 20f;   // so the last few units still tick by quickly
    private const float GlowTime = 0.35f;
    private static readonly Color Glow = new(1f, 0.85f, 0.3f);

    private int _target;
    private float _shown;
    private float _glow;
    private bool _primed;

    // The first value, and any value after Snap, shows without rolling.
    public int Value
    {
        get => _target;
        set
        {
            _target = value;
            if (!_primed || value < _shown)
                _shown = value;
            _primed = true;
            Text = Mathf.FloorToInt(_shown).ToString();
        }
    }

    // For a count that now means something else, such as another weapon's pool.
    public void Snap(int value)
    {
        _primed = false;
        _glow = 0;
        Value = value;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_shown < _target)
        {
            _shown = Mathf.Min(_target, _shown + Mathf.Max((_target - _shown) * Catchup, MinSpeed) * dt);
            _glow = GlowTime;
        }
        else if (_glow > 0)
            _glow -= dt;
        Modulate = Colors.White.Lerp(Glow, Mathf.Clamp(_glow / GlowTime, 0, 1));
        Text = Mathf.FloorToInt(_shown).ToString();
    }
}

// The lives row. A gained heart blinks in and a lost one blinks out before
// it goes, so a life changing hands catches the eye. Blinking uses alpha,
// not Visible, so the row never shifts while a heart flickers.
public partial class HeartRow : HBoxContainer
{
    private const float BlinkTime = 0.8f;
    private const float FlickerStep = 1f / 15f;

    private readonly Dictionary<TextureRect, float> _blinking = new();
    private readonly HashSet<TextureRect> _leaving = new();
    private bool _primed;

    // The first count shows without blinking.
    public int Count
    {
        get => GetChildCount() - _leaving.Count;
        set
        {
            while (Count < value)
            {
                var heart = Revive() ?? NewHeart();
                if (_primed)
                    _blinking[heart] = BlinkTime;
            }
            while (Count > value)
            {
                var heart = LastStaying();
                _leaving.Add(heart);
                _blinking[heart] = BlinkTime;
            }
            _primed = true;
        }
    }

    private TextureRect Revive()
    {
        foreach (var heart in _leaving)
        {
            _leaving.Remove(heart);
            return heart;
        }
        return null;
    }

    private TextureRect NewHeart()
    {
        var heart = new TextureRect
        {
            Texture = Sprites.Get("heart"),
            StretchMode = TextureRect.StretchModeEnum.KeepCentered,
        };
        AddChild(heart);
        return heart;
    }

    private TextureRect LastStaying()
    {
        for (int i = GetChildCount() - 1; i >= 0; i--)
            if (GetChild(i) is TextureRect heart && !_leaving.Contains(heart))
                return heart;
        return null;
    }

    public override void _Process(double delta)
    {
        foreach (var heart in new List<TextureRect>(_blinking.Keys))
        {
            float left = _blinking[heart] - (float)delta;
            if (left > 0)
            {
                _blinking[heart] = left;
                heart.SelfModulate = new Color(1, 1, 1, (int)(left / FlickerStep) % 2 == 0 ? 1 : 0);
                continue;
            }
            _blinking.Remove(heart);
            heart.SelfModulate = Colors.White;
            if (_leaving.Remove(heart))
            {
                RemoveChild(heart);
                heart.QueueFree();
            }
        }
    }
}
