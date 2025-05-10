using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    
    [Header("Audio Clips")]
    [SerializeField] private AudioClip mainMenuMusic;
    [SerializeField] private AudioClip battleMusic;
    
    [Header("Volume Settings")]
    [SerializeField] private float musicVolume = 0.5f;
    [SerializeField] private float fadeDuration = 1.0f;
    
    private string currentScene;
    private bool isFading = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.loop = true;
                musicSource.playOnAwake = false;
                musicSource.volume = musicVolume;
            }
            
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        currentScene = SceneManager.GetActiveScene().name;
        PlayMusicForScene(currentScene);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        currentScene = scene.name;
        PlayMusicForScene(currentScene);
    }

    private void PlayMusicForScene(string sceneName)
    {
        if (sceneName.ToLower().Contains("battle"))
        {
            PlayBattleMusic();
        }
        else
        {
            PlayMainMenuMusic();
        }
    }

    public void PlayMainMenuMusic()
    {
        if (mainMenuMusic == null) return;
        
        if (musicSource.clip != mainMenuMusic)
        {
            FadeToNewClip(mainMenuMusic);
        }
        else if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    public void PlayBattleMusic()
    {
        if (battleMusic == null) return;
        
        if (musicSource.clip != battleMusic)
        {
            FadeToNewClip(battleMusic);
        }
        else if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    private void FadeToNewClip(AudioClip newClip)
    {
        if (isFading) return;
        
        StartCoroutine(FadeMusicCoroutine(newClip));
    }

    private IEnumerator FadeMusicCoroutine(AudioClip newClip)
    {
        isFading = true;
        
        float startVolume = musicSource.volume;
        if (musicSource.isPlaying)
        {
            float elapsedTime = 0f;
            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsedTime / fadeDuration);
                yield return null;
            }
        }
        
        musicSource.clip = newClip;
        musicSource.Play();
        
        float elapsedFadeInTime = 0f;
        while (elapsedFadeInTime < fadeDuration)
        {
            elapsedFadeInTime += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(0f, musicVolume, elapsedFadeInTime / fadeDuration);
            yield return null;
        }
        
        musicSource.volume = musicVolume;
        isFading = false;
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        musicSource.volume = musicVolume;
        
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        PlayerPrefs.Save();
    }

    public float GetMusicVolume()
    {
        return musicVolume;
    }
}