using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("拖入三个音乐文件")]
    public AudioClip themeMusic;
    public AudioClip shootSound;
    public AudioClip reloadSound;

    private AudioSource musicSource;
    private AudioSource[] sfxSources;
    private int nextSfxIndex;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else { Destroy(gameObject); return; }

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;

        sfxSources = new AudioSource[8];
        for (int i = 0; i < 8; i++)
        {
            GameObject obj = new GameObject("SFX_" + i);
            obj.transform.SetParent(transform);
            sfxSources[i] = obj.AddComponent<AudioSource>();
            sfxSources[i].playOnAwake = false;
            sfxSources[i].spatialBlend = 1f;
            sfxSources[i].maxDistance = 150f;
            sfxSources[i].rolloffMode = AudioRolloffMode.Linear;
        }

        if (FindObjectOfType<AudioListener>() == null)
            gameObject.AddComponent<AudioListener>();
    }

    void PlaySFX(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;

        AudioSource source = sfxSources[nextSfxIndex];
        nextSfxIndex = (nextSfxIndex + 1) % sfxSources.Length;

        source.transform.position = position;
        source.volume = volume;
        source.PlayOneShot(clip);
    }

    public void PlayShootSound(Vector3 position)
    {
        PlaySFX(shootSound, position);
    }

    public void PlayReloadSound(Vector3 position)
    {
        PlaySFX(reloadSound, position);
    }

    public void PlayThemeMusic()
    {
        if (themeMusic == null || musicSource == null) return;
        if (musicSource.isPlaying && musicSource.clip == themeMusic) return;

        musicSource.clip = themeMusic;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
    }
}
