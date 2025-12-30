using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

/***** Mesh Combine 시 특이 사항 *****                   
 * MapUnit Layer 에 속한 오브젝트를 대상으로 함.
 * 부모는 MapUnit 이지만 자식은 아닐 경우 자식은 제외됨
 */


public class WFCGeneratorForCube : MonoBehaviour
{
    #region Singleton in editor
    public static WFCGeneratorForCube wfcg;

    private void Awake()
    {
        if (wfcg == null)
            wfcg = this;
        else if (wfcg != this)
        {
            Destroy(wfcg);
            Debug.LogWarning("중복된 WFCgeneratorForCubeEM 인스턴스가 감지되었습니다.");
        }
    }
    #endregion

    //public Vector3 startingPosition;
    [Header("Map Setting")]
    public MapSettingsSO mapSettings; // Scriptable Object 에서 불러옴
    public Vector3Int cubeMapSize;
    public Vector3Int entryCell;    
    public Vector3Int exitCell;
    public int mapSeed;

    [Range(0f, 30f)] public float pathNoise;
    [Range(0f, 1f)] public float windingTolerence;

    public int cubeSize = 2; // 큐브 크기
    public bool isExcludeEnable = true; // 제약조건 적용 여부
    public bool isOuterEnable = true;
    public bool setPlane = true;
    public float planeScaleFactor = 3;
    private bool setPlaneBefore = false;
    private GameObject PotentialMap_Drawer;

    [Header("Cube Exchange Setting")]
    public string FullPathOfCubeResources; // Resources folder 안에 위치시켜야 함
    public bool isCubeExchangeEnable = false;

    [Header("Bulding Materials")]
    public CubeDatabase SchematicCubeDB_All;
    public CubeDatabase schematicCubeDB;
    public CubeDatabase additionalCubeDB;
    public CubeAuxiliaryDB studNNodeDB;
    public VoidRoomDB voidRoomDB;
    public List<CubeData> CubeDataForVoidFilling; // void 영역에 배치하고 연결 속성을 부여할 가상 cube; 후보군은 최소 2개 이상 
    public CubeData[] defaultCubeData;
    private List<CubeData> defaultCubeDataCopy = new List<CubeData>();

    [Header("Schemetic Cube Apparence")]
    public bool isPainting = false;
    public MaterialDB materialDB;
    public float meshScaleOffset;
    public bool drawStudNNode = true;

    [Header("Layer To Combine")]
    public string[] layersToCombine;
    public bool combineMesh;

    [Header("etc")]
    public bool isVisibleMarker = false;
    public Cell[,,] grid; // 3차원 배열
    
    // Possible Values> 0: empty, 1: void filed without potental, 2: void filled with potential, 3: void filled with potential (cube replacible)
    public int[,,] voidGrid; 
    public List<Vector3Int> pathway; 

    public CubeData[] allCubes; // 회전값 적용한 복제품까지 포함하는 prefabs
    private Dictionary<string, CubeData> allCubesDict = new Dictionary<string, CubeData>(); // 맵 수정용 CubeData 저장, 중복 방지
    private List<KeyValuePair<string, CubeData>> allCubesList = new List<KeyValuePair<string, CubeData>>(); // 인덱스 접근 위함

    private GameObject stud;
    private GameObject node;

    private string[] tags = new string[] {"MarkerVertex", "MarkerEdge", "MarkerFace", "MarkerPath" };
    private List<GameObject> tempPrefabs = new List<GameObject>(); // 맵 완료 후 제거할 프리팹 템플릿 저장

    public GameObject cubeMapParent; // CubeMap 정리를 위한 빈 오브젝트
    private GameObject tempParent; // 임시 Cube 정리를 위한 빈 오브젝트

    // 씬에 구현된 오브젝트 
    public GameObject[,,] drawedGO;
    public List<GameObject> otherDrawedGO = new List<GameObject>(); // rooms, plane 등

    public List<GameObject> studGOs = new List<GameObject>();
    public List<GameObject> nodeGOs = new List<GameObject>();
    public List<GameObject> newCubeList = new List<GameObject>();
    private List<LightProbeGroup> lp = new List<LightProbeGroup>();
    private int CubePerlightProbe = 5; // 클수록 light probe 개수 줄어듦 

    private WFCCore wcore = new WFCCore();
    private NoisePotentialPath pf = new NoisePotentialPath();

    private string exportPath = "Assets/__MapExport/"; // prefab, grid 정보 저장용 asset path

    private void Start()
    {
        cubeMapSize = mapSettings.cubeMapSize;
        entryCell = mapSettings.entryCell;
        exitCell = mapSettings.exitCell;
        
        pathNoise = mapSettings.pathNoise;
        windingTolerence = mapSettings.windingTolerence;

        GenerateMapWithSeed();
    }

    public void GenerateMapWithSeed()
    {
        if (mapSeed == 0)
        {
            mapSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            Debug.Log("Generated random map seed: " + mapSeed);
        }
        else
        {
            Debug.Log("Using provided map seed: " + mapSeed);

            // Unity의 랜덤 상태를 받은 시드로 초기화 = Random.Range, Random.value 동일값 도출
            UnityEngine.Random.InitState(mapSeed);
        }

        // 맵 생성
        Initialize();
        Generation();

        if (isCubeExchangeEnable)
            ExchangeCubeSet();

        if (combineMesh)
            CombineMeshes();

        Destroy(tempParent);
        CreatePotentialVisualizer(); // potential field 시각화
    }

