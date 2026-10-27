using UnityEngine;

namespace Vlliards.App
{
    public sealed class GameAudio : MonoBehaviour
    {
        private AudioSource source;
        private AudioClip click, strike, pocket, foul, win, lose;
        private float volume = .7f, lastImpact = -1;

        public void Initialize()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.ignoreListenerPause = true;
            click = Tone("Menu click", .045f, 900, 700, .1f);
            strike = Tone("Cue strike", .1f, 650, 140, .65f);
            pocket = Tone("Pocket", .2f, 320, 90, .25f);
            foul = Tone("Foul", .3f, 180, 100, .1f);
            win = Chord("Win", true);
            lose = Chord("Lose", false);
        }
        public void SetVolume(float value) => volume = Mathf.Clamp01(value);
        public void Click() => Play(click, .55f);
        public void Strike() => Play(strike, .85f);
        public void Pocket() => Play(pocket, .8f);
        public void Foul() => Play(foul, .7f);
        public void Result(bool won) => Play(won ? win : lose, .65f);
        public void Impact(float speed)
        {
            if (speed < .6f || Time.unscaledTime - lastImpact < .06f) return;
            lastImpact = Time.unscaledTime;
            Play(strike, Mathf.Clamp01(speed / 12) * .5f);
        }
        private void Play(AudioClip clip, float gain) { if (source != null && clip != null && volume > 0) source.PlayOneShot(clip, volume * gain); }

        private static AudioClip Tone(string name, float seconds, float from, float to, float noise)
        {
            const int rate = 22050;
            var samples = new float[Mathf.CeilToInt(seconds * rate)];
            float phase = 0;
            uint random = 12345;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / samples.Length;
                phase += Mathf.Lerp(from, to, t) * Mathf.PI * 2 / rate;
                random = random * 1664525 + 1013904223;
                float grit = (random & 65535) / 32768f - 1;
                samples[i] = (Mathf.Sin(phase) * (1 - noise) + grit * noise) * Mathf.Pow(1 - t, 3) * Mathf.Min(1, t * 50) * .45f;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        private static AudioClip Chord(string name, bool happy)
        {
            const int rate = 22050;
            var samples = new float[rate];
            float[] notes = happy ? new[] { 523.25f, 659.25f, 783.99f } : new[] { 392f, 311.13f, 261.63f };
            for (int i = 0; i < samples.Length; i++)
            {
                float time = (float)i / rate;
                for (int note = 0; note < notes.Length; note++)
                {
                    float t = time - note * .12f;
                    if (t >= 0) samples[i] += Mathf.Sin(t * notes[note] * Mathf.PI * 2) * Mathf.Exp(-t * 5) * Mathf.Min(1, t * 70) * .16f;
                }
            }
            var clip = AudioClip.Create(name, rate, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        private void OnDestroy()
        {
            foreach (var clip in new[] { click, strike, pocket, foul, win, lose }) if (clip != null) Destroy(clip);
        }
    }
}
