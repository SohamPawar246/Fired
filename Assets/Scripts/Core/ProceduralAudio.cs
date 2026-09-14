using UnityEngine;

/// <summary>
/// Generates placeholder audio ENTIRELY in code — no .wav/.ogg assets needed.
/// This is why the menus have click/hover sounds and ambient music on day one.
///
/// When real audio lands mid-jam: assign real AudioClips in the AudioManager
/// inspector fields and these generators simply stop being used. Delete this
/// file only after every AudioManager slot has a real clip.
/// </summary>
public static class ProceduralAudio
{
    private const int SampleRate = 44100;

    /// <summary>
    /// Short sine "blip" sweeping from startFreq to endFreq with a fast attack
    /// and exponential decay. Good for UI clicks/hovers/confirms.
    /// </summary>
    public static AudioClip Blip(string name, float startFreq, float endFreq, float duration, float volume = 0.5f)
    {
        int samples = Mathf.CeilToInt(duration * SampleRate);
        var data = new float[samples];
        float phase = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples; // 0..1 through the blip
            float freq = Mathf.Lerp(startFreq, endFreq, t);
            phase += 2f * Mathf.PI * freq / SampleRate;

            float attack = Mathf.Clamp01(i / (0.005f * SampleRate)); // 5ms attack, no click
            float decay = Mathf.Exp(-5f * t);
            data[i] = Mathf.Sin(phase) * attack * decay * volume;
        }