    public void Generation ()
    {
        //>>>>> 준비 과정
        MakeParentObjects(); // gameobject 정리용 부모 오브젝트 생성
        VoidRoomData[] roomCopies = MakeRoomCopies(); // voidRoom 복사

        bool isValid = false;
        int maxGenTrials = 10; // pathfinding 최대 시도 횟수
        while (!isValid)
        {
            //voidRoom 배치가 이상적이지 않을 때 pathfinding 이 실패할 수 있음. 이 경우 VoidRoomSet() 재실행
            VoidRoomSet(roomCopies); // voidRoom 복사 및 배치

            pathway = pf.PathFinder(cubeMapSize, entryCell, exitCell, pathNoise, windingTolerence); // path finding

            if (pathway == null || pathway.Count < 2) 
            { 
                if (maxGenTrials <= 0)
                {
                    Debug.LogError("Pathfinding failed: Maximum trials exceeded. Map generation aborted.");
                    return;
                }
                maxGenTrials--;

                Debug.Log("Pathfinding failed: try again");
                continue;    
            }
            else
            {
                Debug.Log("Pathfinding suceeded. path.count : " + pathway.Count);
                isValid = true;
            }
        }

        //>>>>> 배치 시작
        MakeCubeCopies(); // cell prefabs 을 방위각 별로 복사하여 저장
        InitializeGrid(); // 그리드 마다 중첩된 cell 배열을 갖도록 초기화

        // PathRecoder() 메서드 호출: 경로를 기록하여 grid 업데이트
        PathRecoder(); // 시작점과 목표지점 사이에 최소 1개 이상의 루트를 갖도록 함
        GiveConstraints(); // 각 셀에 대한 제약 조건 부여
        grid = wcore.WFC(grid, cubeMapSize, defaultCubeDataCopy[0]); // core logic 호출하여 grid 갱신
        SaveGridInfo(); // 결과 저장

        ApplyToCubeMap(); // 그리기
        SetMarkerActive(); // 마커 활성화

        Debug.Log("Map generation using default cubes is done.");
    }

    private void CreatePotentialVisualizer()
    {
        PotentialMap_Drawer = new GameObject("PotentialMap_Drawer");

        PotentialMapVisualizer visualizer = PotentialMap_Drawer.AddComponent<PotentialMapVisualizer>();

        // 생성자 대신 만들어둔 Initialize 함수로 데이터 주입
        visualizer.Initialize(cubeSize, cubeMapSize, pf.phi);
        visualizer.DrawPath(pathway);
    }


    public void SetPotentialVisualizerOn()
    {
        PotentialMap_Drawer.GetComponent<PotentialMapVisualizer>().inDrawingOn = true;
    }
    public void SetPotentialVisualizerOff()
    {
        PotentialMap_Drawer.GetComponent<PotentialMapVisualizer>().inDrawingOn = false;
    }

    public void PathRecoder()
    {
        Vector3Int prevP = Vector3Int.zero;
        Vector3Int dir = Vector3Int.zero;
        int i = 0;
        foreach (var p in pathway)
        {
            dir = (p - prevP);

            grid[p.x, p.y, p.z].pathIn = dir; // 대상 셀 갱신
            grid[p.x, p.y, p.z].forcedCollapse = true;
            //Debug.Log($"{p} | {grid[p.x, p.y, p.z].forcedCollapse}");

            if (i != 0) 
                grid[prevP.x, prevP.y, prevP.z].pathOut = dir; // 이전 셀 갱신

            prevP = p;
            i++;
        }
    }

    private void MakeParentObjects()
    {
        // export 폴더 생성
        //if (!AssetDatabase.IsValidFolder(exportPath))
        //    AssetDatabase.CreateFolder("Assets/", "__MapExport");

        if (GameObject.FindGameObjectWithTag("CubeMapParent") == null)
        {
            cubeMapParent = new GameObject("__CubeMapParent");
            cubeMapParent.tag = "CubeMapParent";
        }

        if (GameObject.FindGameObjectWithTag("Temp") == null)
        {
            tempParent = new GameObject("__TempParent");
            tempParent.tag = "Temp";
        }
    }

    public void CleanUp(bool isRegen = false) // 메모리 초기화
    {
        allCubes = null;
        drawedGO = null;
        if (!isRegen) grid = null;
        voidGrid = null;

        studGOs.Clear();
        nodeGOs.Clear();
        newCubeList.Clear();
        defaultCubeDataCopy.Clear();
        allCubesDict.Clear();
        allCubesList.Clear();
        otherDrawedGO.Clear();

        setPlaneBefore = false;

        //var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
        //foreach (GameObject obj in allObjects)
        //{
        //    // 씬에서 최상위 오브젝트 중 필수가 아닌것 제거
        //    if (obj.tag != "CtrlElement" && obj.transform.parent == null)
        //        UnityEngine.Object.DestroyImmediate(obj);
        //}

        // 프리팹 템플릿들 제거, cubeData 연결된 prefabs 지워짐; 미리 지우면 맵수정에 문제 발생
        foreach (var go in tempPrefabs)
            Destroy(go);
        tempPrefabs.Clear();

        Debug.LogWarning("CleanUP is done.");
    }

