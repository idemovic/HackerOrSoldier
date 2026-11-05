using System;
using System.Collections.Generic;
using Godot;

namespace HackerOrSoldier;

public enum Sound
{
    UiClick, Purchase, CantAfford, Pause, Unpause,
    Jump, Swim, Land, BarrelPush,
    ShotPistol, ShotRifle, ShotMinigun, Crossbow, Chainsaw, DynamiteThrow, EmptyClick,
    EnemyShot, EnemyMinigun, EnemyPlasma, EnemyHit, EnemyDie, RocketLaunch,
    BulletHit, Switch, Explosion, Alarm,
    DoorOpen, SecurityDoor, Teleport, ComputerHack, PowerDown, Checkpoint,
    Hide, Unhide, GogglesOn, DisguiseOn, WallCrumble, FlameBurst, ElectricZap,
    Hurt, Death, VestBreak, MissionFailed, LevelComplete,
    PickupKey, PickupHealth, PickupAmmo, PickupVest, PickupItem, PickupWeapon, Coin, LetterBonus,
}

public enum Wave { Square, Triangle, Saw, Noise }

// One voice of a recipe: a waveform sliding from Hz0 to Hz1 over Duration,
// starting Start seconds into the sound. Noise reads Hz as its bit rate
// (low rumbles, high hiss). Decay shapes the fade: 0 holds level, 1 is a
// linear fade to silence, above 1 is snappier.
public readonly record struct Tone(
    Wave Wave, float Hz0, float Hz1, float Start, float Duration, float Volume,
    float Duty = 0.5f, float Vibrato = 0f, float Decay = 1f);

// Retro synth: every sound is a table row, rendered once at startup into a
// 16-bit mono AudioStreamWav, so there are no asset files. Autoloaded.
public partial class Sfx : Node
{
    public const string SfxBus = "SFX";
    public const string MusicBus = "Music";   // future music code plays on this bus

    private const int Rate = 22050;
    private const int PoolSize = 8;
    private const float MaxDistance = 700f;   // about one screen: farther enemies are silent
    private const ulong ThrottleMs = 40;      // a burst of the same sound plays once
    private const float VibratoRate = 7f;
    private const float Ceiling = 0.95f;

    private static Sfx _instance;

    private readonly Dictionary<Sound, AudioStreamWav> _streams = new();
    private readonly Dictionary<Sound, ulong> _lastPlayed = new();
    private readonly AudioStreamPlayer[] _players = new AudioStreamPlayer[PoolSize];
    private readonly AudioStreamPlayer2D[] _players2D = new AudioStreamPlayer2D[PoolSize];
    private int _next;
    private int _next2D;

    public static void Play(Sound sound) => _instance?.PlayOn(sound, null);

    public static void PlayAt(Sound sound, Vector2 globalPosition) => _instance?.PlayOn(sound, globalPosition);

    public override void _Ready()
    {
        _instance = this;
        ProcessMode = ProcessModeEnum.Always;   // the pause menu clicks while the tree is paused
        ApplySettings(Profile.Load());   // before any scene assigns Profile.Current

        foreach (Sound sound in Enum.GetValues<Sound>())
            _streams[sound] = Synthesize(sound);
        for (int i = 0; i < PoolSize; i++)
        {
            _players[i] = new AudioStreamPlayer { Bus = SfxBus };
            _players2D[i] = new AudioStreamPlayer2D { Bus = SfxBus, MaxDistance = MaxDistance };
            AddChild(_players[i]);
            AddChild(_players2D[i]);
        }
    }

    public override void _ExitTree()
    {
        if (_instance == this)
            _instance = null;
    }

    private void PlayOn(Sound sound, Vector2? at)
    {
        ulong now = Time.GetTicksMsec();
        if (_lastPlayed.TryGetValue(sound, out ulong last) && now - last < ThrottleMs)
            return;
        _lastPlayed[sound] = now;

        if (at is Vector2 position)
        {
            var player = _players2D[_next2D = (_next2D + 1) % PoolSize];
            player.GlobalPosition = position;
            player.Stream = _streams[sound];
            player.Play();
        }
        else
        {
            var player = _players[_next = (_next + 1) % PoolSize];
            player.Stream = _streams[sound];
            player.Play();
        }
    }

    // Creates the buses on first use and sets each from the profile: a
    // volume of 0 or a switched-off toggle mutes it.
    public static void ApplySettings(Profile profile = null)
    {
        profile ??= Profile.Current;
        ApplyBus(SfxBus, profile.SoundVolume, profile.SoundOn);
        ApplyBus(MusicBus, profile.MusicVolume, profile.MusicOn);
    }

