using System;
using MechMaster.Domain;
using UnityEngine;

namespace MechMaster.Runtime
{
    public sealed class FeedbackAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private AudioSource source;
        private AudioClip pickupClip;
        private AudioClip handToolClip;
        private AudioClip hexToolClip;
        private AudioClip torxToolClip;
        private AudioClip disassembleClip;
        private AudioClip assembleClip;
        private AudioClip blockedClip;
        private AudioClip modeSwitchClip;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 0.48f;
            source.ignoreListenerPause = true;

            pickupClip = CreateMetalPickup();
            handToolClip = CreateToolClick("Mechanical_Hand", 1750f, 1);
            hexToolClip = CreateToolClick("Mechanical_HexKey", 2200f, 2);
            torxToolClip = CreateToolClick("Mechanical_TorxKey", 2650f, 3);
            disassembleClip = CreateDisassemblySound();
            assembleClip = CreateAssemblySound();
            blockedClip = CreateBlockedSound();
            modeSwitchClip = CreateModeSwitchSound();
        }

        public void PlayPickup()
        {
            Play(pickupClip, 0.72f);
        }

        public void PlayToolSelected(ToolKind tool)
        {
            switch (tool)
            {
                case ToolKind.HexKey:
                    Play(hexToolClip, 0.82f);
                    break;
                case ToolKind.TorxKey:
                    Play(torxToolClip, 0.82f);
                    break;
                default:
                    Play(handToolClip, 0.72f);
                    break;
            }
        }

        public void PlayDisassembled()
        {
            Play(disassembleClip, 1f);
        }

        public void PlayAssembled()
        {
            Play(assembleClip, 1f);
        }

        public void PlayBlocked()
        {
            Play(blockedClip, 0.9f);
        }

        public void PlayModeSwitch()
        {
            Play(modeSwitchClip, 0.78f);
        }

        private void Play(AudioClip clip, float volumeScale)
        {
            if (source != null && clip != null)
            {
                source.PlayOneShot(clip, volumeScale);
            }
        }

        private static AudioClip CreateMetalPickup()
        {
            uint noiseState = 0xA341316Cu;
            float previousNoise = 0f;
            return CreateClip("Mechanical_Pickup", 0.085f, (time, index) =>
            {
                float noise = NextNoise(ref noiseState);
                float brightNoise = noise - previousNoise * 0.82f;
                previousNoise = noise;
                float impact = Mathf.Exp(-time * 54f);
                float ring = MetalRing(time, 2050f, 3600f, 5150f, 38f);
                return impact * (ring * 0.72f + brightNoise * 0.22f);
            });
        }

        private static AudioClip CreateToolClick(string name, float baseFrequency, int clicks)
        {
            uint noiseState = 0x91E10DA5u + (uint)clicks * 7919u;
            return CreateClip(name, 0.055f + clicks * 0.032f, (time, index) =>
            {
                float sample = 0f;
                for (int click = 0; click < clicks; click++)
                {
                    float local = time - click * 0.032f;
                    if (local < 0f)
                    {
                        continue;
                    }

                    float envelope = Mathf.Exp(-local * 72f);
                    float ring = MetalRing(
                        local,
                        baseFrequency,
                        baseFrequency * 1.61f,
                        baseFrequency * 2.17f,
                        55f);
                    sample += envelope * ring * 0.72f;
                }

                return sample + NextNoise(ref noiseState) * Mathf.Exp(-time * 80f) * 0.1f;
            });
        }

        private static AudioClip CreateDisassemblySound()
        {
            uint noiseState = 0xC8013EA4u;
            float smoothedNoise = 0f;
            return CreateClip("Mechanical_Disassemble", 0.34f, (time, index) =>
            {
                float sample = 0f;
                for (int click = 0; click < 5; click++)
                {
                    float local = time - click * 0.028f;
                    if (local >= 0f)
                    {
                        sample += MetalRing(local, 1850f, 3050f, 4620f, 78f)
                            * Mathf.Exp(-local * 84f) * 0.34f;
                    }
                }

                float rawNoise = NextNoise(ref noiseState);
                smoothedNoise = Mathf.Lerp(smoothedNoise, rawNoise, 0.18f);
                if (time > 0.055f && time < 0.19f)
                {
                    float phase = (time - 0.055f) / 0.135f;
                    sample += smoothedNoise * Mathf.Sin(Mathf.PI * phase) * 0.22f;
                }

                float release = time - 0.205f;
                if (release >= 0f)
                {
                    sample += MetalRing(release, 980f, 2360f, 4210f, 19f)
                        * Mathf.Exp(-release * 20f) * 0.75f;
                }

                return sample;
            });
        }

        private static AudioClip CreateAssemblySound()
        {
            uint noiseState = 0xAD90777Du;
            float smoothedNoise = 0f;
            return CreateClip("Mechanical_Assemble", 0.3f, (time, index) =>
            {
                float sample = 0f;
                float rawNoise = NextNoise(ref noiseState);
                smoothedNoise = Mathf.Lerp(smoothedNoise, rawNoise, 0.12f);
                if (time < 0.145f)
                {
                    float slideEnvelope = Mathf.Sin(Mathf.PI * time / 0.145f);
                    sample += smoothedNoise * slideEnvelope * 0.18f;
                }

                float seat = time - 0.12f;
                if (seat >= 0f)
                {
                    sample += Mathf.Sin(2f * Mathf.PI * 210f * seat)
                        * Mathf.Exp(-seat * 38f) * 0.52f;
                }

                sample += LockClick(time - 0.155f, 2450f, 0.58f);
                sample += LockClick(time - 0.205f, 3150f, 0.7f);
                return sample;
            });
        }

        private static AudioClip CreateBlockedSound()
        {
            uint noiseState = 0x7E95761Eu;
            float smoothedNoise = 0f;
            return CreateClip("Mechanical_Blocked", 0.18f, (time, index) =>
            {
                float rawNoise = NextNoise(ref noiseState);
                smoothedNoise = Mathf.Lerp(smoothedNoise, rawNoise, 0.055f);
                float envelope = Mathf.Exp(-time * 25f);
                float body = Mathf.Sin(2f * Mathf.PI * 118f * time) * 0.58f
                    + Mathf.Sin(2f * Mathf.PI * 207f * time) * 0.24f;
                return (body + smoothedNoise * 0.28f) * envelope;
            });
        }

        private static AudioClip CreateModeSwitchSound()
        {
            return CreateClip("Mechanical_ModeSwitch", 0.16f, (time, index) =>
            {
                float first = LockClick(time, 1700f, 0.55f);
                float second = LockClick(time - 0.075f, 2350f, 0.72f);
                return first + second;
            });
        }

        private static float LockClick(float time, float frequency, float strength)
        {
            if (time < 0f)
            {
                return 0f;
            }

            return MetalRing(time, frequency, frequency * 1.48f, frequency * 2.06f, 65f)
                * Mathf.Exp(-time * 72f) * strength;
        }

        private static float MetalRing(
            float time,
            float first,
            float second,
            float third,
            float damping)
        {
            float decay = Mathf.Exp(-time * damping);
            return (
                Mathf.Sin(2f * Mathf.PI * first * time) * 0.62f
                + Mathf.Sin(2f * Mathf.PI * second * time) * 0.27f
                + Mathf.Sin(2f * Mathf.PI * third * time) * 0.11f)
                * decay;
        }

        private static float NextNoise(ref uint state)
        {
            state = state * 1664525u + 1013904223u;
            return ((state >> 8) / 8388607.5f) - 1f;
        }

        private static AudioClip CreateClip(
            string clipName,
            float duration,
            Func<float, int, float> generator)
        {
            int sampleCount = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];
            float peak = 0f;
            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)SampleRate;
                float sample = generator(time, index);
                samples[index] = sample;
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            }

            float gain = peak > 0.0001f ? 0.86f / peak : 1f;
            int fadeSamples = Mathf.Min(sampleCount, Mathf.RoundToInt(SampleRate * 0.012f));
            for (int index = 0; index < sampleCount; index++)
            {
                float fade = index >= sampleCount - fadeSamples
                    ? (sampleCount - 1 - index) / (float)Mathf.Max(1, fadeSamples - 1)
                    : 1f;
                samples[index] = Mathf.Clamp(samples[index] * gain * fade, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(
                clipName,
                sampleCount,
                1,
                SampleRate,
                false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
