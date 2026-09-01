using UnityEngine;
using BeastSoccer.Data;
using BeastSoccer.Core;

namespace BeastSoccer.Audio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Clips")]
        public AudioClip kickSfx;
        public AudioClip whistleSfx;
        public AudioClip goalSfx;
        public AudioClip saveSfx;
        public AudioClip tackleSfx;
        public AudioClip ultSfx;
        public AudioClip leoUltSfx;
        public AudioClip goroUltSfx;
        public AudioClip voltUltSfx;
        public AudioClip ultGoalSfx;
        public AudioClip crowdLoop;
        public AudioClip halfWhistle;

        private AudioSource sfx;
        private AudioSource ambient;
        private float baseAmbientVolume = 0.72f;
        private float baseAmbientPitch = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            sfx = gameObject.AddComponent<AudioSource>();
            ambient = gameObject.AddComponent<AudioSource>();
            ambient.loop = true;
            ambient.volume = baseAmbientVolume;
            ambient.pitch = baseAmbientPitch;
        }

        private void Start()
        {
            if (crowdLoop != null) { ambient.clip = crowdLoop; ambient.Play(); }
            if (GameManager.Instance != null)
                GameManager.Instance.OnPhaseChanged += HandlePhase;
        }

        private void Update()
        {
            if (ambient == null) return;
            bool finalStretch = MatchTimer.Instance != null && MatchTimer.Instance.IsFinalStretch;
            float targetVolume = finalStretch ? 1f : baseAmbientVolume;
            float targetPitch = finalStretch ? 1.08f : baseAmbientPitch;
            ambient.volume = Mathf.MoveTowards(ambient.volume, targetVolume, Time.unscaledDeltaTime * 0.8f);
            ambient.pitch = Mathf.MoveTowards(ambient.pitch, targetPitch, Time.unscaledDeltaTime * 0.18f);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnPhaseChanged -= HandlePhase;
        }

        private void HandlePhase(MatchPhase p)
        {
            switch (p)
            {
                case MatchPhase.GoalScored: Play(goalSfx); break;
                case MatchPhase.StopMade: Play(saveSfx); break;
                case MatchPhase.Kickoff: Play(whistleSfx); break;
                case MatchPhase.SetPiece: break; // GameManager triggers the set-piece whistle once, explicitly.
                case MatchPhase.HalfTime: Play(halfWhistle); break;
            }
        }

        public void PlayKick() => Play(kickSfx);
        public void PlayWhistle() => Play(whistleSfx);
        public void PlayTackle() => Play(tackleSfx);
        public void PlayUlt() => Play(ultSfx);
        public void PlayUlt(CharacterType character)
        {
            AudioClip clip = character == CharacterType.Leo ? leoUltSfx : character == CharacterType.Goro ? goroUltSfx : character == CharacterType.Volt ? voltUltSfx : null;
            Play(clip != null ? clip : ultSfx);
        }
        public void PlayUltGoal() => Play(ultGoalSfx != null ? ultGoalSfx : goalSfx);

        private void Play(AudioClip c)
        {
            if (c != null && sfx != null) sfx.PlayOneShot(c);
        }
    }
}
