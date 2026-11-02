using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Look of the corruption screen's background (Inspector choice on <see cref="PowerUpScreen"/>).</summary>
public enum CorruptionBackdrop
{
    /// <summary>Digital noise: rolling band, scanlines, edge noise, tears.</summary>
    Glitch,
    /// <summary>A virus in the circuit: faint traces under the cards, data packets running along them, node pings.</summary>
    Cyber
}

/// <summary>
/// The Cyber backdrop of the corruption screen, and the switch between backdrops. Both share the blood-red field;
/// Glitch lives in <c>PowerUpScreen.Corruption.cs</c>. Cyber is a faint circuit board under the inventory: small data
/// packets with short trails run along its traces (lighting each trace as they pass), the node they reach flares and
/// sometimes sends out a soft ring, a scan line sweeps down now and then and a few binary motes blink in the air.
/// Calm in the inventory; with the virus of the sequence (level near 1) packets multiply, speed up and ping more.
/// </summary>
public partial class PowerUpScreen
{
    [Header("Corruption screen")]
    [Tooltip("Background of the corruption screen. Glitch: digital noise and tears. Cyber: a virus running through a faint circuit board.")]
    [SerializeField] private CorruptionBackdrop backdrop = CorruptionBackdrop.Cyber;

    private const int TraceCount = 18;
    private const int PacketCount = 14;
    private const int PacketTrail = 4;
    private const int MoteCount = 18;
    private const int PingCount = 4;
    private const float TraceWidth = 3f;
    private const float SweepTime = 7f;  // one pass of the scan line, then a pause
    private const float SweepPause = 3f;

    private static readonly Color TraceColor = new(0.9f, 0.12f, 0.22f);

    private sealed class Trace
    {
        public readonly List<Vector2> Points = new();
        public readonly List<float> Lengths = new(); // cumulative length at each point
        public readonly List<Image> Segments = new();
        public Image Start, End;
        public float Lit;      // glow left by a packet passing, decays
        public float EndGlow;  // flare of the end node when a packet arrives
        public float Length => Lengths[^1];
    }

    private sealed class Packet
    {
        public Image[] Parts;
        public Trace Trace;
        public float Distance, Speed, Wait;
        public bool Outward; // runs from the inner end back to the edge
        public Color Color;
    }

    private sealed class Mote
    {
        public Image Image;
        public Vector2 Pos, Velocity;
        public float Phase, Rate;
        public Color Color;
    }

    private sealed class Ping
    {
        public Image Image;
        public float Time = -1f; // < 0 = free
        public Color Color;
    }

    private RectTransform glitchLayer;
    private RectTransform cyberLayer;
    private RawImage sweep;
    private readonly List<Trace> traces = new();
    private readonly List<Packet> packets = new();
    private readonly List<Mote> motes = new();
    private readonly List<Ping> pings = new();
    private float sweepClock;

    private void BuildCyberLayer(RectTransform background)
    {
        cyberLayer = NewRect("Cyber", background);
        Stretch(cyberLayer);

        sweep = NewRawIn("Sweep", cyberLayer, BuildSweepTexture());
        sweep.enabled = true;
        sweep.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        sweep.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        sweep.rectTransform.sizeDelta = new Vector2(0f, 260f);

        BuildTraces(new System.Random(11));

        for (int i = 0; i < PingCount; i++)
        {
            Image ring = NewImage("Ping", cyberLayer, Color.clear, false);
            ring.sprite = UiSprites.Ring;
            ring.type = Image.Type.Simple;
            Place(ring.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero);
            ring.enabled = false;
            pings.Add(new Ping { Image = ring });
        }

        for (int i = 0; i < PacketCount; i++)
        {
            var packet = new Packet { Parts = new Image[PacketTrail] };
            for (int j = 0; j < PacketTrail; j++) packet.Parts[j] = Square(cyberLayer, "Packet", 4f);
            packet.Wait = Random.Range(0f, 3f); // start staggered
            packets.Add(packet);
        }

        for (int i = 0; i < MoteCount; i++)
        {
            var mote = new Mote
            {
                Image = Square(cyberLayer, "Mote", Random.value < 0.7f ? 4f : 6f),
                Pos = new Vector2(Random.Range(-560f, 560f), Random.Range(-1200f, 1200f)),
                Velocity = Random.insideUnitCircle.normalized * Random.Range(6f, 18f),
                Phase = Random.Range(0f, 6.3f),
                Rate = Random.Range(0.6f, 1.6f),
                Color = Random.value < 0.2f ? CorruptCyan : TraceColor
            };
            motes.Add(mote);
        }
    }

