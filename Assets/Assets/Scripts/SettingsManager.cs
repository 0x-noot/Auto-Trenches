using UnityEngine;
using UnityEngine.SceneManagement;
public class SettingsManager : MonoBehaviour
{
    [System.Serializable]
    public class GameSettings
    {
        public float masterVolume = 1f;
        public float musicVolume = 1f;
        public float sfxVolume = 1f;
    }

    private static SettingsManager instance;
    public static SettingsManager Instance
    {
        get { return instance; }
    }

    private GameSettings settings;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadSettings()
    {
        string settingsJson = PlayerPrefs.GetString("GameSettings", "");
        if (string.IsNullOrEmpty(settingsJson))
        {
            settings = new GameSettings();
        }
        else
        {
            settings = JsonUtility.FromJson<GameSettings>(settingsJson);
        }
    }

    public void SaveSettings()
    {
        string settingsJson = JsonUtility.ToJson(settings);
        PlayerPrefs.SetString("GameSettings", settingsJson);
        PlayerPrefs.Save();
    }

    public void UpdateVolume(float masterVolume, float musicVolume, float sfxVolume)
    {
        settings.masterVolume = masterVolume;
        settings.musicVolume = musicVolume;
        settings.sfxVolume = sfxVolume;
        SaveSettings();
    }
}