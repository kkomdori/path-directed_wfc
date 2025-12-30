using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.AI;

[System.Serializable]
// WFC 셀 하나의 상태 (가능한 타일 후보군 보유)
public class Cell
{
    public List<CubeData> possibleCubes; // 후보군 cube 저장소

    // collapse 상태 저장
    public bool IsCollapsed => possibleCubes.Count == 1;
    public bool[] connectionState = new bool[6]; // right, left, up, down, forward, back
    public Connection[] connectionType = new Connection[6];
    public Vector3 worldPosition;
    public Vector3Int worldPositionInt;
    public bool isTouched = false; // 에디터에서 클릭된 상태
    public bool isFixed = false; // 에디터에서 클릭된 상태

    // path 방향에 따라 constraint 적용
    public bool forcedCollapse = false;
    public Vector3Int pathIn = Vector3Int.zero; 
    public Vector3Int pathOut = Vector3Int.zero;

    public string collapsedCube_name = "";
    public int collapsedCube_rNum = 0;
    public float collapsedCube_scale = 1f;
    public float collapsedCube_yOffset = 0f;
    public bool collapsedCube_isEnablePaint = true;

    public Cell(CubeData[] allCubes)
    {
        if (isFixed == false) possibleCubes = new List<CubeData>(allCubes);
    }

    public CubeData GetCollapsedCube() => IsCollapsed ? possibleCubes[0] : null; 

    public void SetCollapsedInfo()
    {
        if (IsCollapsed)
        {
            if (possibleCubes[0] == null) return;

            collapsedCube_name = possibleCubes[0].cubeName;
            collapsedCube_rNum = possibleCubes[0].rNum;
            collapsedCube_scale = possibleCubes[0].scale;
            collapsedCube_yOffset = possibleCubes[0].yOffset;
            collapsedCube_isEnablePaint = possibleCubes[0].isEnablePaint;
        }
    }

    public void ApplyCollapsedInfo()
    {
        if (IsCollapsed)
        {
            if (possibleCubes[0] == null) return;

            possibleCubes[0].cubeName = collapsedCube_name;
            possibleCubes[0].rNum = collapsedCube_rNum;
            possibleCubes[0].scale = collapsedCube_scale;
            possibleCubes[0].yOffset = collapsedCube_yOffset;
            possibleCubes[0].isEnablePaint = collapsedCube_isEnablePaint;
        }
    }
}