using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Synthesizes the game's retro sound effects (square/triangle waves, noise, pitch sweeps, arpeggios),
/// gives them an 8-bit flavour (lower sample rate, fewer volume steps) and saves them as .wav files
/// in Assets/Audio/Sfx, one per <see cref="Sfx"/> value. Tweak a recipe below and run the menu again.
/// Any file can also be replaced by a hand-made sound with the same name.
/// </summary>
public static class RetroSfxGenerator
{
    private const int SampleRate = 44100;
    private const string Folder = "Assets/Audio/Sfx";

    private enum Wave { Square, Triangle, Saw, Noise }

    // Musical notes (Hz) used by the jingles.
    private const float G4 = 392f, C5 = 523f, E5 = 659f, G5 = 784f, B5 = 988f, C6 = 1047f, D6 = 1175f, E6 = 1319f,
        G6 = 1568f, B6 = 1976f, D7 = 2349f, A4 = 440f, F4 = 349f, C4 = 262f, G3 = 196f;

    [MenuItem("Tools/Brick Breaker/Generate Sound Effects")]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(Folder);

        // Ball
        Save(Sfx.Launch, Mix((0f, Tone(0.14f, 300f, 900f, Wave.Square, 0.6f, decay: 0.08f)),
                             (0f, Tone(0.14f, 150f, 450f, Wave.Triangle, 0.5f, decay: 0.08f))));
        Save(Sfx.PaddleHit, Mix((0f, Tone(0.09f, 520f, 430f, Wave.Square, 0.6f, duty: 0.25f, decay: 0.04f)),
                                (0f, Tone(0.09f, 260f, 215f, Wave.Triangle, 0.6f, decay: 0.05f))));
        Save(Sfx.WallBounce, Tone(0.06f, 330f, 290f, Wave.Triangle, 0.8f, decay: 0.03f));
        Save(Sfx.BallLost, Tone(0.5f, 600f, 80f, Wave.Square, 0.6f, decay: 0.3f, vibratoHz: 9f, vibratoDepth: 0.05f));

        // Bricks and wall
        Save(Sfx.BrickHit, Tone(0.07f, 880f, 830f, Wave.Square, 0.5f, duty: 0.25f, decay: 0.03f));
        Save(Sfx.BrickBreak, Mix((0f, Tone(0.22f, 6000f, 1500f, Wave.Noise, 0.6f, decay: 0.08f)),
                                 (0f, Tone(0.15f, 700f, 180f, Wave.Square, 0.5f, decay: 0.06f))));
        Save(Sfx.WallStep, Mix((0f, Tone(0.3f, 90f, 50f, Wave.Triangle, 1f, decay: 0.12f)),
                               (0f, Tone(0.2f, 1200f, 300f, Wave.Noise, 0.3f, decay: 0.05f))));

        // XP and progression
        Save(Sfx.OrbLaunch, Tone(0.05f, 1100f, 1500f, Wave.Triangle, 0.5f, decay: 0.03f));
        Save(Sfx.OrbCollect, Mix((0f, Tone(0.08f, 1320f, 1760f, Wave.Square, 0.5f, decay: 0.05f)),
                                 (0.03f, Tone(0.06f, 2640f, 2640f, Wave.Triangle, 0.3f, decay: 0.03f))));
        Save(Sfx.LevelUp, Mix((0f, Arp(new[] { C5, E5, G5 }, 0.075f, Wave.Square, 0.5f)),
                              (0.225f, Tone(0.4f, C6, C6, Wave.Square, 0.5f, decay: 0.25f, vibratoHz: 7f, vibratoDepth: 0.02f)),
                              (0.225f, Tone(0.4f, C5, C5, Wave.Triangle, 0.4f, decay: 0.25f))));
        Save(Sfx.ClearFanfare, Mix((0f, Arp(new[] { G4, C5, E5, G5, C6 }, 0.055f, Wave.Square, 0.45f)),
                                   (0.275f, Tone(0.5f, C6, C6, Wave.Triangle, 0.4f, decay: 0.3f)),
                                   (0.275f, Tone(0.5f, E6, E6, Wave.Triangle, 0.35f, decay: 0.3f)),
                                   (0.275f, Tone(0.5f, G6, G6, Wave.Square, 0.25f, decay: 0.3f, vibratoHz: 8f, vibratoDepth: 0.02f))));

        // Level-up cards
        Save(Sfx.CardsAppear, Mix((0f, Tone(0.35f, 800f, 6000f, Wave.Noise, 0.25f, decay: 0.2f)),
                                  (0f, Tone(0.3f, 220f, 660f, Wave.Triangle, 0.4f, decay: 0.2f))));
        Save(Sfx.CardSelect, Tone(0.07f, 660f, 990f, Wave.Square, 0.5f, duty: 0.25f, decay: 0.05f));
        Save(Sfx.Confirm, Mix((0f, Tone(0.08f, G5, G5, Wave.Square, 0.5f, decay: 0.06f)),
                              (0.08f, Tone(0.18f, D6, D6, Wave.Square, 0.5f, decay: 0.1f))));
        Save(Sfx.Denied, Mix((0f, Tone(0.08f, 180f, 170f, Wave.Square, 0.6f, duty: 0.1f)),
                             (0.1f, Tone(0.1f, 150f, 140f, Wave.Square, 0.6f, duty: 0.1f))));

