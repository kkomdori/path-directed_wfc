using UnityEngine;

public enum RoomRotation
{
    D0,
    D90,
    D180,
    D270,
}

[CreateAssetMenu(fileName = "VoidRoomData", menuName = "WFCforCube/VoidRoomData")]
public class VoidRoomData : ScriptableObject
{
    public GameObject voidRoomPrefab;
    public int roomCopyNumber = 1;
    public float scale = 1f;
    public Vector3Int gridOffset = new Vector3Int(0, 0, 0);

    [Tooltip("Room 을 Exit Cell 과 접촉하도록 위치시킵니다.")]
    public bool attachToExit = false; // room을 exitCell 바로 옆에 위치시킴
    [Tooltip("Exit Cell 이 위치 할 상대좌표")]
    public Vector3Int exitCellPivot = new Vector3Int(1, 0, -1);

    [Header("RoomTransforms")]
    public Vector3Int startPosOnGrid; 
    public Vector3Int sizeOnGrid;
    public bool isRandomPosition = false;
    public bool fixYLocation = false; // random location 시 Y 좌표를 고정할지 여부
    public bool overlapAllow = false; // 현재 voidRoomPrefab 가 이미 배치된 것과 겹쳐도 되는지 여부
    public bool isRandomRotation = false;
    public RoomRotation rotation;
    [HideInInspector]
    public int rNum = 0; // rotation number
    public bool pathFindingAllow = false; // path finding process가 해당 voidroom을 통과하게 할 지 여부
    public bool isReplacibleToPathCube = false; // path가 지나가는 곳을 path cube로 대체할 수 있는지 여부
    public bool isNetworkObject = false;
}