    // Circuit traces from the screen edges inward, in straight runs with 45 or 90 degree bends, a node at each end.
    // They run under the inventory cards and show in the gaps between them.
    private void BuildTraces(System.Random rng)
    {
        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        for (int i = 0; i < TraceCount; i++)
        {
            var trace = new Trace();
            Vector2 pos, dir;
            if (i < 14)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                pos = new Vector2(side * 600f, Mathf.Lerp(-1150f, 1150f, ((i / 2) + Rand(0.15f, 0.85f)) / 7f));
                dir = new Vector2(-side, 0f);
            }
            else
            {
                float side = i % 2 == 0 ? -1f : 1f; // a few from the top and the bottom
                pos = new Vector2(Rand(-420f, 420f), side * 1250f);
                dir = new Vector2(0f, -side);
            }

            trace.Points.Add(pos);
            int runs = (int)Rand(3f, 7f);
            for (int r = 0; r < runs; r++)
            {
                pos += dir * Rand(60f, 220f);
                trace.Points.Add(pos);
                if (Mathf.Abs(pos.x) < 60f) break;
                // Bend 45 or 90 degrees, never back toward where it came from.
                float turn = (rng.NextDouble() < 0.5 ? 45f : 90f) * (rng.NextDouble() < 0.5 ? -1f : 1f);
                Vector2 next = Quaternion.Euler(0f, 0f, turn) * dir;
                if (Vector2.Dot(next, trace.Points[0] - pos) > 0.5f * (trace.Points[0] - pos).magnitude) next = dir;
                dir = new Vector2(Mathf.Round(next.x * 1000f) / 1000f, Mathf.Round(next.y * 1000f) / 1000f).normalized;
            }

            float total = 0f;
            trace.Lengths.Add(0f);
            for (int p = 1; p < trace.Points.Count; p++)
            {
                Vector2 a = trace.Points[p - 1], b = trace.Points[p];
                total += Vector2.Distance(a, b);
                trace.Lengths.Add(total);
                Image segment = NewImage("Trace", cyberLayer, Color.clear, false);
                segment.sprite = null;
                segment.type = Image.Type.Simple;
                RectTransform r = segment.rectTransform;
                Place(r, Vector2.zero, (a + b) * 0.5f, new Vector2(Vector2.Distance(a, b) + TraceWidth, TraceWidth));
                r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                trace.Segments.Add(segment);
            }
            trace.Start = Square(cyberLayer, "Node", 10f);
            trace.Start.rectTransform.anchoredPosition = trace.Points[0];
            trace.End = Square(cyberLayer, "Node", 12f);
            trace.End.rectTransform.anchoredPosition = trace.Points[^1];
            traces.Add(trace);
        }
    }

    // Shows the chosen backdrop's layer and hides the other.
    private void ApplyBackdrop()
    {
        if (glitchLayer != null) glitchLayer.gameObject.SetActive(backdrop == CorruptionBackdrop.Glitch);
        if (cyberLayer != null) cyberLayer.gameObject.SetActive(backdrop == CorruptionBackdrop.Cyber);
    }

    private void AnimateCyber(float dt, float time, float level)
    {
        float glow = 0.8f + 0.05f * Mathf.Sin(time * 0.7f) + 0.15f * level;
        field.color = new Color(glow, glow, glow, 0.96f);

        // Traces: barely there, a soft glow where a packet just ran.
        foreach (Trace trace in traces)
        {
            trace.Lit = Mathf.MoveTowards(trace.Lit, 0f, dt * 0.8f);
            trace.EndGlow = Mathf.MoveTowards(trace.EndGlow, 0f, dt * 1.6f);
            Color line = WithAlpha(TraceColor, 0.07f + 0.08f * level + 0.14f * trace.Lit);
            foreach (Image segment in trace.Segments) segment.color = line;
            trace.Start.color = WithAlpha(TraceColor, 0.14f + 0.1f * level);
            trace.End.color = WithAlpha(Color.Lerp(TraceColor, Color.white, 0.5f * trace.EndGlow), 0.16f + 0.1f * level + 0.6f * trace.EndGlow);
            trace.End.rectTransform.localScale = Vector3.one * (1f + 0.6f * trace.EndGlow);
        }

        // Packets: more of them run, and faster, as the virus takes hold.
        int running = Mathf.RoundToInt(Mathf.Lerp(6f, PacketCount, level));
        for (int i = 0; i < packets.Count; i++) AnimatePacket(packets[i], dt, level, i < running);

        // Pings: a soft ring from a node a packet reached.
        foreach (Ping ping in pings)
        {
            if (ping.Time < 0f) continue;
            ping.Time += dt;
            float k = Mathf.Clamp01(ping.Time / 1.1f);
            float size = Mathf.Lerp(16f, 200f, 1f - (1f - k) * (1f - k));
            ping.Image.rectTransform.sizeDelta = new Vector2(size, size);
            ping.Image.color = WithAlpha(ping.Color, 0.28f * (1f - k) * (1f - k));
            if (k >= 1f)
            {
                ping.Time = -1f;
                ping.Image.enabled = false;
            }
        }

        // Scan line: a soft band sweeping down, then a pause.
        sweepClock += dt * (1f + 1.5f * level);
        float cycle = Mathf.Repeat(sweepClock, SweepTime + SweepPause);
        float half = root.rect.height * 0.5f + 200f;
        sweep.enabled = cycle < SweepTime;
        if (sweep.enabled)
        {
            float k = cycle / SweepTime;
            sweep.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(half, -half, k));
            sweep.color = WithAlpha(TraceColor, (0.05f + 0.08f * level) * Mathf.Sin(Mathf.PI * k));
        }

        // Motes: tiny bits drifting and blinking slowly, wrapping around the screen.
        float halfHeight = root.rect.height * 0.5f;
        foreach (Mote mote in motes)
        {
            mote.Pos += mote.Velocity * (1f + level) * dt;
            if (mote.Pos.x < -580f) mote.Pos.x += 1160f;
            if (mote.Pos.x > 580f) mote.Pos.x -= 1160f;
            if (mote.Pos.y < -halfHeight - 20f) mote.Pos.y += 2f * halfHeight + 40f;
            if (mote.Pos.y > halfHeight + 20f) mote.Pos.y -= 2f * halfHeight + 40f;
            float blink = 0.5f + 0.5f * Mathf.Sin(time * mote.Rate * 2f + mote.Phase);
            mote.Image.rectTransform.anchoredPosition = new Vector2(Mathf.Round(mote.Pos.x / 2f) * 2f, Mathf.Round(mote.Pos.y / 2f) * 2f);
            mote.Image.color = WithAlpha(mote.Color, (0.08f + 0.1f * level) + 0.22f * blink * blink);
        }
    }

    private void AnimatePacket(Packet packet, float dt, float level, bool allowed)
    {
        if (packet.Trace == null)
        {
            foreach (Image part in packet.Parts) part.enabled = false;
            packet.Wait -= dt * (1f + 3f * level);
            if (packet.Wait > 0f || !allowed) return;
            packet.Trace = traces[Random.Range(0, traces.Count)];
            packet.Outward = Random.value < 0.3f;
            packet.Distance = 0f;
            packet.Speed = Random.Range(140f, 280f);
            packet.Color = Random.value < 0.18f ? CorruptCyan : Random.value < 0.25f ? Color.white : CorruptRed;
        }

        Trace trace = packet.Trace;
        packet.Distance += packet.Speed * (1f + 1.6f * level) * dt;
        trace.Lit = Mathf.Max(trace.Lit, 0.8f);
        for (int j = 0; j < packet.Parts.Length; j++)
        {
            Image part = packet.Parts[j];
            float d = packet.Distance - j * 12f;
            bool on = d > 0f && d < trace.Length;
            part.enabled = on;
            if (!on) continue;
            Vector2 p = PointOn(trace, packet.Outward ? trace.Length - d : d);
            part.rectTransform.anchoredPosition = new Vector2(Mathf.Round(p.x / 2f) * 2f, Mathf.Round(p.y / 2f) * 2f);
            float size = j == 0 ? 10f : 8f - j * 1.5f;
            part.rectTransform.sizeDelta = new Vector2(size, size);
            part.color = WithAlpha(j == 0 ? Color.Lerp(packet.Color, Color.white, 0.35f) : packet.Color, (j == 0 ? 0.75f : 0.45f / j) * (0.75f + 0.25f * level));
        }

        // Arrived (the whole trail is in): the node it reached flares, sometimes pings.
        if (packet.Distance - (packet.Parts.Length - 1) * 12f >= trace.Length)
        {
            if (packet.Outward) trace.Lit = 1f;
            else trace.EndGlow = 1f;
            if (Random.value < 0.3f + 0.5f * level)
                StartPing(packet.Outward ? trace.Points[0] : trace.Points[^1], packet.Color);
            packet.Trace = null;
            packet.Wait = Random.Range(0.4f, 2.6f);
        }
    }

    private void StartPing(Vector2 at, Color color)
    {
        foreach (Ping ping in pings)
        {
            if (ping.Time >= 0f) continue;
            ping.Time = 0f;
            ping.Color = color;
            ping.Image.enabled = true;
            ping.Image.rectTransform.anchoredPosition = at;
            return;
        }
    }

    private static Vector2 PointOn(Trace trace, float distance)
    {
        for (int i = 1; i < trace.Points.Count; i++)
        {
            if (distance > trace.Lengths[i]) continue;
            float span = trace.Lengths[i] - trace.Lengths[i - 1];
            float k = span > 0f ? (distance - trace.Lengths[i - 1]) / span : 0f;
            return Vector2.Lerp(trace.Points[i - 1], trace.Points[i], k);
        }
        return trace.Points[^1];
    }

    private static Image Square(RectTransform parent, string name, float size)
    {
        Image image = NewImage(name, parent, Color.clear, false);
        image.sprite = null;
        image.type = Image.Type.Simple;
        Place(image.rectTransform, Vector2.zero, Vector2.zero, new Vector2(size, size));
        return image;
    }

    // A soft vertical falloff for the scan line: transparent at both ends, bright in the middle.
    private static Texture2D BuildSweepTexture()
    {
        var texture = new Texture2D(1, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 32; y++)
        {
            float k = 1f - Mathf.Abs(y / 31f - 0.5f) * 2f;
            texture.SetPixel(0, y, new Color(1f, 1f, 1f, k * k * k));
        }
        texture.Apply(false);
        return texture;
    }
}