    private static void ApplyBus(string name, int volume, bool on)
    {
        int bus = AudioServer.GetBusIndex(name);
        if (bus < 0)
        {
            AudioServer.AddBus();
            bus = AudioServer.BusCount - 1;
            AudioServer.SetBusName(bus, name);
            AudioServer.SetBusSend(bus, "Master");
        }
        AudioServer.SetBusVolumeDb(bus, volume <= 0 ? -80f : Mathf.LinearToDb(volume / 10f));
        AudioServer.SetBusMute(bus, volume <= 0 || !on);
    }

    private static Tone Sq(float hz0, float hz1, float start, float dur, float vol, float duty = 0.5f, float vib = 0f, float decay = 1f) =>
        new(Wave.Square, hz0, hz1, start, dur, vol, duty, vib, decay);
    private static Tone Tri(float hz0, float hz1, float start, float dur, float vol, float vib = 0f, float decay = 1f) =>
        new(Wave.Triangle, hz0, hz1, start, dur, vol, 0.5f, vib, decay);
    private static Tone Saw(float hz0, float hz1, float start, float dur, float vol, float vib = 0f, float decay = 1f) =>
        new(Wave.Saw, hz0, hz1, start, dur, vol, 0.5f, vib, decay);
    private static Tone Noise(float hz0, float hz1, float start, float dur, float vol, float decay = 1f) =>
        new(Wave.Noise, hz0, hz1, start, dur, vol, 0.5f, 0f, decay);
    // A held pitch of a jingle: fades gently so the notes stay distinct.
    private static Tone Note(Wave wave, float hz, float start, float dur, float vol, float duty = 0.5f) =>
        new(wave, hz, hz, start, dur, vol, duty, 0f, 0.5f);

