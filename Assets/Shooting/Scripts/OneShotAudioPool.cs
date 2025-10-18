using UnityEngine;

/// Pooled one-shot 3D SFX player with inspector-level controls (no AudioMixer needed).
/// Put ONE in your first scene (it persists). Call OneShotAudioPool.PlayClipAt(pos, clip, vol, pitch).
public class OneShotAudioPool : MonoBehaviour
{
    public static OneShotAudioPool I { get; private set; }

    [Header("Pool")]
    [Min(1)] public int poolSize = 32;

    [Header("3D Settings")]
    [Range(0f, 1f)] public float spatialBlend = 1f; // 1 = fully 3D
    public float minDistance = 1f;
    public float maxDistance = 30f;
    public AudioRolloffMode rolloff = AudioRolloffMode.Linear;

    [Header("Global Gain/Pitch (applied to all voices)")]
    [Tooltip("Overall loudness. Multiplies per-call volume.")]
    [Range(0f, 2f)] public float masterVolume = 1f;

    [Tooltip("Global pitch multiplier for all voices.")]
    [Range(0.5f, 2f)] public float masterPitch = 1f;

    [Header("Tone Shaping (per-voice filters)")]
    [Tooltip("High-cut: lowers bright/treble to make bass feel more present.")]
    public bool enableHighCut = false;
    [Tooltip("Cutoff frequency in Hz (e.g., 22000 = off, 8000 = darker, 4000 = bassier).")]
    [Range(10f, 22000f)] public float highCutHz = 22000f;
    [Tooltip("Resonance (Q). 1 = flat, higher = more peak near cutoff.")]
    [Range(0.1f, 10f)] public float highCutResonanceQ = 1f;

    [Space(6)]
    [Tooltip("Low-cut: removes sub-bass/rumble if needed.")]
    public bool enableLowCut = false;
    [Tooltip("Cutoff frequency in Hz (e.g., 10–80 for rumble cleanup).")]
    [Range(10f, 1000f)] public float lowCutHz = 10f;

    [Header("Other")]
    [Tooltip("If true, survives scene loads.")]
    public bool dontDestroyOnLoad = true;

    // runtime
    AudioSource[] _sources;
    AudioLowPassFilter[] _lpf;   // for high-cut
    AudioHighPassFilter[] _hpf;  // for low-cut
    int _next = 0;

    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

        BuildPool();
        ApplySettingsToPool();
    }

    void Reset()
    {
        // sensible defaults
        poolSize = 32;
        spatialBlend = 1f;
        minDistance = 1f;
        maxDistance = 30f;
        rolloff = AudioRolloffMode.Linear;
        masterVolume = 1f;
        masterPitch = 1f;
        enableHighCut = false; highCutHz = 22000f; highCutResonanceQ = 1f;
        enableLowCut = false; lowCutHz = 10f;
    }

    void OnValidate()
    {
        // live-update in editor when values change
        if (_sources == null || _sources.Length == 0) return;
        ApplySettingsToPool();
    }

    void BuildPool()
    {
        // cleanup old
        foreach (Transform c in transform) DestroyImmediate(c.gameObject);

        _sources = new AudioSource[Mathf.Max(1, poolSize)];
        _lpf = new AudioLowPassFilter[_sources.Length];
        _hpf = new AudioHighPassFilter[_sources.Length];

        for (int i = 0; i < _sources.Length; i++)
        {
            var go = new GameObject($"OneShot_{i}");
            go.transform.SetParent(transform, false);

            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;

            // 3D base
            src.spatialBlend = spatialBlend;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.rolloffMode = rolloff;

            // filters (disabled by default, toggled in ApplySettingsToPool)
            var lpf = go.AddComponent<AudioLowPassFilter>();
            lpf.enabled = false;

            var hpf = go.AddComponent<AudioHighPassFilter>();
            hpf.enabled = false;

            _sources[i] = src;
            _lpf[i] = lpf;
            _hpf[i] = hpf;
        }
    }

    void ApplySettingsToPool()
    {
        // push inspector values to all voices
        for (int i = 0; i < _sources.Length; i++)
        {
            var s = _sources[i];
            s.spatialBlend = spatialBlend;
            s.minDistance = minDistance;
            s.maxDistance = maxDistance;
            s.rolloffMode = rolloff;

            // pitch/volume are also applied at play time; masterPitch here keeps looping future correctness if added
            s.pitch = masterPitch;

            // HIGH-CUT (LPF)
            var lpf = _lpf[i];
            if (lpf)
            {
                lpf.enabled = enableHighCut;
                lpf.cutoffFrequency = Mathf.Clamp(highCutHz, 10f, 22000f);
                lpf.lowpassResonanceQ = Mathf.Clamp(highCutResonanceQ, 0.1f, 10f);
            }

            // LOW-CUT (HPF)
            var hpf = _hpf[i];
            if (hpf)
            {
                hpf.enabled = enableLowCut;
                hpf.cutoffFrequency = Mathf.Clamp(lowCutHz, 10f, 1000f);
            }
        }
    }

    /// Play a clip once from the pool at position.
    /// volume: 0..1 typical. pitch: 0.5..2 typical. Both are multiplied by global master controls.
    public static void PlayClipAt(Vector3 position, AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null || I == null || I._sources == null || I._sources.Length == 0) return;

        var s = I._sources[I._next];
        I._next = (I._next + 1) % I._sources.Length;

        s.transform.position = position;

        // apply globals
        s.pitch = Mathf.Clamp(pitch, 0.1f, 3f) * I.masterPitch;
        s.volume = Mathf.Clamp01(volume) * Mathf.Clamp(I.masterVolume, 0f, 2f);

        s.clip = clip;
        s.Stop(); // ensure restart
        s.Play();
    }

    // --- Optional helpers ---

    /// Quickly rebuild pool if you change poolSize at runtime via inspector.
    [ContextMenu("Rebuild Pool Now")]
    void Context_Rebuild() { BuildPool(); ApplySettingsToPool(); }

    /// Apply current inspector values to existing pool (without rebuild).
    [ContextMenu("Apply Settings To Pool")]
    void Context_Apply() { ApplySettingsToPool(); }
}
