using Godot;

namespace HackerOrSoldier;

// The outdoors behind the tile layers: the level's night or day sky, drawn
// by a pixel-art shader over the whole level rect. Stars grow in a pixel at
// a time, twinkle, fade out, and come back elsewhere; clouds drift with the
// wind, billow in and dissolve through an ordered dither. Every cycle length
// divides Godot's 3600 s TIME rollover, so the sky never jumps when it wraps.
public partial class SkyBackdrop : Node2D
{
    private static Shader _shader;
    private readonly ShaderMaterial _material = new();
    private Vector2 _size;

    public SkyBackdrop()
    {
        _material.Shader = _shader ??= new Shader { Code = ShaderCode };
        Material = _material;
    }

    public void Apply(LevelData level) =>
        Apply(new Vector2(level.Width, level.Height) * TileSetBuilder.TileSize, level.Sky == "day");

    public void Apply(Vector2 size, bool day)
    {
        _size = size;
        _material.SetShaderParameter("is_day", day);
        _material.SetShaderParameter("level_height", size.Y);
        QueueRedraw();
    }

    public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, _size), Colors.White);

    private const string ShaderCode = """
        shader_type canvas_item;

        uniform bool is_day = false;
        uniform float level_height = 384.0;

        varying vec2 sky_px;

        void vertex() {
            sky_px = VERTEX;
        }

        vec4 hash43(vec3 p) {
            vec4 p4 = fract(vec4(p.xyzx) * vec4(0.1031, 0.1030, 0.0973, 0.1099));
            p4 += dot(p4, p4.wzxy + 33.33);
            return fract((p4.xxyz + p4.yzzw) * p4.zywx);
        }

        float bayer2(vec2 a) { a = floor(a); return fract(a.x / 2.0 + a.y * a.y * 0.75); }
        float bayer4(vec2 a) { return bayer2(0.5 * a) * 0.25 + bayer2(a); }

        // Cycle lengths in seconds, all divisors of 3600.
        float star_period(float r) {
            return r < 0.17 ? 6.0 : r < 0.34 ? 8.0 : r < 0.5 ? 9.0 : r < 0.67 ? 10.0 : r < 0.84 ? 12.0 : 15.0;
        }
        float cloud_period(float r) {
            return r < 0.17 ? 20.0 : r < 0.34 ? 24.0 : r < 0.5 ? 30.0 : r < 0.67 ? 36.0 : r < 0.84 ? 40.0 : 45.0;
        }

        // One star slot per 16 px cell. Each cycle the slot rolls a new star
        // (or none) at a new spot; it brightens one palette step at a time,
        // holds and twinkles, dims, then stays dark until the next cycle.
        // Returns the brightness step 0-3; tint picks the bright color.
        int star(vec2 p, out int tint) {
            const float C = 16.0;
            vec2 cell = floor(p / C);
            vec2 local = p - cell * C;
            vec4 h = hash43(vec3(cell, 3.0));
            float period = star_period(h.x);
            float t = TIME / period + h.y;
            float cyc = mod(floor(t), 3600.0 / period);
            float life = fract(t);
            vec4 s = hash43(vec3(cell, cyc + 11.0));
            tint = h.z < 0.15 ? 1 : h.z < 0.3 ? 2 : 0;
            if (s.x > 0.5)
                return 0;
            int peak = s.w < 0.45 ? 1 : s.w < 0.8 ? 2 : 3;
            float e = clamp(min(life, 0.8 - life) / 0.12, 0.0, 1.0);
            int lvl = min(int(ceil(e * 3.0)), peak);
            if (lvl == 3 && sin(TIME * (1.5 + h.w * 3.0) + h.z * 6.283) > 0.8)
                lvl = 2;
            vec2 d = abs(local - (vec2(2.0) + floor(s.yz * (C - 4.0))));
            float dist = d.x + d.y;
            if (dist < 0.5)
                return lvl;
            if (s.w > 0.92 && dist < 1.5)
                return max(lvl - 2, 0);   // cross arms on the brightest stars
            return 0;
        }

        // Clouds ride horizontal lanes; each lane drifts right at its own
        // whole-pixel speed and is cut into slots that hold at most one
        // cloud. A cloud lives one cycle: it swells from 60% size while its
        // dither fills in, holds, then shrinks and dissolves.
        // Returns 0 (sky), 1 (lit body) or 2 (shaded underside).
        int cloud(vec2 p, float lane_h, float slot_w, float size, float layer, float flatness, float max_alpha) {
            float lane = floor(p.y / lane_h);
            vec4 hl = hash43(vec3(lane, layer, 7.0));
            if (hl.y < 0.25)
                return 0;   // a clear lane
            float m = 30.0 + floor(hl.x * 40.0);   // slots drifted per TIME rollover
            float xs = p.x - floor(TIME * slot_w * m / 3600.0);
            float slot = floor(xs / slot_w);
            float slot_id = mod(slot, m);
            vec2 q = vec2(xs - slot * slot_w, p.y - lane * lane_h);

            vec4 h = hash43(vec3(slot_id, lane, layer));
            float period = cloud_period(h.x);
            float t = TIME / period + h.y;
            float cyc = mod(floor(t), 3600.0 / period);
            float life = fract(t);
            vec4 c = hash43(vec3(slot_id + layer * 97.0, lane, cyc + 5.0));
            if (c.x > 0.6)
                return 0;
            float env = clamp(min(life, 0.9 - life) / 0.15, 0.0, 1.0);
            if (env * max_alpha <= bayer4(q))
                return 0;

            // Place the full-size cloud anywhere it fits inside its slot.
            float squash = flatness / 0.3;
            float hw0 = size * (0.55 + 0.45 * c.y);
            float top = hw0 * (0.7 * flatness + 0.54 * squash);
            float bottom = hw0 * 0.55 * flatness;
            vec2 center = floor(vec2(
                hw0 + 4.0 + c.z * (slot_w - 2.0 * hw0 - 8.0),
                top + 2.0 + c.w * (lane_h - top - bottom - 4.0)));
            float hw = hw0 * mix(0.6, 1.0, env);
            float hh = hw * flatness;
            vec2 d = q - center;
            if (d.y >= hh * 0.55)
                return 0;   // flat bottom

            vec2 e = d / vec2(hw, hh);
            bool base = dot(e, e) <= 1.0;
            vec2 d1 = (d - vec2(-0.35 * hw, -0.5 * hh)) / vec2(1.0, squash);
            float r1 = hw * (0.34 + 0.12 * h.z);
            vec2 d2 = (d - vec2(0.2 * hw, -0.7 * hh)) / vec2(1.0, squash);
            float r2 = hw * (0.42 + 0.12 * h.w);
            if (!base && dot(d1, d1) > r1 * r1 && dot(d2, d2) > r2 * r2)
                return 0;
            return d.y > hh * 0.1 ? 2 : 1;
        }

        // Base color: a vertical gradient over the level height, banded and
        // dithered only in a thin seam at each band edge.
        vec3 gradient(vec2 p, vec3 top, vec3 mid, vec3 bottom) {
            float v = clamp(p.y / max(level_height, 1.0), 0.0, 1.0) * 2.0;
            float band = floor(v);
            float seam = 12.0 / max(level_height, 1.0) * 2.0;
            if (bayer4(p) < smoothstep(1.0 - seam, 1.0, fract(v)))
                band += 1.0;
            return band < 0.5 ? top : band < 1.5 ? mid : bottom;
        }

        void fragment() {
            vec2 p = floor(sky_px);
            vec3 col;
            if (is_day) {
                col = gradient(p, vec3(0.184, 0.384, 0.839), vec3(0.231, 0.435, 0.878), vec3(0.333, 0.522, 0.918));
                int far = cloud(p, 56.0, 160.0, 28.0, 1.0, 0.3, 1.0);
                if (far == 1) col = vec3(0.722, 0.808, 0.957);
                if (far == 2) col = vec3(0.580, 0.698, 0.925);
                int near = cloud(p, 80.0, 224.0, 48.0, 2.0, 0.3, 1.0);
                if (near == 1) col = vec3(0.949, 0.965, 0.988);
                if (near == 2) col = vec3(0.769, 0.827, 0.941);
            } else {
                col = gradient(p, vec3(0.043, 0.055, 0.102), vec3(0.051, 0.063, 0.122), vec3(0.067, 0.078, 0.149));
                int tint;
                int lvl = star(p, tint);
                if (lvl == 1) col = vec3(0.165, 0.184, 0.282);
                if (lvl == 2) col = vec3(0.416, 0.439, 0.565);
                if (lvl == 3) col = tint == 1 ? vec3(0.949, 0.851, 0.627)
                    : tint == 2 ? vec3(0.659, 0.816, 0.949) : vec3(0.910, 0.910, 0.941);
                int wisp = cloud(p, 96.0, 256.0, 64.0, 3.0, 0.16, 0.55);
                if (wisp == 1) col = vec3(0.098, 0.114, 0.200);
                if (wisp == 2) col = vec3(0.075, 0.090, 0.165);
            }
            COLOR = vec4(col, 1.0);
        }
        """;
}