    // The mix of each sound. Levels: explosion loudest, UI click quietest.
    private static readonly Dictionary<Sound, Tone[]> Recipes = new()
    {
        [Sound.UiClick] = new[] { Sq(1800, 1200, 0, 0.025f, 0.18f, 0.25f) },
        [Sound.Purchase] = new[] { Note(Wave.Square, 988, 0, 0.06f, 0.25f, 0.25f), Note(Wave.Square, 1319, 0.06f, 0.14f, 0.25f, 0.25f) },
        [Sound.CantAfford] = new[] { Sq(196, 190, 0, 0.1f, 0.3f), Sq(147, 140, 0.1f, 0.16f, 0.3f) },
        [Sound.Pause] = new[] { Tri(660, 440, 0, 0.09f, 0.35f) },
        [Sound.Unpause] = new[] { Tri(440, 660, 0, 0.09f, 0.35f) },

        [Sound.Jump] = new[] { Sq(280, 700, 0, 0.14f, 0.25f, 0.25f, decay: 0.8f) },
        [Sound.Swim] = new[] { Tri(200, 320, 0, 0.12f, 0.15f), Noise(1500, 800, 0, 0.12f, 0.07f) },
        [Sound.Land] = new[] { Noise(900, 300, 0, 0.06f, 0.2f), Tri(140, 70, 0, 0.08f, 0.3f) },
        [Sound.BarrelPush] = new[] { Noise(400, 300, 0, 0.12f, 0.16f), Saw(70, 60, 0, 0.12f, 0.14f) },

        [Sound.ShotPistol] = new[] { Noise(9000, 1500, 0, 0.12f, 0.4f, 2f), Sq(900, 120, 0, 0.08f, 0.3f, 0.125f) },
        [Sound.ShotRifle] = new[]
        {
            Noise(11000, 2000, 0, 0.08f, 0.35f, 2f), Sq(700, 150, 0, 0.06f, 0.25f, 0.125f),
            Noise(11000, 2000, 0.06f, 0.08f, 0.35f, 2f), Sq(700, 150, 0.06f, 0.06f, 0.25f, 0.125f),
            Noise(11000, 2000, 0.12f, 0.1f, 0.35f, 2f), Sq(700, 150, 0.12f, 0.06f, 0.25f, 0.125f),
        },
        [Sound.ShotMinigun] = new[]
        {
            Noise(7000, 1200, 0, 0.06f, 0.35f, 2f), Saw(200, 90, 0, 0.06f, 0.25f),
            Noise(7000, 1200, 0.045f, 0.07f, 0.35f, 2f), Saw(200, 90, 0.045f, 0.06f, 0.25f),
        },
        [Sound.Crossbow] = new[] { Saw(600, 150, 0, 0.15f, 0.25f, 0.04f), Noise(5000, 3000, 0, 0.03f, 0.18f) },
        [Sound.Chainsaw] = new[] { Saw(70, 90, 0, 0.25f, 0.3f, 0.06f, 0.2f), Noise(2500, 2500, 0, 0.25f, 0.12f, 0.2f) },
        [Sound.DynamiteThrow] = new[] { Tri(300, 500, 0, 0.15f, 0.28f), Noise(2000, 900, 0, 0.18f, 0.12f) },
        [Sound.EmptyClick] = new[] { Sq(1200, 900, 0, 0.015f, 0.22f), Sq(600, 600, 0.05f, 0.015f, 0.18f) },

        [Sound.EnemyShot] = new[] { Noise(7000, 1000, 0, 0.1f, 0.35f, 2f), Sq(600, 100, 0, 0.1f, 0.25f, 0.25f) },
        [Sound.EnemyMinigun] = new[]
        {
            Noise(6000, 1000, 0, 0.06f, 0.3f, 2f), Saw(160, 80, 0, 0.06f, 0.22f),
            Noise(6000, 1000, 0.05f, 0.07f, 0.3f, 2f), Saw(160, 80, 0.05f, 0.06f, 0.22f),
        },
        [Sound.EnemyPlasma] = new[] { Sq(1400, 300, 0, 0.22f, 0.25f, 0.125f, 0.02f), Tri(700, 200, 0, 0.2f, 0.2f) },
        [Sound.EnemyHit] = new[] { Sq(400, 200, 0, 0.08f, 0.3f), Noise(3000, 1000, 0, 0.06f, 0.2f) },
        [Sound.EnemyDie] = new[] { Saw(400, 60, 0, 0.35f, 0.3f), Noise(2500, 300, 0, 0.3f, 0.2f) },
        [Sound.RocketLaunch] = new[] { Noise(2500, 400, 0, 0.5f, 0.3f, 0.7f), Saw(120, 60, 0, 0.5f, 0.22f, decay: 0.7f) },

        [Sound.BulletHit] = new[] { Noise(4000, 2000, 0, 0.05f, 0.25f), Sq(1500, 600, 0, 0.03f, 0.12f) },
        [Sound.Switch] = new[] { Note(Wave.Square, 1000, 0, 0.05f, 0.25f), Note(Wave.Square, 1500, 0.06f, 0.08f, 0.25f) },
        [Sound.Explosion] = new[]
        {
            Noise(3000, 200, 0, 0.8f, 0.4f, 1.5f), Noise(800, 80, 0, 0.9f, 0.3f, 1.2f), Saw(90, 30, 0, 0.6f, 0.22f),
        },
        [Sound.Alarm] = new[]
        {
            Note(Wave.Square, 880, 0, 0.15f, 0.25f), Note(Wave.Square, 660, 0.16f, 0.15f, 0.25f),
            Note(Wave.Square, 880, 0.32f, 0.15f, 0.25f), Note(Wave.Square, 660, 0.48f, 0.15f, 0.25f),
        },

        [Sound.DoorOpen] = new[] { Saw(150, 90, 0, 0.12f, 0.25f), Noise(1200, 500, 0, 0.15f, 0.18f), Tri(300, 600, 0.1f, 0.1f, 0.22f) },
        [Sound.SecurityDoor] = new[] { Saw(80, 160, 0, 0.4f, 0.25f), Noise(600, 300, 0, 0.4f, 0.14f, 0.6f) },
        [Sound.Teleport] = new[] { Sq(300, 1500, 0, 0.35f, 0.2f, 0.25f, 0.03f), Tri(1500, 300, 0.15f, 0.3f, 0.22f) },
        [Sound.ComputerHack] = new[]
        {
            Note(Wave.Square, 784, 0, 0.06f, 0.22f), Note(Wave.Square, 988, 0.06f, 0.06f, 0.22f),
            Note(Wave.Square, 1175, 0.12f, 0.06f, 0.22f), Note(Wave.Square, 1568, 0.18f, 0.14f, 0.22f),
        },
        [Sound.PowerDown] = new[] { Saw(600, 60, 0, 0.5f, 0.25f), Sq(300, 40, 0, 0.5f, 0.18f) },
        [Sound.Checkpoint] = new[]
        {
            Note(Wave.Triangle, 659, 0, 0.08f, 0.4f), Note(Wave.Triangle, 880, 0.08f, 0.08f, 0.4f),
            Note(Wave.Triangle, 1319, 0.16f, 0.16f, 0.4f),
        },

        [Sound.Hide] = new[] { Tri(500, 250, 0, 0.1f, 0.28f) },
        [Sound.Unhide] = new[] { Tri(250, 500, 0, 0.1f, 0.28f) },
        [Sound.GogglesOn] = new[] { Tri(400, 1200, 0, 0.15f, 0.3f), Note(Wave.Square, 1200, 0.15f, 0.06f, 0.2f) },
        [Sound.DisguiseOn] = new[] { Sq(500, 250, 0, 0.2f, 0.2f, 0.5f, 0.05f), Note(Wave.Triangle, 800, 0.1f, 0.12f, 0.28f) },
        [Sound.WallCrumble] = new[] { Noise(1800, 300, 0, 0.35f, 0.35f), Noise(500, 100, 0, 0.3f, 0.28f) },
        [Sound.FlameBurst] = new[] { Noise(1800, 900, 0, 0.5f, 0.28f, 0.5f), Saw(90, 70, 0, 0.5f, 0.18f, decay: 0.5f) },
        [Sound.ElectricZap] = new[] { Noise(9000, 5000, 0, 0.3f, 0.22f, 0.5f), Saw(110, 130, 0, 0.3f, 0.22f, decay: 0.5f) },

        [Sound.Hurt] = new[] { Sq(500, 150, 0, 0.25f, 0.3f), Noise(2000, 500, 0, 0.15f, 0.22f) },
        [Sound.Death] = new[] { Saw(600, 40, 0, 0.8f, 0.3f, 0.03f), Sq(400, 50, 0.1f, 0.7f, 0.22f) },
        [Sound.VestBreak] = new[] { Noise(5000, 1500, 0, 0.12f, 0.35f), Sq(900, 300, 0, 0.1f, 0.25f), Tri(300, 150, 0.08f, 0.15f, 0.25f) },
        [Sound.MissionFailed] = new[]
        {
            Note(Wave.Triangle, 392, 0, 0.2f, 0.4f), Note(Wave.Triangle, 330, 0.2f, 0.2f, 0.4f),
            Note(Wave.Triangle, 262, 0.4f, 0.2f, 0.4f), Tri(196, 180, 0.6f, 0.5f, 0.4f, 0.02f, 0.4f),
        },
        [Sound.LevelComplete] = new[]
        {
            Note(Wave.Square, 523, 0, 0.12f, 0.25f, 0.25f), Note(Wave.Square, 659, 0.12f, 0.12f, 0.25f, 0.25f),
            Note(Wave.Square, 784, 0.24f, 0.12f, 0.25f, 0.25f), Note(Wave.Square, 1047, 0.36f, 0.12f, 0.25f, 0.25f),
            Note(Wave.Square, 1047, 0.5f, 0.4f, 0.25f, 0.25f), Note(Wave.Triangle, 523, 0.5f, 0.4f, 0.3f),
        },

        [Sound.PickupKey] = new[] { Note(Wave.Square, 1047, 0, 0.06f, 0.25f, 0.25f), Note(Wave.Square, 1568, 0.06f, 0.14f, 0.25f, 0.25f) },
        [Sound.PickupHealth] = new[]
        {
            Note(Wave.Triangle, 523, 0, 0.07f, 0.4f), Note(Wave.Triangle, 659, 0.07f, 0.07f, 0.4f),
            Note(Wave.Triangle, 784, 0.14f, 0.07f, 0.4f), Note(Wave.Triangle, 1047, 0.21f, 0.18f, 0.4f),
        },
        [Sound.PickupAmmo] = new[]
        {
            Note(Wave.Square, 700, 0, 0.03f, 0.22f), Note(Wave.Square, 900, 0.05f, 0.03f, 0.22f), Note(Wave.Square, 1400, 0.1f, 0.06f, 0.22f),
        },
        [Sound.PickupVest] = new[] { Note(Wave.Triangle, 392, 0, 0.08f, 0.4f), Note(Wave.Triangle, 587, 0.08f, 0.14f, 0.4f) },
        [Sound.PickupItem] = new[]
        {
            Note(Wave.Triangle, 659, 0, 0.07f, 0.4f), Note(Wave.Triangle, 988, 0.07f, 0.07f, 0.4f), Note(Wave.Triangle, 1319, 0.14f, 0.12f, 0.4f),
        },
        [Sound.PickupWeapon] = new[]
        {
            Noise(2500, 2500, 0, 0.025f, 0.3f), Saw(200, 400, 0.03f, 0.08f, 0.22f),
            Noise(2500, 2500, 0.12f, 0.025f, 0.3f), Note(Wave.Square, 1200, 0.15f, 0.12f, 0.2f, 0.25f),
        },
        [Sound.Coin] = new[] { Note(Wave.Square, 988, 0, 0.05f, 0.25f, 0.25f), Sq(1319, 1319, 0.05f, 0.2f, 0.25f, 0.25f, decay: 1.5f) },
        [Sound.LetterBonus] = new[]
        {
            Note(Wave.Square, 523, 0, 0.08f, 0.22f, 0.25f), Note(Wave.Square, 659, 0.08f, 0.08f, 0.22f, 0.25f),
            Note(Wave.Square, 784, 0.16f, 0.08f, 0.22f, 0.25f), Note(Wave.Square, 1047, 0.24f, 0.08f, 0.22f, 0.25f),
            Note(Wave.Square, 1319, 0.32f, 0.08f, 0.22f, 0.25f), Note(Wave.Square, 1568, 0.4f, 0.08f, 0.22f, 0.25f),
            Note(Wave.Square, 2093, 0.48f, 0.5f, 0.22f, 0.25f), Note(Wave.Triangle, 1047, 0.48f, 0.5f, 0.3f),
        },
    };

