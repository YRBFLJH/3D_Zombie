using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("音频源池")]
    public AudioSource musicSource;
    public AudioSource[] sfxSources;

    private int nextSfxIndex;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (sfxSources == null || sfxSources.Length == 0)
        {
            // 自动创建8个SFX AudioSource
            sfxSources = new AudioSource[8];
            for (int i = 0; i < 8; i++)
            {
                GameObject obj = new GameObject("SFX_" + i);
                obj.transform.SetParent(transform);
                sfxSources[i] = obj.AddComponent<AudioSource>();
                sfxSources[i].playOnAwake = false;
                sfxSources[i].spatialBlend = 1f;
                sfxSources[i].maxDistance = 50f;
            }
        }
    }

    public void PlaySFX(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null || sfxSources == null) return;

        AudioSource source = sfxSources[nextSfxIndex];
        nextSfxIndex = (nextSfxIndex + 1) % sfxSources.Length;

        source.transform.position = position;
        source.volume = volume;
        source.PlayOneShot(clip);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (musicSource == null || clip == null) return;
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
    }
}
