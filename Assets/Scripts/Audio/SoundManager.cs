using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays the game's sound effects from a small pool of 2D audio sources.
/// Any script calls <see cref="Play"/> with an <see cref="Sfx"/> id; if no SoundManager is in the scene it does nothing.
/// Each sound has its own volume, a little random pitch variation (so repeats do not sound robotic)
/// and an optional minimum interval (so a burst of identical sounds does not pile up).
/// Sounds keep playing while the game is paused, so menus and banners are heard.
/// </summary>
public class SoundManager : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public Sfx id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.6f;
        [Tooltip("Random pitch variation, +/- this fraction.")]
        [Range(0f, 0.3f)] public float pitchJitter = 0.04f;
        [Tooltip("Seconds before the same sound can play again (0 = no limit).")]
        [Min(0f)] public float minInterval;
    }

    private static SoundManager instance;

    [SerializeField, Range(0f, 1f)] private float masterVolume = 0.8f;
    [SerializeField] private bool muted;
    [Tooltip("How many sounds can play at the same time.")]
    [SerializeField, Min(1)] private int voices = 12;
    [SerializeField] private List<Entry> sounds = new();

    private readonly Dictionary<Sfx, Entry> lookup = new();
    private readonly Dictionary<Sfx, float> lastPlayed = new();
    private AudioSource[] sources;
    private int nextSource;

    public bool Muted
    {
        get => muted;
        set => muted = value;
    }

    public float MasterVolume
    {
        get => masterVolume;
        set => masterVolume = Mathf.Clamp01(value);
    }

    private void Awake()
    {
        instance = this;

        foreach (Entry entry in sounds)
            if (entry.clip != null) lookup[entry.id] = entry;

        sources = new AudioSource[voices];
        for (int i = 0; i < voices; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D
            source.ignoreListenerPause = true;
            sources[i] = source;
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>Plays a sound. Pitch 1 = normal; higher = faster and higher.</summary>
    public static void Play(Sfx id, float pitch = 1f, float volumeScale = 1f)
    {
        if (instance != null) instance.PlayInternal(id, pitch, volumeScale);
    }

    private void PlayInternal(Sfx id, float pitch, float volumeScale)
    {
        if (muted || !lookup.TryGetValue(id, out Entry entry)) return;

        float now = Time.unscaledTime;
        if (entry.minInterval > 0f && lastPlayed.TryGetValue(id, out float last) && now - last < entry.minInterval) return;
        lastPlayed[id] = now;

        AudioSource source = FindSource();
        source.clip = entry.clip;
        source.volume = entry.volume * volumeScale * masterVolume;
        source.pitch = pitch * (1f + UnityEngine.Random.Range(-entry.pitchJitter, entry.pitchJitter));
        source.Play();
    }

    // A free source if there is one, otherwise the next one in turn (the oldest sound is cut).
    private AudioSource FindSource()
    {
        for (int i = 0; i < sources.Length; i++)
        {
            int index = (nextSource + i) % sources.Length;
            if (!sources[index].isPlaying)
            {
                nextSource = (index + 1) % sources.Length;
                return sources[index];
            }
        }

        AudioSource oldest = sources[nextSource];
        nextSource = (nextSource + 1) % sources.Length;
        return oldest;
    }
}