    private void MakeCubeCopies(bool isLoad = false)
    {
        if (isLoad && SchematicCubeDB_All == null)
            throw new Exception("Default Schematic CubeDB is Null. Editor execution aborted.");

        if (schematicCubeDB == null)
            throw new Exception("Schematic CubeDB is Null. Editor execution aborted.");


        // 옵션 cube data 포함시키기
        CubeData[] concatDB;
        if (additionalCubeDB == null)
            if (isLoad) concatDB = SchematicCubeDB_All.cubeData;
            else concatDB = schematicCubeDB.cubeData;
        else
            if (isLoad) concatDB = SchematicCubeDB_All.cubeData.Concat(additionalCubeDB.cubeData).ToArray();
            else concatDB = schematicCubeDB.cubeData.Concat(additionalCubeDB.cubeData).ToArray();


        CubeData[,] rotationPool;
        rotationPool = new CubeData[concatDB.Length, 4]; // 4 : xz 평면 90도씩 회전한 것 저장

        for (int i = 0; i < concatDB.Length; i++)
        {
            if (concatDB[i] == null) continue;
            
            for (int j = 0; j < 4; j++)
            {
                // 빈 SO 인스턴스 생성; SO 는 new 키워드 사용 불가
                var tempCubeData = ScriptableObject.CreateInstance<CubeData>();
                var src = concatDB[i];

                CopyScriptableObject(src, tempCubeData); // SO 복사

                // 회전으로 인한 Connection 갱신
                switch (j)
                {
                    case 0: // 0°
                        tempCubeData.x = src.x;
                        tempCubeData.x0 = src.x0;
                        tempCubeData.z = src.z;
                        tempCubeData.z0 = src.z0;
                        break;
                    case 1: // 90°
                        tempCubeData.x = src.z;
                        tempCubeData.x0 = src.z0;
                        tempCubeData.z = src.x0;
                        tempCubeData.z0 = src.x;
                        break;
                    case 2: // 180°
                        tempCubeData.x = src.x0;
                        tempCubeData.x0 = src.x;
                        tempCubeData.z = src.z0;
                        tempCubeData.z0 = src.z;
                        break;
                    case 3: // 270°
                        tempCubeData.x = src.z0;
                        tempCubeData.x0 = src.z;
                        tempCubeData.z = src.x;
                        tempCubeData.z0 = src.x0;
                        break;
                }
                tempCubeData.rNum = j;

                // Prefab 복제 붙여넣기 (SO 복제는 모든 항목 개별로 초기화해야함); 원본 프리팹 훼손 방지, 회전값 적용 무시됨
                GameObject go = Instantiate(concatDB[i].cubePrefab, Vector3Int.down * 100, Quaternion.identity);
                // GameObject go = Instantiate(src.cubePrefab, Vector3Int.down * 100, Quaternion.Euler(0, 90f * j, 0));

                tempCubeData.cubeName = concatDB[i].cubePrefab.name; // notAllowDimer 체크용, ApplyToCubeMap() 에서 이름으로 찾기 위함
                tempCubeData.cubePrefab = go;
                tempPrefabs.Add(go);
                rotationPool[i, j] = tempCubeData;

                go.transform.parent = tempParent.transform; // 부모 설정
            }
        }
        allCubes = rotationPool.Cast<CubeData>().Where(t => t != null).ToArray(); // 2차원 배열 -> 1차원 배열

        foreach(var i in allCubes)
        {
            try
            {
                PaintPrefabs(i, materialDB.wallpaper);
                allCubesDict.Add(i.cubePrefab.name + i.rNum, i);
            }
            catch
            {
                continue;
            }
        }

        allCubesList = allCubesDict.ToList();

        stud = Instantiate(studNNodeDB.stud, Vector3Int.down * 100, Quaternion.identity);
        node = Instantiate(studNNodeDB.node, Vector3Int.down * 100, Quaternion.identity);

        PaintPrefabs(stud, materialDB.studPaint);
        PaintPrefabs(node, materialDB.nodePaint);

        tempPrefabs.Add(stud);
        tempPrefabs.Add(node);

        stud.transform.parent = tempParent.transform; // 부모 설정
        node.transform.parent = tempParent.transform; // 부모 설정

        // defalutCube copy
        for (int i = 0; i < defaultCubeData.Length; i++)
        {
            if (defaultCubeData[i] == null) continue;

            // 빈 SO 인스턴스 생성; SO 는 new 키워드 사용 불가
            var tempDefaultCubeData = ScriptableObject.CreateInstance<CubeData>();
            var src = defaultCubeData[i];

            CopyScriptableObject(src, tempDefaultCubeData); // SO 복사

            GameObject d = Instantiate(defaultCubeData[i].cubePrefab, Vector3Int.down * 100, Quaternion.identity); // 복제하고

            PaintPrefabs(d, materialDB.wallpaper); // 칠하고
            tempDefaultCubeData.cubePrefab = d;
            defaultCubeDataCopy.Add(tempDefaultCubeData); // 재할당

            tempPrefabs.Add(d);
            d.transform.parent = tempParent.transform; // 부모 설정
        }
    }

    private VoidRoomData[] MakeRoomCopies()
    {
        if (voidRoomDB == null) return null;

        // voidRoom copy
        //VoidRoomData[] VoidRoomPool;

        List<VoidRoomData> VoidRoomPool = new List<VoidRoomData>();

        //VoidRoomPool = new VoidRoomData[voidRoomDB.voidRoomData.Length];

        for (int i = 0; i < voidRoomDB.voidRoomData.Length; i++)
        {
            VoidRoomData vr = voidRoomDB.voidRoomData[i];

            // 옵션에 따라 방 복제
            int copyNum = vr.roomCopyNumber;
            while (copyNum > 0)
            {
    
                RoomRotation r;
                if (vr.isRandomRotation)
                    r = (RoomRotation)UnityEngine.Random.Range(0, 4);
                else
                    r = vr.rotation;

                // 매 방마다 새로운 scriptable object 생성
                var tempVRData = ScriptableObject.CreateInstance<VoidRoomData>();
                CopyScriptableObject(vr, tempVRData); // hard Copy

                // pivot은 VoidRoom prefab의 좌측하단 cell 중간에 위치함을 전제로 함
                switch (r)
                {
                    case RoomRotation.D0:
                        tempVRData.rNum = 0;
                        break;
                    case RoomRotation.D90:
                        tempVRData.sizeOnGrid.x = vr.sizeOnGrid.z;
                        tempVRData.sizeOnGrid.z = vr.sizeOnGrid.x;
                        tempVRData.rNum = 1;
                        break;
                    case RoomRotation.D180:
                        tempVRData.rNum = 2;
                        break;
                    case RoomRotation.D270:
                        tempVRData.sizeOnGrid.x = vr.sizeOnGrid.z;
                        tempVRData.sizeOnGrid.z = vr.sizeOnGrid.x;
                        tempVRData.rNum = 3;
                        break;
                }

                GameObject voidRoomObject;
                if (voidRoomDB.voidRoomData[i].voidRoomPrefab == null)
                {
                    Debug.LogError("voidRoomDB has no connected prefab.");
                    voidRoomObject = CubeDataForVoidFilling[0].cubePrefab;
                }
                else
                    voidRoomObject = voidRoomDB.voidRoomData[i].voidRoomPrefab;

                GameObject go = Instantiate(voidRoomObject, Vector3Int.down * 100, Quaternion.identity);
                tempVRData.voidRoomPrefab = go;
                tempPrefabs.Add(go);
                VoidRoomPool.Add(tempVRData);
                //VoidRoomPool[i] = tempVRData;

                go.transform.parent = tempParent.transform;
                copyNum--;

                //Debug.Log("D1. room 생성 : " + tempVRData.voidRoomPrefab.name + " | " + tempVRData.rNum);

                if (!vr.isRandomPosition) // random 배치 옵션 켜져있으면 다음 copy 진행
                    break;
            }
        }

        foreach (var i in VoidRoomPool)
        {
            //Debug.Log("D2. room 생성 : " + i.voidRoomPrefab.name + " | " + i.rNum);
        }

        VoidRoomData[] allRooms = VoidRoomPool.Cast<VoidRoomData>().ToArray();
        return allRooms;
    }