    // Renders a recipe to 16-bit mono PCM. Each voice has a short attack and
    // release ramp so no sound starts or ends with a click.
    public static AudioStreamWav Synthesize(Sound sound)
    {
        var tones = Recipes[sound];
        float total = 0f;
        foreach (var tone in tones)
            total = Mathf.Max(total, tone.Start + tone.Duration);
        var mix = new float[(int)(total * Rate) + 1];
        var rng = new Random((int)sound * 7919 + 1);
        const int ramp = Rate / 250;

        foreach (var tone in tones)
        {
            int first = (int)(tone.Start * Rate);
            int count = (int)(tone.Duration * Rate);
            float phase = 0f;
            float noise = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                float hz = Mathf.Lerp(tone.Hz0, tone.Hz1, t)
                    * (1f + tone.Vibrato * Mathf.Sin(Mathf.Tau * VibratoRate * i / Rate));
                phase += Mathf.Min(hz, Rate / 2f) / Rate;
                if (phase >= 1f)
                {
                    phase -= 1f;
                    noise = rng.NextSingle() * 2f - 1f;
                }
                float value = tone.Wave switch
                {
                    Wave.Square => phase < tone.Duty ? 1f : -1f,
                    Wave.Triangle => 4f * Mathf.Abs(phase - 0.5f) - 1f,
                    Wave.Saw => 2f * phase - 1f,
                    _ => noise,
                };
                float envelope = Mathf.Pow(1f - t, tone.Decay)
                    * Mathf.Min(1f, (float)i / ramp) * Mathf.Min(1f, (float)(count - i) / ramp);
                mix[first + i] += value * tone.Volume * envelope;
            }
        }

