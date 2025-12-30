using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MapSettingUI : MonoBehaviour
{
    public MapSettingsSO mapSettings;
    
    private WFCGeneratorForCube w;

    private bool isKey1On = false;
    private bool isKey2On = true;

    [Header("UI Components")]
    [Space(10)]
    public TMP_InputField inputSizeX;
    public TMP_InputField inputSizeY;
    public TMP_InputField inputSizeZ;

    [Space(5)]
    public TMP_InputField inputEntryX;
    public TMP_InputField inputEntryY;
    public TMP_InputField inputEntryZ;

    [Space(5)]
    public TMP_InputField inputExitX;
    public TMP_InputField inputExitY;
    public TMP_InputField inputExitZ;

    [Space(5)]
    public Slider sliderPathNoise;
    public TextMeshProUGUI textPathNoiseVal; // 슬라이더 옆에 수치 표시용

    [Space(5)]
    public Slider sliderWinding;
    public TextMeshProUGUI textWindingVal;

    [Space(10)]
    public Button btnApply; // 설정을 확정하는 버튼

    private void Start()
    {
        // UI 초기값 세팅 (현재 데이터 불러오기)
        LoadCurrentSettings();

        // 슬라이더 값 변경 시 텍스트 업데이트 실시간 반영
        sliderPathNoise.onValueChanged.AddListener(UpdateNoiseText);
        sliderWinding.onValueChanged.AddListener(UpdateWindingText);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q)) // potential map and root viewer
            OnButtonQ();

        if (Input.GetKeyDown(KeyCode.E)) // skin viewer
            OnButtonE();

        if (Input.GetKeyDown(KeyCode.R)) // regeneration
            OnButtonR();
    }

    public void OnButtonQ()
    {
        if (isKey1On)
        {
            isKey1On = false;
            WFCGeneratorForCube.wfcg.SetPotentialVisualizerOn();
        }
        else
        {
            isKey1On = true;
            WFCGeneratorForCube.wfcg.SetPotentialVisualizerOff();
        }
    }

    public void OnButtonE()
    {
        if (isKey2On)
        {
            isKey2On = false;
            WFCGeneratorForCube.wfcg.cubeMapParent.SetActive(false);
        }
        else
        {
            isKey2On = true;
            WFCGeneratorForCube.wfcg.cubeMapParent.SetActive(true);
        }
    }

    public void OnButtonR()
    {
        ApplySettings();
        SceneManager.LoadScene("_MapGenerator_Simulation");
    }

    private void LoadCurrentSettings()
    {
        if (WFCGeneratorForCube.wfcg == null) return;
        w = WFCGeneratorForCube.wfcg;

        // Vector3Int - Size
        inputSizeX.text = mapSettings.cubeMapSize.x.ToString();
        inputSizeY.text = mapSettings.cubeMapSize.y.ToString();
        inputSizeZ.text = mapSettings.cubeMapSize.z.ToString();

        // Vector3Int - Entry
        inputEntryX.text = mapSettings.entryCell.x.ToString();
        inputEntryY.text = mapSettings.entryCell.y.ToString();
        inputEntryZ.text = mapSettings.entryCell.z.ToString();

        // Vector3Int - Exit
        inputExitX.text = mapSettings.exitCell.x.ToString();
        inputExitY.text = mapSettings.exitCell.y.ToString();
        inputExitZ.text = mapSettings.exitCell.z.ToString();

        // Sliders
        sliderPathNoise.maxValue = 30f;
        sliderPathNoise.value = mapSettings.pathNoise;
        UpdateNoiseText(mapSettings.pathNoise);

        sliderWinding.maxValue = 1f;
        sliderWinding.value = mapSettings.windingTolerence;
        UpdateWindingText(mapSettings.windingTolerence);
    }

    private void UpdateNoiseText(float value)
    {
        textPathNoiseVal.text = value.ToString("F2"); // 소수점 2자리
    }

    private void UpdateWindingText(float value)
    {
        textWindingVal.text = value.ToString("F2");
    }

    // "적용" 버튼을 눌렀을 때 실제 데이터에 값 전달
    public void ApplySettings()
    {
        if (w == null) return;

        // Parse Inputs (예외 처리 필요 시 TryParse 사용 권장)
        mapSettings.cubeMapSize = new Vector3Int(
            int.Parse(inputSizeX.text),
            int.Parse(inputSizeY.text),
            int.Parse(inputSizeZ.text));

        mapSettings.entryCell = new Vector3Int(
            Mathf.Clamp(int.Parse(inputEntryX.text), 1, mapSettings.cubeMapSize.x - 1),
            Mathf.Clamp(int.Parse(inputEntryY.text), 0, mapSettings.cubeMapSize.y),
            Mathf.Clamp(int.Parse(inputEntryZ.text), 1, mapSettings.cubeMapSize.z - 1));

        mapSettings.exitCell = new Vector3Int(
            Mathf.Clamp(int.Parse(inputExitX.text), 1, mapSettings.cubeMapSize.x - 2),
            Mathf.Clamp(int.Parse(inputExitY.text), 0, mapSettings.cubeMapSize.y - 1),
            Mathf.Clamp(int.Parse(inputExitZ.text), 1, mapSettings.cubeMapSize.z - 2));

        mapSettings.pathNoise = sliderPathNoise.value;
        mapSettings.windingTolerence = sliderWinding.value;

        Debug.Log("Map Settings Saved!");
    }
}