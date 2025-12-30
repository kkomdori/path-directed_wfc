using UnityEngine;

// 프리팹 당 y 축기준 rotation 적용하여 3개의 변형 프리팹 추가 생성
// 프리팹 회전과 함께 enum value 도 같이 회전. 예) x -> z, z -> xi, xi -> zi
public enum Connection
{
    None,
    C0,
    wall,
    air,
    path,
    window,
    railing,
    C1,
    C2,
    C3,
    C4,
    Outer,
}


[System.Serializable]
public class ExcludeYRange
{
    public bool isEnable;
    public int min;
    public int max;

    public ExcludeYRange(bool isEnable, int min, int max)
    {
        this.isEnable = isEnable;
        this.min = min;
        this.max = max;
    }
}

[CreateAssetMenu(fileName = "CubeData", menuName = "WFCforCube/CubeData")]
public class CubeData : ScriptableObject
{
    public GameObject cubePrefab;

    public string cubeName = "";
    public int weight = 1;
    public int rNum = 0; // rotation number
    public float scale = 1f;
    public float yOffset = 0f;
    public bool isEnablePaint = true;

    [Header("Constrains")]
    public bool isEnable;
    public bool outerOnly;
    public bool excludeAtSide;
    public bool excludeAtTop;
    public bool excludeAtBottom;
    public ExcludeYRange excludeYRange;
    public bool notAllowDimer;

    // y축을 기준을 회전하여 
    [Header ("Connection")]
    public Connection x;
    public Connection x0;
    public Connection y;
    public Connection y0;
    public Connection z;
    public Connection z0;

    [Header("Etc")]
    public int serialNo;
}