    // voidRoomDB 의 voidRoomData 배열을 복사하여 회전값 적용 및 배치
    private void VoidRoomSet(VoidRoomData[] voidRooms)
    {
        if (voidRooms == null) return;

        voidGrid = new int[cubeMapSize.x, cubeMapSize.y, cubeMapSize.z];

        for (int i = 0; i < voidRooms.Length; i++)
        {
            VoidRoomData vr = voidRooms[i];
            Vector3Int newStartPosOnGrid;

            if (vr.attachToExit)
            {
                // 미리 입력된 exit 이 위치할 상대좌표에 exit cell 이 오도록 방 생성 위치 지정, exit cell pivot 을 중심으로 회전값을 반영
                int x = -vr.exitCellPivot.x; // -1
                int y = -vr.exitCellPivot.y;
                int z = -vr.exitCellPivot.z; // 1

                switch (vr.rNum)
                {
                    case 1: // 90
                        newStartPosOnGrid = new Vector3Int(z, y, -x) + exitCell;
                        break;
                    case 2: // 180
                        newStartPosOnGrid = new Vector3Int(-x, y, -z) + exitCell;
                        break;
                    case 3: // 270
                        newStartPosOnGrid = new Vector3Int(-z, y, x) + exitCell;
                        break;
                    default: // 0
                        newStartPosOnGrid = new Vector3Int(x, y, z) + exitCell;
                        break;
                }
            }
            else if (vr.isRandomPosition)
            {
                int yPos;
                if (vr.fixYLocation)
                    yPos = vr.startPosOnGrid.y;
                else
                    yPos = UnityEngine.Random.Range(0, cubeMapSize.y);

                newStartPosOnGrid = new Vector3Int(UnityEngine.Random.Range(0, cubeMapSize.x - vr.sizeOnGrid.x),
                    yPos,
                    UnityEngine.Random.Range(0, cubeMapSize.z - vr.sizeOnGrid.z));
            }
            else
            {
                newStartPosOnGrid = vr.startPosOnGrid;
            }

            // voidRoom 공간이 차지하는 Grid에 회전값 고려해서 속성 부여
            // Possible Values> 0: empty, 1: void filed without potental, 2: void filled with potential, 3: void filled with potential (cube replacible)
            bool isEmptyCell = true;

            int loopSizeX = vr.sizeOnGrid.x;
            int loopSizeZ = vr.sizeOnGrid.z;

            Vector3Int gridPosForVoidMarking = new Vector3Int(
                newStartPosOnGrid.x + (int)vr.gridOffset.x,
                newStartPosOnGrid.y + (int)vr.gridOffset.y,
                newStartPosOnGrid.z + (int)vr.gridOffset.z);

            switch (vr.rNum)
            {
                case 1: // 90도 회전
                    gridPosForVoidMarking += new Vector3Int(0, 0, -(vr.sizeOnGrid.z - 1));
                    break;
                case 2: // 180도 회전
                    gridPosForVoidMarking += new Vector3Int(-(vr.sizeOnGrid.x - 1), 0, -(vr.sizeOnGrid.z - 1));
                    break;
                case 3: // 270도 회전
                    gridPosForVoidMarking += new Vector3Int(-(vr.sizeOnGrid.x - 1), 0, 0);
                    break;
            }

            for (int x = gridPosForVoidMarking.x; x < (gridPosForVoidMarking.x + loopSizeX); x++)
                for (int y = gridPosForVoidMarking.y; y < (gridPosForVoidMarking.y + vr.sizeOnGrid.y); y++)
                    for (int z = gridPosForVoidMarking.z; z < (gridPosForVoidMarking.z + loopSizeZ); z++)
                    {
                        if (x >= voidGrid.GetLength(0) || y >= voidGrid.GetLength(1) || z >= voidGrid.GetLength(2)
                            || x < 0 || y < 0 || z < 0)
                            continue;

                        try
                        {
                            //이미 배치된 void 있는지 확인
                            if (voidGrid[x, y, z] != 0)
                                isEmptyCell = false;
                            else
                            {
                                // 새로 배치되는 void 영역 지정, 2: void filled with potential, 3: void filled with potential (cube replacible)
                                if (vr.pathFindingAllow)
                                    voidGrid[x, y, z] = vr.isReplacibleToPathCube ? 3 : 2;
                                else
                                    voidGrid[x, y, z] = 1; // 1: void filed without potental
                            }
                        }
                        catch
                        {
                            Debug.Log($"void 영역지정 에러 : ({x}, {y}, {z} | {voidGrid.GetLength(0)}, {voidGrid.GetLength(1)}, {voidGrid.GetLength(2)})");
                        }
                    }

            // GameObject 생성
            if ((!vr.overlapAllow && isEmptyCell) || vr.overlapAllow)
            {
                GameObject go = null;

                // world position 으로 변환
                Vector3 pos = new Vector3((newStartPosOnGrid.x + vr.gridOffset.x) * cubeSize,
                    (newStartPosOnGrid.y + vr.gridOffset.y) * cubeSize,
                    (newStartPosOnGrid.z + vr.gridOffset.z) * cubeSize);

                go = Instantiate(vr.voidRoomPrefab, pos, Quaternion.Euler(0, 90f * vr.rNum, 0));
                go.transform.parent = cubeMapParent.transform; // 부모 설정

                if (go != null)
                {
                    go.transform.localScale = go.transform.localScale * vr.scale;
                    otherDrawedGO.Add(go);
                    Debug.Log($"VoidRoom 배치 : {vr.voidRoomPrefab} | {pos}, wasNetworkObject flag: {vr.isNetworkObject}");
                }
            }
            else
            {
                Debug.LogError($"Void ({vr.name}) is not placed. This void is not allowed overlap.");
            }
        }
    }

    void CopyScriptableObject<ScriptableObject>(ScriptableObject source, ScriptableObject target)
    {
        string json = JsonUtility.ToJson(source);
        JsonUtility.FromJsonOverwrite(json, target);
    }

