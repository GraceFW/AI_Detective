using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuSettingsPanelUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private bool hideOnAwake = true;

    [Header("Volume")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private TMP_Text bgmValueText;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Text sfxValueText;

    private BGMManager _bgmManager;
    private bool _initialized;

    public bool IsOpen
    {
        get
        {
            GameObject root = panelRoot != null ? panelRoot : gameObject;
            return root != null && root.activeInHierarchy;
        }
    }

    private void Awake()
    {
        EnsureInitialized();
        if (hideOnAwake && panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void OnEnable()
    {
        EnsureInitialized();
        RefreshFromAudioManagers();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        if (bgmSlider != null)
        {
            bgmSlider.onValueChanged.RemoveListener(OnBgmSliderChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);
        }
    }

    public void Open()
    {
        EnsureInitialized();
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        RefreshFromAudioManagers();
    }

    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void Close()
    {
        EnsureInitialized();
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        ConfigureSlider(bgmSlider);
        ConfigureSlider(sfxSlider);

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        if (bgmSlider != null)
        {
            bgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
        }

        _initialized = true;
    }

    private void ConfigureSlider(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.wholeNumbers = true;
    }

    private void RefreshFromAudioManagers()
    {
        _bgmManager = ResolveBgmManager();
        float bgmVolume = _bgmManager != null ? _bgmManager.GetMasterVolume01() : 1f;
        float sfxVolume = SfxManager.Instance != null ? SfxManager.Instance.GetMasterVolume01() : 1f;

        SetSliderWithoutNotify(bgmSlider, bgmVolume);
        SetSliderWithoutNotify(sfxSlider, sfxVolume);
        UpdateValueText(bgmValueText, bgmVolume);
        UpdateValueText(sfxValueText, sfxVolume);
    }

    private void SetSliderWithoutNotify(Slider slider, float normalizedValue)
    {
        if (slider == null)
        {
            return;
        }

        slider.SetValueWithoutNotify(Mathf.RoundToInt(Mathf.Clamp01(normalizedValue) * 100f));
    }

    private void OnBgmSliderChanged(float value)
    {
        float normalized = Mathf.Clamp01(value / 100f);
        UpdateValueText(bgmValueText, normalized);

        _bgmManager = ResolveBgmManager();
        if (_bgmManager != null)
        {
            _bgmManager.SetMasterVolume01(normalized);
        }
    }

    private void OnSfxSliderChanged(float value)
    {
        float normalized = Mathf.Clamp01(value / 100f);
        UpdateValueText(sfxValueText, normalized);

        if (SfxManager.Instance != null)
        {
            SfxManager.Instance.SetMasterVolume01(normalized);
        }
    }

    private void UpdateValueText(TMP_Text valueText, float normalizedValue)
    {
        if (valueText != null)
        {
            valueText.text = Mathf.RoundToInt(Mathf.Clamp01(normalizedValue) * 100f).ToString();
        }
    }

    private BGMManager ResolveBgmManager()
    {
        if (BGMManager.Instance != null)
        {
            return BGMManager.Instance;
        }

        return FindObjectOfType<BGMManager>();
    }
}