        var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// Soft looping ambient pad: a stack of gently detuned sine waves with a slow
    /// volume swell. Frequencies are snapped so every wave completes whole cycles
    /// over the loop length — that is what makes the loop seamless.
    /// </summary>
    public static AudioClip AmbientPad(string name, float[] chordFreqs, float loopSeconds = 8f, float volume = 0.16f)
    {
        int samples = Mathf.CeilToInt(loopSeconds * SampleRate);
        var data = new float[samples];
        float snap = 1f / loopSeconds; // frequencies must be multiples of this to loop cleanly

        foreach (float rawFreq in chordFreqs)
        {
            // Two slightly detuned copies of each note = warm chorus effect.
            float f1 = Mathf.Round(rawFreq / snap) * snap;
            float f2 = Mathf.Round((rawFreq + 1.1f) / snap) * snap;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                // Slow swell (one full cycle per loop, so it also loops cleanly).
                float swell = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * t / loopSeconds);
                float s = Mathf.Sin(2f * Mathf.PI * f1 * t) + Mathf.Sin(2f * Mathf.PI * f2 * t);
                data[i] += s * swell * (volume / chordFreqs.Length);
            }
        }

        var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// Gunshot: a white-noise crack with fast decay layered over a low sine thump
    /// that pitch-drops. Used by the menu PLAY flourish (and free to reuse in-game).
    /// </summary>
    public static AudioClip GunBang(string name, float duration = 0.5f, float volume = 0.65f)
    {
        int samples = Mathf.CeilToInt(duration * SampleRate);
        var data = new float[samples];
        var rng = new System.Random(1337);
        float last = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;
            float k = i / (float)samples;

            // Crack: noise, lightly low-passed (one-pole) so it isn't pure hiss.
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            last += (noise - last) * 0.35f;
            float crack = last * Mathf.Exp(-18f * t);

            // Body: sub thump, 110 Hz dropping to 45 Hz.
            float freq = Mathf.Lerp(110f, 45f, Mathf.Clamp01(k * 3f));
            float thump = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-9f * t) * 0.8f;

            float attack = Mathf.Clamp01(i / (0.002f * SampleRate));
            data[i] = (crack + thump) * attack * volume;
        }

        var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// Driving synth loop: four-on-the-floor kick, offbeat hats, a 16th-note
    /// saw bassline and a lead stab per bar. Loops seamlessly (whole bars, and
    /// every voice decays inside its own step). This is the "thrilling" track —
    /// the ambient pads read as menu wallpaper, this reads as a chase.
    /// </summary>
    public static AudioClip DrivingLoop(string name, float bpm, float[] bassNotes, float[] stabNotes, float volume = 0.30f)
    {
        float beat = 60f / bpm;
        int bars = 8;
        int samples = Mathf.CeilToInt(bars * 4 * beat * SampleRate);
        var data = new float[samples];
        var rng = new System.Random(4242);

        float step = beat / 4f;                       // 16th note
        int totalSteps = bars * 16;

        for (int s = 0; s < totalSteps; s++)
        {
            int start = Mathf.FloorToInt(s * step * SampleRate);
            int beatInBar = (s / 4) % 4;
            bool isBeat = s % 4 == 0;
            bool isOffbeat = s % 4 == 2;

            // Kick on every beat.
            if (isBeat)
            {
                int len = Mathf.Min(Mathf.FloorToInt(0.16f * SampleRate), samples - start);
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)SampleRate;
                    float f = Mathf.Lerp(150f, 48f, Mathf.Clamp01(t * 12f));
                    data[start + i] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-16f * t) * 0.9f;
                }
            }

            // Offbeat hat: tiny noise tick.
            if (isOffbeat)
            {
                int len = Mathf.Min(Mathf.FloorToInt(0.04f * SampleRate), samples - start);
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)SampleRate;
                    data[start + i] += (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-90f * t) * 0.16f;
                }
            }

            // 16th-note bass: pick note per bar, gate every 16th, skip beat 4's
            // last step for a breath. Soft-saw via first 4 harmonics.
            float bass = bassNotes[(s / 16) % bassNotes.Length];
            if (s % 16 != 15)
            {
                int len = Mathf.Min(Mathf.FloorToInt(step * 0.85f * SampleRate), samples - start);
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)SampleRate;
                    float saw = 0f;
                    for (int h = 1; h <= 4; h++)
                        saw += Mathf.Sin(2f * Mathf.PI * bass * h * t) / h;
                    float attack = Mathf.Clamp01(i / (0.003f * SampleRate));
                    float decay = Mathf.Exp(-7f * t);
                    data[start + i] += saw * attack * decay * 0.30f;
                }
            }

            // Lead stab: one bright chord hit on beat 2 of each bar.
            if (beatInBar == 1 && isBeat)
            {
                float lead = stabNotes[(s / 16) % stabNotes.Length];
                int len = Mathf.Min(Mathf.FloorToInt(0.5f * SampleRate), samples - start);
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)SampleRate;
                    float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * lead * t)) * 0.35f
                             + Mathf.Sin(2f * Mathf.PI * lead * 1.5f * t) * 0.65f; // add a fifth
                    float attack = Mathf.Clamp01(i / (0.004f * SampleRate));
                    data[start + i] += sq * attack * Mathf.Exp(-5f * t) * 0.14f;
                }
            }
        }

        // Normalize with headroom and apply master volume.
        float peak = 0.0001f;
        for (int i = 0; i < samples; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        float g = volume / peak;
        for (int i = 0; i < samples; i++) data[i] *= g;

        var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// AmbientPad plus a sprinkle of soft bell "plucks" (sine pings with a long
    /// decay) placed through the loop — makes the menu music feel alive instead
    /// of a static drone. pluckFreqs is cycled through at even intervals.
    /// </summary>
    public static AudioClip AmbientPadWithSparkle(string name, float[] chordFreqs, float[] pluckFreqs,
        float loopSeconds = 8f, float padVolume = 0.16f, float pluckVolume = 0.05f)
    {
        var clip = AmbientPad(name, chordFreqs, loopSeconds, padVolume);
        int samples = clip.samples;
        var data = new float[samples];
        clip.GetData(data, 0);

        int pluckCount = pluckFreqs.Length;
        for (int p = 0; p < pluckCount; p++)
        {
            // Spread plucks through the loop, leaving room for the last one's tail.
            float startTime = (p + 0.5f) * (loopSeconds - 1.2f) / pluckCount;
            int start = Mathf.FloorToInt(startTime * SampleRate);
            float freq = pluckFreqs[p];

            int pluckSamples = Mathf.Min(Mathf.FloorToInt(1.1f * SampleRate), samples - start);
            for (int i = 0; i < pluckSamples; i++)
            {
                float t = i / (float)SampleRate;
                float attack = Mathf.Clamp01(i / (0.004f * SampleRate));
                float decay = Mathf.Exp(-4.5f * t);
                // Fundamental + quiet octave = a slightly bell-like timbre.
                float s = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t);
                data[start + i] += s * attack * decay * pluckVolume;
            }
        }

        clip.SetData(data, 0);
        return clip;
    }
}
