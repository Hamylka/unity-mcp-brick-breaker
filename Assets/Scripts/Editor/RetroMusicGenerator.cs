using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Composes and renders the game's music loop: a 16-bar synthwave/chiptune track in A minor at 112 BPM
/// (Am - F - C - G). Bars 1-8: drums, octave bass, pads and arpeggio. Bars 9-16: the same plus a lead melody with echo.
/// Everything is rendered with wrap-around, so the end flows seamlessly into the start when looping.
/// Saved to Assets/Audio/Music/MusicLoop.wav. Tweak the patterns below and run the menu again.
/// </summary>
public static class RetroMusicGenerator
{
    private const int SampleRate = 32000;
    private const float Bpm = 112f;
    private const int Bars = 16;
    private const string Folder = "Assets/Audio/Music";
    private const string FileName = "MusicLoop.wav";

    private enum Wave { Square, Triangle, Saw, Sine }

    private static float Beat => 60f / Bpm;
    private static int TotalSamples => Mathf.RoundToInt(Bars * 4 * Beat * SampleRate);

    // Chords, two bars each: voicing (MIDI notes) and bass root.
    private static readonly int[][] ChordVoicings =
    {
        new[] { 57, 60, 64 }, // Am
        new[] { 57, 60, 65 }, // F
        new[] { 55, 60, 64 }, // C
        new[] { 55, 59, 62 }, // G
    };
    private static readonly int[] BassRoots = { 45, 41, 48, 43 }; // A2, F2, C3, G2

    // Lead melody for bars 9-16, as (length in beats, MIDI note; 0 = rest).
    private static readonly (float beats, int note)[] Melody =
    {
        (1.5f, 76), (0.5f, 74), (1f, 72), (1f, 69),   // Am
        (1f, 72), (1f, 74), (2f, 76),                 // Am
        (1.5f, 77), (0.5f, 76), (1f, 72), (1f, 69),   // F
        (1f, 72), (1f, 69), (2f, 72),                 // F
        (1.5f, 79), (0.5f, 76), (1f, 74), (1f, 72),   // C
        (1f, 74), (1f, 76), (2f, 79),                 // C
        (1f, 71), (1f, 74), (1f, 79), (1f, 74),       // G
        (1f, 71), (1f, 74), (2f, 76),                 // G -> back to Am
    };

