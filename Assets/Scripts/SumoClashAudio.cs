using System.Collections;
using UnityEngine;

/// <summary>
/// Small 2D sound bank for match feedback. Clips are stored under Resources/Audio.
/// </summary>
public sealed class SumoClashAudio : MonoBehaviour
{
    public static SumoClashAudio Instance { get; private set; }

    private AudioSource source;
    private AudioClip countdownClip;
    private AudioClip startClip;
    private AudioClip hitClip;
    private AudioClip gameOverClip;
    private AudioClip victoryClip;
    private AudioClip dashClip;
    private AudioClip menuClip;
    private float lastHitSoundTime = -1f;

    private void Awake()
    {
        Instance = this;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 1f;

        countdownClip = Resources.Load<AudioClip>("Audio/countdown");
        startClip = Resources.Load<AudioClip>("Audio/clash_start");
        hitClip = Resources.Load<AudioClip>("Audio/sumo_hit");
        gameOverClip = Resources.Load<AudioClip>("Audio/game_over");
        victoryClip = Resources.Load<AudioClip>("Audio/victory");
        dashClip = Resources.Load<AudioClip>("Audio/dash");
        menuClip = Resources.Load<AudioClip>("Audio/menu_click");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void PlayCountdown()
    {
        Play(countdownClip, 0.72f);
    }

    public void PlayStart()
    {
        Play(startClip, 0.88f);
    }

    public void PlayHit()
    {
        if (Time.unscaledTime - lastHitSoundTime < 0.07f)
        {
            return;
        }

        lastHitSoundTime = Time.unscaledTime;
        Play(hitClip, Random.Range(0.75f, 1f), Random.Range(0.92f, 1.08f));
    }

    public void PlayDash()
    {
        Play(dashClip, 0.58f);
    }

    public void PlayMenu()
    {
        Play(menuClip, 0.55f);
    }

    public void PlayResult(bool hasWinner)
    {
        Play(gameOverClip, 0.82f);
        if (hasWinner)
        {
            StartCoroutine(PlayVictoryAfterBuzzer());
        }
    }

    private IEnumerator PlayVictoryAfterBuzzer()
    {
        yield return new WaitForSecondsRealtime(0.36f);
        Play(victoryClip, 0.75f);
    }

    private void Play(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null || source == null)
        {
            return;
        }

        source.pitch = pitch;
        source.PlayOneShot(clip, volume);
    }
}
