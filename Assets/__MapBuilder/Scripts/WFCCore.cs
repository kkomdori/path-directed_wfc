using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WFCCore
{
    private Cell[,,] grid;
    private Vector3Int cubeMapSize;
    private CubeData defaultCubeData;

    // 큐브 가져와서 붙이기
    public Cell[,,] WFC(Cell[,,] InitGrid, Vector3Int cubeMapSize, CubeData defaultCubeData)
    {
        Debug.Log("WFC 시작");

        grid = InitGrid;
        this.cubeMapSize = cubeMapSize;
        this.defaultCubeData = defaultCubeData;

        Queue<Vector3Int> updateQueue = new Queue<Vector3Int>(); // 전파 큐 생성; 엔트로피 줄어든 셀 위치 저장

        while (true)
        {
            // 가장 낮은 엔트로피 셀 선택
            // 이미 collapsed 된 셀은 updateQueue에 들어가지 못하므로 constraint 조건 적용 시 최소 2개의 cube 배치
            var cellPos = FindLowestEntropyCell();

            if (cellPos == null) break;

            Collapse(cellPos.Value); // 셀 확정

            updateQueue.Enqueue(cellPos.Value);
            Propagate(updateQueue); // 변경 전파; 주변 그리드에서 선택 가능한 cell 만 남기는 과정
        }

        Debug.Log("생성완료");
        return grid;
    }

    // 아직 collapse되지 않은 셀 중 가장 엔트로피가 낮은 셀 위치 반환
    Vector3Int? FindLowestEntropyCell()
    {
        Vector3Int? lowestPos = null;
        int minEntropy = int.MaxValue;

        for (int z = 0; z < cubeMapSize.z; z++)
            for (int y = 0; y < cubeMapSize.y; y++)
                for (int x = 0; x < cubeMapSize.x; x++)
                {
                    var cell = grid[x, y, z];
                    if (cell.IsCollapsed || cell.possibleCubes.Count == 0) continue;

                    lowestPos = new Vector3Int(x, y, z); // 해당 위치 기록

                    // 강제 집행; path 경로
                    if (cell.forcedCollapse)    return lowestPos;

                    int entropy = cell.possibleCubes.Count;
                    if (entropy < minEntropy) minEntropy = entropy;
                }

        return lowestPos;
    }

    // Cell 내의 후보군 중 하나의 Cube를 가중치 반영해서 선정
    void Collapse(Vector3Int pos)
    {
        Cell cell = grid[pos.x, pos.y, pos.z];

        if (cell.possibleCubes.Count == 0)
        {
            Debug.LogError($"Collapse 실패: 후보 Cube가 없습니다. 위치: {pos}");
            return;
        }

        int totalWeight = cell.possibleCubes.Sum(t => t.weight);
        int rand = UnityEngine.Random.Range(0, totalWeight);
        int cumulative = 0;

        foreach (var cube in cell.possibleCubes)
        {

            cumulative += cube.weight;
            if (rand < cumulative)
            {
                cell.possibleCubes = new List<CubeData> { cube };
                return;
            }
        }

        // rand >= cumulative 일 때
        Debug.LogError($"가능한 cube 보다 많은 cube 존재함 => collapse with Last one. 위치: {pos}");
        cell.possibleCubes = new List<CubeData> { cell.possibleCubes.Last() };
        Debug.Log($"{pos} | {cell.forcedCollapse} | {cell.possibleCubes} | {rand} | {cumulative} ");
    }

    // 변경된 셀 기준으로 주변 셀에 전파
    void Propagate(Queue<Vector3Int> updateQueue)
    {

        while (updateQueue.Count > 0)
        {
            bool isConnect = false;

            Vector3Int pos = updateQueue.Dequeue();
            CubeData centerCube = grid[pos.x, pos.y, pos.z].GetCollapsedCube();
            //Debug.Log("Dequeue : " + pos);
            
            if (centerCube == null) continue;
            isConnect = NeighborConnectCheck(pos, Vector3Int.up, centerCube.y, (t, c) => t.y0 == c); // offset 위치의 target pos 가 center 와 연결되는 cube 추리기
            if (isConnect) UpdateNeighborCell(pos, Vector3Int.up, updateQueue); // 큐 업데이트
            isConnect = NeighborConnectCheck(pos, Vector3Int.down, centerCube.y0, (t, c) => t.y == c);
            if (isConnect) UpdateNeighborCell(pos, Vector3Int.down, updateQueue);
            isConnect = NeighborConnectCheck(pos, Vector3Int.right, centerCube.x, (t, c) => t.x0 == c);
            if (isConnect) UpdateNeighborCell(pos, Vector3Int.right, updateQueue);
            isConnect = NeighborConnectCheck(pos, Vector3Int.left, centerCube.x0, (t, c) => t.x == c);
            if (isConnect) UpdateNeighborCell(pos, Vector3Int.left, updateQueue);
            isConnect = NeighborConnectCheck(pos, Vector3Int.forward, centerCube.z, (t, c) => t.z0 == c);
            if (isConnect) UpdateNeighborCell(pos, Vector3Int.forward, updateQueue);
            isConnect = NeighborConnectCheck(pos, Vector3Int.back, centerCube.z0, (t, c) => t.z == c);
            if (isConnect) UpdateNeighborCell(pos, Vector3Int.back, updateQueue);
        }
    }

    // 인접 셀 후보군을 필터링하고 줄어들었을 경우 전파 큐에 다시 넣음
    void UpdateNeighborCell(Vector3Int centerPos, Vector3Int offset, Queue<Vector3Int> queue)
    {
        Vector3Int nPos = centerPos + offset;
        queue.Enqueue(nPos);
    }

    bool NeighborConnectCheck(Vector3Int centerPos, Vector3Int offset, Connection connection, System.Func<CubeData, Connection, bool> predicate)
    {
        // 이웃 셀을 center의 connection에 맞게 변화시킴 

        Vector3Int nPos = centerPos + offset;
        if (nPos.x < 0 || nPos.y < 0 || nPos.z < 0 || nPos.x >= cubeMapSize.x || nPos.y >= cubeMapSize.y || nPos.z >= cubeMapSize.z) return false;

        Cell neighbor = grid[nPos.x, nPos.y, nPos.z];
        if (neighbor.IsCollapsed || neighbor.forcedCollapse) return false; // 이웃 셀이 고정일 때, 변화 시키지 않음

        int before = neighbor.possibleCubes.Count;

        //Late constraint : center 의 notAllowDimer 항목이 체크되어 있으면 인접 셀에서 center 와 동일한 id 의 cube 제거 
        CubeData cube = grid[centerPos.x, centerPos.y, centerPos.z].GetCollapsedCube();
        if (cube.notAllowDimer)
        {
            neighbor.possibleCubes = neighbor.possibleCubes.Where(t => cube.cubeName != t.cubeName).ToList();
        }

        //Connectivity check
        neighbor.possibleCubes = neighbor.possibleCubes.Where(t => predicate(t, connection)).ToList();

        if (neighbor.possibleCubes.Count == 0)
        {
            // Late constraint, connectivity check 과정에서 발생할 수 있는 non-collapsed & 0 possiblity 문제 해결 
            neighbor.possibleCubes = new List<CubeData> { defaultCubeData };
            //Debug.Log("NeighborConnectCheck -> defaultCube");
        }
        else if (neighbor.possibleCubes.Count != before)
        {
            return true; // 업데이트 큐에 등록 허가
        }

        //Debug.Log($"possibleCubes : {nPos}, Before : {before}, After : {neighbor.possibleCubes.Count}");
        return false;
    }
}
