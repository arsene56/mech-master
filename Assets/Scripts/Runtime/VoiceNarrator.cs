using UnityEngine;

namespace MechMaster.Runtime
{
    public sealed class VoiceNarrator : MonoBehaviour
    {
        public bool Enabled { get; set; } = true;

        public void Speak(string text)
        {
            if (!Enabled || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            // 首版保留统一入口；微信端接入经过授权的预录音频后替换此实现。
            Debug.Log("[机械大师讲解] " + text);
        }
    }
}