        var data = new byte[mix.Length * 2];
        for (int i = 0; i < mix.Length; i++)
        {
            short pcm = (short)(Mathf.Clamp(mix[i], -Ceiling, Ceiling) * short.MaxValue);
            data[2 * i] = (byte)(pcm & 0xFF);
            data[2 * i + 1] = (byte)((pcm >> 8) & 0xFF);
        }
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = Rate,
            Stereo = false,
            Data = data,
        };
    }

    // Writes every sound to <dir>/<name>.wav so a human can audition them.
    // Returns name, byte size and peak (0-1) per file.
    public static List<(string Name, int Bytes, float Peak)> DumpWavs(string dir)
    {
        DirAccess.MakeDirRecursiveAbsolute(dir);
        var report = new List<(string, int, float)>();
        foreach (Sound sound in Enum.GetValues<Sound>())
        {
            var stream = Synthesize(sound);
            string name = sound.ToString();
            stream.SaveToWav($"{dir}/{name}.wav");
            int peak = 0;
            for (int i = 0; i < stream.Data.Length; i += 2)
                peak = Math.Max(peak, Math.Abs((short)(stream.Data[i] | (stream.Data[i + 1] << 8))));
            report.Add((name, stream.Data.Length, peak / (float)short.MaxValue));
        }
        return report;
    }
}