    // 모든 셀 초기화 (하나의 cell 이 모든 후보 cube를 갖도록)
    void InitializeGrid()
    {
        if (grid == null)
        {
            grid = new Cell[cubeMapSize.x, cubeMapSize.y, cubeMapSize.z];
            Debug.Log("grid is null. Generate new Grid.");
        } else
            Debug.Log("grid is not null");

        // voidRoom 과 연결을 위해 해당 위치에 임의의 cube 추가 
        CubeData[] cd = new CubeData[1];
        for (int z = 0; z < cubeMapSize.z; z++)
            for (int y = 0; y < cubeMapSize.y; y++)
                for (int x = 0; x < cubeMapSize.x; x++)
                {
                    if (grid[x, y, z] != null && grid[x, y, z].isFixed) continue; // regeneration 일 경우

                    if (voidGrid == null || voidGrid[x, y, z] == 0) // void Room 배치 영역이 아닌 경우
                        grid[x, y, z] = new Cell(allCubes); // 모든 후보 cube 를 갖는 cell 생성
                    else
                    {
                        // path 에 속하는 void 를 cube로 대체할 수 있는 경우
                        if (voidGrid[x, y, z] == 3 && pathway.Contains(new Vector3Int(x, y, z))) 
                            grid[x, y, z] = new Cell(allCubes);
                        else
                            grid[x, y, z] = new Cell(CubeDataForVoidFilling.ToArray());
                    }
                }
    }

    void GiveConstraints()
    {
        //Early constraint : cell 에서 규칙 맞지 않는 connection 을 갖는 cube 제거
        for (int z = 0; z < cubeMapSize.z; z++)
            for (int y = 0; y < cubeMapSize.y; y++)
                for (int x = 0; x < cubeMapSize.x; x++)
                {
                    // outer 에는 outer 만 남기기
                    if (isOuterEnable)
                    {
                        if (z == 0)
                            SelfConnectCheck(new Vector3Int(x, y, z), Connection.Outer, (t, c) => t.z0 == c);
                        if (z == cubeMapSize.z - 1)
                            SelfConnectCheck(new Vector3Int(x, y, z), Connection.Outer, (t, c) => t.z == c);
                        if (x == 0)
                            SelfConnectCheck(new Vector3Int(x, y, z), Connection.Outer, (t, c) => t.x0 == c);
                        if (x == cubeMapSize.x - 1)
                            SelfConnectCheck(new Vector3Int(x, y, z), Connection.Outer, (t, c) => t.x == c);

                        if ((z == 0 && x == 0) 
                            || (z == cubeMapSize.z - 1 && x == 0) 
                            || (z == 0 && x == cubeMapSize.x - 1) 
                            || (z == cubeMapSize.z - 1 && x == cubeMapSize.x - 1))
                        {
                            PaintPrefabs(defaultCubeDataCopy[1], materialDB.wallpaper); //
                            grid[x, y, z].possibleCubes = new List<CubeData> { defaultCubeDataCopy[1] };
                        }
                    }

                    if (pathway != null)
                    {
                        // cubedata 중 cell 의 pathIn or pathOut 의 dir 과 일치하는 connection 변수명의 값이 path 인 것만 후보로 남기기

                        //if (grid[x, y, z].pathIn != Vector3Int.zero || grid[x, y, z].pathOut != Vector3Int.zero)
                        //    if (grid[x, y, z].pathIn == Vector3Int.zero || grid[x, y, z].pathOut == Vector3Int.zero)
                        //        Debug.Log($"({x}, {y}, {z}) | {grid[x, y, z].pathIn} | {grid[x, y, z].pathOut}");

                        if (grid[x, y, z].pathIn == Vector3Int.up)
                            SelfConnectCheck(new Vector3Int(x, y, z), Connection.path, (t, c) => t.y0 == c);
                        else if (grid[x, y, z].pathIn == Vector3Int.down)
                            SelfConnectCheck(new Vector3Int(x, y, z), Connection.path, (t, c) => t.y == c);
                        else if (grid[x, y, z].pathIn == Vector3Int.left)
                            SelfConnectCheck(new Vector3Int(x, y, z), Connection.path, (t, c) => t.x == c);
                        else if (grid[x, y, z].pathIn == Vector3Int.right)
                            SelfConnectCheck(new Vector3Int(x, y, z), Connection.path, (t, c) => t.x0 == c);
                        else if (grid[x, y, z].pathIn == Vector3Int.forward)
                            SelfConnectCheck(new Vector3Int(x, y, z), Connection.path, (t, c) => t.z0 == c);
                        else if (grid[x, y, z].pathIn == Vector3Int.back)
                            SelfConnectCheck(new Vector3Int(x, y, z), Connection.path, (t, c) => t.z == c);

                        if (grid[x, y, z].pathOut == Vector3Int.up)
                            SelfConnectCheck(new Vector3Int(x, y, z), Connection.path, (t, c) => t.y == c);
                        else if (grid[x, y, z].pathOut == Vector3Int.down)
                            SelfConnectCheck(new Vector3Int(x, y, z), Connection.path, (t, c) => t.y0 == c);
                        else if (grid[x, y, z].pathOut == Vector3Int.left)
                            SelfConnectCheck(new Vector3Int(x, y, z), Connection.path, (t, c) => t.x0 == c);
                        else if (grid[x, y, z].pathOut == Vector3Int.right)
                            SelfConnectCheck(new Vector3Int(x, y, z), Connection.path, (t, c) => t.x == c);
                        else if (grid[x, y, z].pathOut == Vector3Int.forward)
                            SelfConnectCheck(new Vector3Int(x, y, z), Connection.path, (t, c) => t.z == c);
                        else if (grid[x, y, z].pathOut == Vector3Int.back)
                            SelfConnectCheck(new Vector3Int(x, y, z), Connection.path, (t, c) => t.z0 == c);
                    }

                    if (isExcludeEnable)
                    {
                        ConstrainCheck(new Vector3Int(x, y, z), (t) => !t.isEnable);

                        // 외곽 셀에 대한 제약조건 적용; cell 에 남길 cube 선택
                        if (z == 0 || z == cubeMapSize.z - 1 || y == 0 || y == cubeMapSize.y - 1 || x == 0 || x == cubeMapSize.x - 1)
                        {
                            if (isOuterEnable && y == 0)
                                if (z == 0 || z == cubeMapSize.z - 1 || x == 0 || x == cubeMapSize.x - 1) continue;

                            ConstrainCheck(new Vector3Int(x, y, z), (t) => !t.excludeAtSide);
                            ConstrainCheck(new Vector3Int(x, y, z), (t) => !t.excludeAtTop);
                            ConstrainCheck(new Vector3Int(x, y, z), (t) => !t.excludeAtBottom);
                        }
                        else // 내부 셀에서 outerOnly cube 제거
                        {
                            ConstrainCheck(new Vector3Int(x, y, z), (t) => !t.outerOnly);
                        }

                        // 고도 제한
                        ConstrainCheck(new Vector3Int(x, y, z), (t) => !t.excludeYRange.isEnable || t.excludeYRange.min > y || t.excludeYRange.max < y);

                        // 바닥 셀 조건
                        if (y == 0)
                        {
                            SelfConnectCheck(new Vector3Int(x, y, z), Connection.wall, (t, c) => t.y0 == c);
                        }
                    }
                }
    }