    [MenuItem("Tools/Brick Breaker/Generate Music")]
    public static void Generate()
    {
        int length = TotalSamples;
        var drums = new float[length];
        var bed = new float[length];   // bass, pads, arp: pumped by the kick
        var lead = new float[length];
        var random = new System.Random(1984);

        for (int bar = 0; bar < Bars; bar++)
        {
            int chord = (bar / 2) % 4;
            float barStart = bar * 4 * Beat;

            // Drums: four-on-the-floor kick, snare on 2 and 4, hats on 8ths (open on the off-beats).
            for (int beat = 0; beat < 4; beat++)
            {
                float t = barStart + beat * Beat;
                Kick(drums, t, 0.9f);
                if (beat == 1 || beat == 3) Snare(drums, t, 0.45f, random);
                Hat(drums, t, 0.08f, false, random);
                Hat(drums, t + Beat * 0.5f, 0.12f, true, random);
            }
            if (bar % 4 == 3) // little fill at the end of every 4 bars
            {
                Snare(drums, barStart + 3.5f * Beat, 0.3f, random);
                Snare(drums, barStart + 3.75f * Beat, 0.35f, random);
            }

            // Bass: octave bounce on 8th notes, filtered saw.
            for (int eighth = 0; eighth < 8; eighth++)
            {
                int note = BassRoots[chord] + (eighth % 2 == 1 ? 12 : 0);
                Note(bed, barStart + eighth * Beat * 0.5f, Beat * 0.42f, Midi(note), Wave.Saw, 0.34f,
                    attack: 0.004f, release: 0.04f, cutoff: 900f);
            }

            // Pads: soft detuned triangles, one chord per two bars.
            if (bar % 2 == 0)
            {
                foreach (int note in ChordVoicings[chord])
                {
                    Note(bed, barStart, 8 * Beat - 0.1f, Midi(note), Wave.Triangle, 0.09f, attack: 0.35f, release: 0.4f);
                    Note(bed, barStart, 8 * Beat - 0.1f, Midi(note) * 1.004, Wave.Triangle, 0.07f, attack: 0.35f, release: 0.4f);
                }
            }

            // Arpeggio: 16th notes climbing the chord, an octave up, plucky square.
            int[] voicing = ChordVoicings[chord];
            int[] arp = { voicing[0] + 12, voicing[1] + 12, voicing[2] + 12, voicing[0] + 24 };
            for (int sixteenth = 0; sixteenth < 16; sixteenth++)
            {
                Note(bed, barStart + sixteenth * Beat * 0.25f, Beat * 0.2f, Midi(arp[sixteenth % 4]), Wave.Square, 0.055f,
                    attack: 0.002f, release: 0.05f, duty: 0.25f, decay: 0.09f);
            }
        }

        // Lead melody on the second half, with vibrato.
        float time = 8 * 4 * Beat;
        foreach (var (beats, note) in Melody)
        {
            if (note > 0)
                Note(lead, time, beats * Beat * 0.92f, Midi(note), Wave.Square, 0.12f,
                    attack: 0.01f, release: 0.12f, duty: 0.5f, vibratoDelay: 0.15f, cutoff: 3500f);
            time += beats * Beat;
        }

        // Echo on the lead (dotted 8th, a few repeats).
        var echoed = (float[])lead.Clone();
        int delay = Mathf.RoundToInt(Beat * 0.75f * SampleRate);
        float feedback = 0.35f;
        for (int repeat = 1; repeat <= 3; repeat++)
        {
            float gain = Mathf.Pow(feedback, repeat);
            for (int i = 0; i < length; i++) echoed[(i + delay * repeat) % length] += lead[i] * gain;
        }

        // Sidechain pump: the bed ducks after every kick and swells back.
        var mix = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)SampleRate;
            float sinceBeat = t % Beat;
            float pump = 1f - 0.4f * Mathf.Exp(-sinceBeat / 0.11f);
            mix[i] = drums[i] + bed[i] * pump + echoed[i];
        }

        // Soft clip and normalize.
        float peak = 0.0001f;
        for (int i = 0; i < length; i++)
        {
            mix[i] = (float)Math.Tanh(mix[i] * 1.3f);
            peak = Mathf.Max(peak, Mathf.Abs(mix[i]));
        }
        for (int i = 0; i < length; i++) mix[i] *= 0.85f / peak;

        Directory.CreateDirectory(Folder);
        string path = $"{Folder}/{FileName}";
        WriteWav(path, mix);
        AssetDatabase.ImportAsset(path);

        // Compressed in memory: small on phones, decoded while playing.
        if (AssetImporter.GetAtPath(path) is AudioImporter importer)
        {
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        Debug.Log($"Music loop generated: {path} ({length / (float)SampleRate:F1} s).");
    }

    // ---------------------------------------------------------------- instruments

    private static double Midi(int note) => 440.0 * Math.Pow(2.0, (note - 69) / 12.0);

    /// <summary>Adds one note to the buffer (wrapping around the loop end).</summary>
    private static void Note(float[] buffer, float start, float duration, double frequency, Wave wave, float volume,
        float attack = 0.005f, float release = 0.05f, float duty = 0.5f, float decay = 0f,
        float vibratoDelay = -1f, float cutoff = 0f)
    {
        int length = buffer.Length;
        int startIndex = Mathf.RoundToInt(start * SampleRate);
        int count = Mathf.RoundToInt((duration + release) * SampleRate);
        double phase = 0.0;
        float filtered = 0f;
        float alpha = cutoff > 0f ? 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate) : 1f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float envelope = t < attack ? t / attack : (t < duration ? 1f : Mathf.Max(0f, 1f - (t - duration) / release));
            if (decay > 0f) envelope *= Mathf.Exp(-t / decay);

            double f = frequency;
            if (vibratoDelay >= 0f && t > vibratoDelay) f *= 1.0 + 0.012 * Math.Sin(2.0 * Math.PI * 5.5 * t);
            phase += f / SampleRate;
            phase -= Math.Floor(phase);

            float value = wave switch
            {
                Wave.Square => phase < duty ? 1f : -1f,
                Wave.Triangle => 4f * Mathf.Abs((float)phase - 0.5f) - 1f,
                Wave.Saw => 2f * (float)phase - 1f,
                _ => Mathf.Sin(2f * Mathf.PI * (float)phase)
            };

            filtered += alpha * (value - filtered); // one-pole low-pass (alpha = 1 means no filter)
            buffer[(startIndex + i) % length] += filtered * volume * envelope;
        }
    }

    private static void Kick(float[] buffer, float start, float volume)
    {
        int length = buffer.Length;
        int startIndex = Mathf.RoundToInt(start * SampleRate);
        int count = Mathf.RoundToInt(0.3f * SampleRate);
        double phase = 0.0;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            double frequency = 45.0 + 105.0 * Math.Exp(-t / 0.03); // 150 Hz dropping to 45 Hz
            phase += frequency / SampleRate;
            float value = Mathf.Sin(2f * Mathf.PI * (float)(phase - Math.Floor(phase)));
            float envelope = Mathf.Exp(-t / 0.16f) * Mathf.Min(1f, t / 0.001f);
            buffer[(startIndex + i) % length] += value * volume * envelope;
        }
    }

    private static void Snare(float[] buffer, float start, float volume, System.Random random)
    {
        int length = buffer.Length;
        int startIndex = Mathf.RoundToInt(start * SampleRate);
        int count = Mathf.RoundToInt(0.25f * SampleRate);
        float low = 0f;
        double phase = 0.0;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            low += 0.2f * (noise - low);
            float crack = (noise - low) * Mathf.Exp(-t / 0.09f);           // high-passed noise
            phase += 190.0 / SampleRate;
            float body = (4f * Mathf.Abs((float)(phase - Math.Floor(phase)) - 0.5f) - 1f) * Mathf.Exp(-t / 0.05f);
            buffer[(startIndex + i) % length] += (crack * 0.8f + body * 0.5f) * volume;
        }
    }

    private static void Hat(float[] buffer, float start, float volume, bool open, System.Random random)
    {
        int length = buffer.Length;
        int startIndex = Mathf.RoundToInt(start * SampleRate);
        float decay = open ? 0.09f : 0.025f;
        int count = Mathf.RoundToInt(decay * 5f * SampleRate);
        float low = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            low += 0.5f * (noise - low);
            buffer[(startIndex + i) % length] += (noise - low) * volume * Mathf.Exp(-t / decay);
        }
    }

    // ---------------------------------------------------------------- output

    private static void WriteWav(string path, float[] samples)
    {
        using var stream = new FileStream(path, FileMode.Create);
        using var writer = new BinaryWriter(stream);
        int dataBytes = samples.Length * 2;
        writer.Write("RIFF".ToCharArray());
        writer.Write(36 + dataBytes);
        writer.Write("WAVE".ToCharArray());
        writer.Write("fmt ".ToCharArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data".ToCharArray());
        writer.Write(dataBytes);
        foreach (float s in samples) writer.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f));
    }
}