        // Game over
        Save(Sfx.GameOver, Mix((0f, Arp(new[] { C5, A4, F4 }, 0.17f, Wave.Triangle, 0.6f)),
                               (0.51f, Tone(0.8f, C4, G3, Wave.Square, 0.4f, decay: 0.5f, vibratoHz: 5f, vibratoDepth: 0.03f))));
        Save(Sfx.LetterThud, Mix((0f, Tone(0.14f, 130f, 55f, Wave.Triangle, 1f, decay: 0.06f)),
                                 (0f, Tone(0.08f, 3000f, 800f, Wave.Noise, 0.35f, decay: 0.03f))));
        Save(Sfx.CountTick, Tone(0.025f, 1500f, 1500f, Wave.Square, 0.4f, decay: 0.012f));
        Save(Sfx.NewBest, Mix((0f, Arp(new[] { G5, B5, D6, G6, B6, D7 }, 0.045f, Wave.Square, 0.45f, 0.25f)),
                              (0.27f, Tone(0.4f, D7, D7, Wave.Triangle, 0.4f, decay: 0.25f, vibratoHz: 8f, vibratoDepth: 0.03f))));
        Save(Sfx.Restart, Mix((0f, Tone(0.3f, 200f, 1200f, Wave.Square, 0.5f, decay: 0.2f)),
                              (0f, Tone(0.3f, 100f, 600f, Wave.Triangle, 0.4f, decay: 0.2f))));

        AssetDatabase.Refresh();
        Debug.Log($"Sound effects generated in {Folder}.");
    }

    // ---------------------------------------------------------------- synthesis

    /// <summary>
    /// One voice: an exponential pitch sweep from startHz to endHz, a short attack, an optional exponential decay
    /// (time constant in seconds) and optional vibrato. For noise, the frequency is how often a new random value is picked.
    /// </summary>
    private static float[] Tone(float duration, float startHz, float endHz, Wave wave, float volume,
        float duty = 0.5f, float attack = 0.003f, float decay = 0f, float vibratoHz = 0f, float vibratoDepth = 0f)
    {
        int count = Mathf.CeilToInt(duration * SampleRate);
        var samples = new float[count];
        float phase = 0f;
        float noiseValue = 0f;
        uint seed = 22222u;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float k = t / duration;
            float frequency = startHz * Mathf.Pow(endHz / startHz, k);
            if (vibratoHz > 0f) frequency *= 1f + vibratoDepth * Mathf.Sin(2f * Mathf.PI * vibratoHz * t);

            float previous = phase;
            phase += frequency / SampleRate;
            phase -= Mathf.Floor(phase);

            float value;
            switch (wave)
            {
                case Wave.Square: value = phase < duty ? 1f : -1f; break;
                case Wave.Triangle: value = 4f * Mathf.Abs(phase - 0.5f) - 1f; break;
                case Wave.Saw: value = 2f * phase - 1f; break;
                default:
                    if (phase < previous || i == 0) // new random value each cycle (sample-and-hold noise)
                    {
                        seed = seed * 1664525u + 1013904223u;
                        noiseValue = (seed >> 8) / (float)(1 << 24) * 2f - 1f;
                    }
                    value = noiseValue;
                    break;
            }

            float envelope = Mathf.Min(1f, t / attack);
            if (decay > 0f) envelope *= Mathf.Exp(-t / decay);
            envelope *= Mathf.Min(1f, (duration - t) / 0.005f); // tiny fade-out, no click
            samples[i] = value * volume * envelope;
        }
        return samples;
    }

    /// <summary>Plays the notes one after the other, each a short plucked tone.</summary>
    private static float[] Arp(float[] notes, float noteLength, Wave wave, float volume, float duty = 0.5f)
    {
        var parts = new (float, float[])[notes.Length];
        for (int i = 0; i < notes.Length; i++)
            parts[i] = (i * noteLength, Tone(noteLength * 1.2f, notes[i], notes[i], wave, volume, duty, decay: noteLength * 0.8f));
        return Mix(parts);
    }

    /// <summary>Adds parts together, each starting at its own time in seconds.</summary>
    private static float[] Mix(params (float start, float[] samples)[] parts)
    {
        int length = 0;
        foreach (var part in parts)
            length = Mathf.Max(length, Mathf.RoundToInt(part.start * SampleRate) + part.samples.Length);

        var result = new float[length];
        foreach (var part in parts)
        {
            int offset = Mathf.RoundToInt(part.start * SampleRate);
            for (int i = 0; i < part.samples.Length; i++) result[offset + i] += part.samples[i];
        }
        return result;
    }

    // ---------------------------------------------------------------- output

    private static void Save(Sfx id, float[] samples)
    {
        // Normalize, then crush: hold each value for 4 samples (~11 kHz) and keep 64 volume steps, for an 8-bit feel.
        float peak = 0.0001f;
        foreach (float s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
        float gain = 0.9f / peak;
        float held = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            if (i % 4 == 0) held = Mathf.Round(samples[i] * gain * 32f) / 32f;
            samples[i] = held;
        }

        string path = $"{Folder}/{id}.wav";
        using var stream = new FileStream(path, FileMode.Create);
        using var writer = new BinaryWriter(stream);
        int dataBytes = samples.Length * 2;
        writer.Write("RIFF".ToCharArray());
        writer.Write(36 + dataBytes);
        writer.Write("WAVE".ToCharArray());
        writer.Write("fmt ".ToCharArray());
        writer.Write(16);              // format chunk size
        writer.Write((short)1);        // PCM
        writer.Write((short)1);        // mono
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);  // bytes per second
        writer.Write((short)2);        // bytes per sample
        writer.Write((short)16);       // bits per sample
        writer.Write("data".ToCharArray());
        writer.Write(dataBytes);
        foreach (float s in samples) writer.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f));
    }
}
