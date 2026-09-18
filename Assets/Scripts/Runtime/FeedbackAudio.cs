using UnityEngine;

namespace MechMaster.Runtime
{
    public sealed class FeedbackAudio : MonoBehaviour
    {
        private AudioSource source;
        private AudioClip successClip;
        private AudioClip errorClip;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = 0.18f;
            successClip = CreateTone("Success", 660f, 0.09f);
            errorClip = CreateTone("Error", 180f, 0.12f);
        }

        public void PlaySuccess()
        {
            source.PlayOneShot(successClip);
        }

        public void PlayError()
        {
            source.PlayOneShot(errorClip);
        }

        private static AudioClip CreateTone(string clipName, float frequency, float duration)
        {
            const int sampleRate = 22050;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int index = 0; index < sampleCount; index++)
            {
                float envelope = 1f - index / (float)sampleCount;
                samples[index] = Mathf.Sin(2f * Mathf.PI * frequency * index / sampleRate)
                    * envelope * 0.35f;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}