    private void SetPlane()
    {
        // plane 세팅; map size + padding
        if (setPlane && !setPlaneBefore)
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);

            // 기본 색상으로 지정
            plane.GetComponent<MeshRenderer>().material = materialDB.wallpaper;

            plane.transform.position = new Vector3((cubeMapSize.x - 1f) * cubeSize / 2f, - (cubeSize / 2f) - 0.01f, (cubeMapSize.z - 1f) * cubeSize / 2f);
            plane.transform.localScale *= planeScaleFactor;
            plane.transform.parent = cubeMapParent.transform;
            plane.layer = LayerMask.NameToLayer("Ground");

            setPlaneBefore = true;
            otherDrawedGO.Add(plane);
        }
    }

    public bool ConnectionCheckForCollapsed(Vector3Int pos)
    {
        CubeData centerCube = grid[pos.x, pos.y, pos.z].GetCollapsedCube(); // collapse 되지 않았으면 null 할당
        if (centerCube == null) return false;

        Vector3Int[] nPos = new Vector3Int[6];
        CubeData[] neighborCube = new CubeData[6];

        nPos[0] = pos + Vector3Int.right;
        nPos[1] = pos + Vector3Int.left;
        nPos[2] = pos + Vector3Int.up;
        nPos[3] = pos + Vector3Int.down;
        nPos[4] = pos + Vector3Int.forward;
        nPos[5] = pos + Vector3Int.back;

        for (int i = 0; i < nPos.Length; i++)
        {
            if (nPos[i].x < 0 || nPos[i].y < 0 || nPos[i].z < 0 || nPos[i].x >= cubeMapSize.x || nPos[i].y >= cubeMapSize.y || nPos[i].z >= cubeMapSize.z)
            {
                neighborCube[i] = null; 
                continue;
            }
            neighborCube[i] = grid[nPos[i].x, nPos[i].y, nPos[i].z].GetCollapsedCube(); 
        }

        Connection[] centerConnections = new Connection[6] { centerCube.x, centerCube.x0, centerCube.y, centerCube.y0, centerCube.z, centerCube.z0 };
        
        bool[] sixB = new bool[6];
        sixB[0] = centerConnections[0] == (neighborCube[0]?.x0 ?? centerConnections[0]);
        sixB[1] = centerConnections[1] == (neighborCube[1]?.x ?? centerConnections[1]);
        sixB[2] = centerConnections[2] == (neighborCube[2]?.y0 ?? centerConnections[2]);
        sixB[3] = centerConnections[3] == (neighborCube[3]?.y ?? centerConnections[3]);
        sixB[4] = centerConnections[4] == (neighborCube[4]?.z0 ?? centerConnections[4]);
        sixB[5] = centerConnections[5] == (neighborCube[5]?.z ?? centerConnections[5]);

        grid[pos.x, pos.y, pos.z].connectionState = sixB; // bool 저장
        grid[pos.x, pos.y, pos.z].connectionType = centerConnections; // Connection 저장

        if (!sixB[0] || !sixB[1] || !sixB[2] || !sixB[3] || !sixB[4] || !sixB[5])
            return false;
        else
            return true;
    }

    // Single connectivity check
    bool SelfConnectCheck(Vector3Int centerPos, Connection connection, System.Func<CubeData, Connection, bool> predicate)
    {
        var targetGrid = grid[centerPos.x, centerPos.y, centerPos.z];
        if (targetGrid.IsCollapsed) return false;

        int before = targetGrid.possibleCubes.Count;
        targetGrid.possibleCubes = targetGrid.possibleCubes.Where(t => predicate(t, connection)).ToList();

        //Debug.Log($"ConnectCheck : {centerPos} | {before} -> {targetGrid.possibleCubes.Count}");
        if (targetGrid.possibleCubes.Count != before) return true;

        //Debug.Log($"ConnectionFail : {centerPos}");
        return false;
    }

    bool ConstrainCheck(Vector3Int Pos, System.Func<CubeData, bool> predicate)
    {
        var targetGrid = grid[Pos.x, Pos.y, Pos.z];
        if (targetGrid.IsCollapsed) return false;

        int before = targetGrid.possibleCubes.Count;
        targetGrid.possibleCubes = targetGrid.possibleCubes.Where(t => predicate(t)).ToList();

        if (targetGrid.possibleCubes.Count != before)
        {
            //Debug.Log($"Costrained : {Pos} | {before} -> {targetGrid.possibleCubes.Count}");
            return true;
        } 

        //Debug.Log($"Costrained fail : {Pos}");
        return false;
    }

    // 최종 확정된 Cube을 Grid에 반영
    void ApplyToCubeMap()
    {
        drawedGO = new GameObject[cubeMapSize.x, cubeMapSize.y, cubeMapSize.z];

        for (int z = 0; z < cubeMapSize.z; z++)
            for (int y = 0; y < cubeMapSize.y; y++)
                for (int x = 0; x < cubeMapSize.x; x++)
                {
                    // x 가 저장된 grid x 보다 클 때, 인덱스 오류
                    if (x >= grid.GetLength(0) || y >= grid.GetLength(1) || z >= grid.GetLength(2)) continue;

                    if (grid[x, y, z].IsCollapsed)
                    {

                        List<CubeData> possibleCubes = new List<CubeData>();

                        string cubeName = grid[x, y, z].collapsedCube_name;
                        int rNum = grid[x, y, z].collapsedCube_rNum;
                        float yOffset = grid[x, y, z].collapsedCube_yOffset;
                        float scale = grid[x, y, z].collapsedCube_scale;

                        bool isInAllCubes = false;

                        CubeData collapseCubeData = defaultCubeDataCopy[2];

                        foreach (var data in allCubes) // SO 에 씬 오브젝트는 영구 저장 불가. allCubes 에서 찾아서 이름으로 매칭
                        {
                            if (data.cubeName == cubeName && data.rNum == rNum) 
                            {
                                collapseCubeData = data;
                                possibleCubes.Add(data);
                                isInAllCubes = true;
                                break; // 찾으면 나가기
                            }
                        }

                        if (!isInAllCubes) // 강제 배정
                            possibleCubes.Add(collapseCubeData);

                        // 좌표에 Cube 생성
                        GameObject go = Instantiate(collapseCubeData.cubePrefab,
                            new Vector3(x * cubeSize, y * cubeSize + yOffset, z * cubeSize),
                            Quaternion.Euler(0, 90f * rNum, 0));

                        go.transform.localScale = go.transform.localScale * scale;
                        drawedGO[x, y, z] = go;
                        drawedGO[x, y, z].transform.parent = cubeMapParent.transform; // 부모 설정

                        // cell에 좌표 저장
                        grid[x, y, z].worldPosition = new Vector3(x * cubeSize, y * cubeSize + yOffset, z * cubeSize);
                        grid[x, y, z].worldPositionInt = new Vector3Int(x * cubeSize, y * cubeSize, z * cubeSize);

                        grid[x, y, z].possibleCubes.Clear();
                        grid[x, y, z].possibleCubes = possibleCubes;
                        //grid[x, y, z].ApplyCollapsedInfo();

                        // 벽 사이 마감
                        if (drawStudNNode) CubeDecorationWithTag(go, grid[x, y, z].possibleCubes[0]);
                    }
                }

        
        // Generate 종료 후, 연결 상태 검사 및 문제 셀 로그 출력
        for (int z = 0; z < cubeMapSize.z; z++)
            for (int y = 0; y < cubeMapSize.y; y++)
                for (int x = 0; x < cubeMapSize.x; x++)
                {
                    if (!grid[x, y, z].IsCollapsed && grid[x, y, z].forcedCollapse)
                    {
                        Debug.LogWarning($"셀 ({x},{y},{z}) 가 collapse 되지 않음. 후보수: {grid[x, y, z].possibleCubes.Count} | Path : {grid[x, y, z].forcedCollapse}");
                    }

                    // 최종 확정된 연결 상태에 따라 셀 업데이트
                    // ConnectionCheckForCollapsed(new Vector3Int(x, y, z));
                }

        // 바닥 추가
        SetPlane();
    }


    void CubeDecorationWithTag(GameObject go, CubeData cube)
    {
        //CubeData cd = go.GetComponent<CubeData>();
        MeshRenderer[] me = go.GetComponentsInChildren<MeshRenderer>(true); // MarkerEdge 가져오기

        // Cube 마다 다른 local 회전값을 보정한 기둥 설치
        Vector3 dir = cube.rNum % 2 == 0 ? Vector3.forward : Vector3.right;
        Vector3 dir2 = cube.rNum % 2 == 0 ? Vector3.right : Vector3.forward;
        
        foreach (var e in me)
        {
            GameObject decoGO = null; // paint 대상
            if (e.CompareTag(tags[1])) // Stud 설치
            {
                if (e.transform.localPosition.y == 0.0f)
                    decoGO = Instantiate(stud, e.transform.position, Quaternion.identity);
                else if (e.transform.localPosition.x == 0.0f)
                    decoGO = Instantiate(stud, e.transform.position, Quaternion.Euler(dir * 90f));
                else if (e.transform.localPosition.z == 0.0f)
                    decoGO = Instantiate(stud, e.transform.position, Quaternion.Euler(dir2 * 90f));
                else
                    Debug.Log("stud object 생성 오류");

                if (decoGO != null)
                {
                    studGOs.Add(decoGO);
                    decoGO.transform.parent = cubeMapParent.transform; // 부모 설정
                }
            }
            else if (e.CompareTag(tags[0])) // Node 설치
            {
                // Vertex marker는 모든 큐브에 존재하므로 체크 후 빈 공간에는 설치하지 않음
                if (Physics.OverlapSphere(e.transform.position, 0.2f).Length > 1)
                    decoGO = Instantiate(node, e.transform.position, Quaternion.identity);

                if (decoGO != null)
                {
                    nodeGOs.Add(decoGO);
                    decoGO.transform.parent = cubeMapParent.transform; // 부모 설정
                }
            }
        }
    }

    private void PaintPrefabs(CubeData cd, Material mat)
    {
        if (!isPainting || !cd.isEnablePaint || mat == null) return;

        //Debug.Log(cd.cubePrefab.name);
        
        MeshRenderer[] renderers = cd.cubePrefab.GetComponentsInChildren<MeshRenderer>();

        foreach (MeshRenderer r in renderers)
        {
            r.transform.localScale = r.transform.localScale * meshScaleOffset; // 스케일 조정, 빛샘 방지
            r.material = mat;
        }
    }

    private void PaintPrefabs(GameObject go, Material mat)
    {
        if (!isPainting || mat == null) return;

        MeshRenderer[] renderers = go.GetComponentsInChildren<MeshRenderer>();

        foreach (MeshRenderer r in renderers)
        {
            r.transform.localScale = r.transform.localScale * meshScaleOffset; // 스케일 조정, 빛샘 방지
            r.material = mat;
        }
    }

    public void CombineMeshes()
    {
        Dictionary<Material, List<MeshFilter>> groups = new Dictionary<Material, List<MeshFilter>>();
        HashSet<GameObject> tempGO = new HashSet<GameObject>(); // 중복 방지

        // material별로 MeshRenderer와 Transform 수집
        List<MeshRenderer> renderers = new List<MeshRenderer>();
        List<MeshRenderer> renderersTemp = cubeMapParent.GetComponentsInChildren<MeshRenderer>(false).ToList();


        foreach (string l in layersToCombine) // 지정된 레이어의 mesh 합치기
        {
            renderers.AddRange(renderersTemp.Where(r => r.gameObject.layer == LayerMask.NameToLayer(l)).ToList());
            Debug.LogWarning($"Mesh Combine. Target layer : {l}");
        }

        foreach (var r in renderers)
        {
            if (tags.Any(tag => r.CompareTag(tag))) continue;
            if (!r.gameObject.activeSelf) continue;

            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            Material[] materials = r.sharedMaterials;
            int subMeshCount = mf.sharedMesh.subMeshCount;
            int count = Mathf.Min(materials.Length, subMeshCount);

            for (int i = 0; i < count; i++)
            {
                Material mat = materials[i];
                if (mat == null) continue;

                if (!groups.ContainsKey(mat))
                    groups[mat] = new List<MeshFilter>();

                groups[mat].Add(mf);
                tempGO.Add(r.gameObject);
            }
        }

        MeshCombiner.CombineMeshesByMaterial(groups, cubeMapParent, exportPath);
    }

    public void SetMarkerActive()
    {
        for (int i = 0; i < tags.Length; i++)
        {
            GameObject[] makers = GameObject.FindGameObjectsWithTag(tags[i]);
            
            // marker 숨기기
            if (!isVisibleMarker)
                foreach (var m in makers) m.SetActive(false);
            else
                foreach (var m in makers) m.SetActive(true);
        }

        if (!isVisibleMarker)
                Debug.Log("Set Marker Active False");
        else
                Debug.Log("Set Marker Active True");
    }

    public void ExchangeCubeSet()
    {
        // Resources 하위 폴더인 "Cubes" 에서 cube 프리팹 불러옴
        string pattern = @"(?<=Resources\/).*"; // ~Resources/"라는 문자열 바로 뒤에 오는 모든 문자열
        string path;
        Match match = Regex.Match(FullPathOfCubeResources, pattern);

        if (match.Success)
            path = match.Value; // 매치된 문자열 (즉, Resources/ 이후의 부분) 반환
        else
        {
            path = string.Empty; // 매치되는 부분이 없으면 빈 문자열 반환
            Debug.Log("교환 할 cubeset 이 없습니다.");
            return;
        }

        Debug.Log("path" + path);

        GameObject[] loadedPrefabs = Resources.LoadAll<GameObject>(path);

        foreach (GameObject prefab in loadedPrefabs)
        {
            if (prefab != null)
                newCubeList.Add(prefab);
        }

        Debug.Log(newCubeList.Count + "개의 프리팹을 불러왔습니다.");
   

        int count = 0;
        // grid[,,] 에서 cubeData 참고하여 다시 그리고 drawedGO 대체
        for (int x = 0; x < drawedGO.GetLength(0); x++)
            for (int y = 0; y < drawedGO.GetLength(1); y++)
                for (int z = 0; z < drawedGO.GetLength(2); z++)
                {
                    string cubeName = grid[x, y, z].collapsedCube_name;
                    int rNum = grid[x, y, z].collapsedCube_rNum;
                    float yOffset = grid[x, y, z].collapsedCube_yOffset;
                    float scale = grid[x, y, z].collapsedCube_scale;

                    //cd = grid[x, y, z].GetCollapsedCube(); 
                    //string cdName = Regex.Replace(cd.cubeName, @"\(Clone\)", "");

                    if (!grid[x, y, z].IsCollapsed)
                    {
                        Debug.LogError($"Cell ({x}, {y}, {z}) is not collabsed.");
                        continue;
                    }

                    foreach (var nc in newCubeList)
                    {
                        if (nc.name == cubeName)
                        {   
                            drawedGO[x, y, z].transform.parent = tempParent.transform;
                            Destroy(drawedGO[x, y, z]);

                            GameObject go = Instantiate(nc,
                                new Vector3(x * cubeSize, y * cubeSize + yOffset, z * cubeSize),
                                Quaternion.Euler(0, 90f * rNum, 0));
                            
                            go.transform.localScale = go.transform.localScale * scale;
                            drawedGO[x, y, z] = go;
                            drawedGO[x, y, z].transform.parent = cubeMapParent.transform; // 부모 설정
                            
                            count++;
                            //Debug.Log($"Cube has been exchanged at Cell ({x}, {y}, {z}). | {count}");
                        }
                    }

                    if (drawedGO[x, y, z].GetComponent<LightProbeGroup>() is LightProbeGroup lpg) // null 체크와 값 할당 동시에
                        lp.Add(lpg);
                }

        // light probe 개수 조절
        int n = 0;
        foreach (var i in lp)
        {
            if (i == null) continue;
            if (n % CubePerlightProbe == 0) i.enabled = true;
            else Destroy(i);
            //else i.enabled = false;
            n++;
        }

        SetMarkerActive();

        Debug.Log("Cube exchange is done.");
    }

    public Vector3Int WorldToGrid(Vector3Int worldPos)
    {
        return new Vector3Int (worldPos.x / cubeSize, worldPos.y / cubeSize, worldPos.z / cubeSize); 
    }

    public Vector3Int GridToWorld(Vector3Int worldPos)
    {
        return new Vector3Int(worldPos.x * cubeSize, worldPos.y * cubeSize, worldPos.z * cubeSize);
    }

    public void SaveGridInfo()
    {
        SaveGrid gridAsset = ScriptableObject.CreateInstance<SaveGrid>();

        if (grid == null)
        {
            Debug.LogError("Grid is null. Cannot save grid info.");
            return;
        }

        foreach (var i in grid)
            i.SetCollapsedInfo(); // collabsed cubeName, rNum 갱신

        gridAsset.Init(grid); // collapsed cubeData -> grid fields  
        gridAsset.pathList = pathway.ToList();
    }

    public void Initialize(bool isRegen = false)
    {
        // 생성된 Hierachy 의 생성된 object 지우기
        tempParent = GameObject.FindWithTag("Temp");
        cubeMapParent = GameObject.FindWithTag("CubeMapParent");

        if (tempParent != null) DestroyImmediate(cubeMapParent);
        if (cubeMapParent != null) DestroyImmediate(tempParent);

        CleanUp(isRegen);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Vector3 cubeDimensions = new Vector3(1, 1, 1);
        Vector3 cubeDimensions2 = new Vector3(1.5f, 1.5f, 1.5f);

        // --- Entry Cell 그리기 ---
        Gizmos.color = Color.green;
        Vector3 worldEntryCenter = new Vector3(
            entryCell.x * cubeSize,
            entryCell.y * cubeSize,
            entryCell.z * cubeSize
        );
        Gizmos.DrawWireCube(worldEntryCenter, cubeDimensions);

        // --- Exit Cell 그리기 ---
        Gizmos.color = Color.red;
        Vector3 worldExitCenter = new Vector3(
            exitCell.x * cubeSize,
            exitCell.y * cubeSize,
            exitCell.z * cubeSize
        );
        Gizmos.DrawWireCube(worldExitCenter, cubeDimensions);
    }
#endif
}