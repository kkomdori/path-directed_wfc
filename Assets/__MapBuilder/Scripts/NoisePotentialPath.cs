using System.Collections.Generic;
using UnityEngine;

public class NoisePotentialPath
{
    private Vector3Int gridSize;
    private Vector3Int startCell;
    private Vector3Int endCell;
    private float noiseAmplitude = 5f;
    private int maxSteps = 2048; // 무한루프 방지
    private int maxTrial = 100;
    private float tolerence;

    public float[,,] phi;                 // 전위맵
    readonly Vector3Int[] dirs =
    {
        Vector3Int.right, Vector3Int.left,
        Vector3Int.up,    Vector3Int.down,
        Vector3Int.forward, Vector3Int.back
    };

    List<Vector3Int> pathList;       // 최종 경로

    public List<Vector3Int> PathFinder(Vector3Int mapSize, Vector3Int start, Vector3Int end, float noise, float tolerence)
    {
        gridSize = mapSize;
        startCell = start;
        endCell = end;
        noiseAmplitude = noise;
        this.tolerence = tolerence;

        BuildPotentialField();
        TracePath();
        //DrawLine();

        return pathList;
    }

    /*-------------------------------------------------------------*/
    void BuildPotentialField()
    {
        phi = new float[gridSize.x, gridSize.y, gridSize.z];
        float totalDist = Vector3.Distance(startCell, endCell);

        for (int x = 0; x < gridSize.x; ++x)
            for (int y = 0; y < gridSize.y; ++y)
                for (int z = 0; z < gridSize.z; ++z)
                {
                    if (WFCGeneratorForCube.wfcg.voidGrid != null && WFCGeneratorForCube.wfcg.voidGrid[x, y, z] == 1)
                    {
                        phi[x, y, z] = 500f; // void room 위치한 cell 은 저항으로 작용
                        continue;
                    }

                    float basePhi = 100f * Vector3.Distance(new Vector3(x, y, z), endCell) / totalDist;
                    float noise = UnityEngine.Random.Range(-1f, 1f) * noiseAmplitude; // ±noise

                    if (WFCGeneratorForCube.wfcg.voidGrid != null && (WFCGeneratorForCube.wfcg.voidGrid[x, y, z] == 2 
                        || WFCGeneratorForCube.wfcg.voidGrid[x, y, z] == 3))
                        phi[x, y, z] = Mathf.Clamp(basePhi + noise, 10f, 90f) * 0.1f; // 전위차 기반 path 통과 할 수 있게 전위값을 낮춤
                    else
                        phi[x, y, z] = Mathf.Clamp(basePhi + noise, 10f, 90f); // 일반 cell 의 전위값
                }

        phi[startCell.x, startCell.y, startCell.z] = 100f;
        phi[endCell.x, endCell.y, endCell.z] = 0f;
    }

    /*-------------------------------------------------------------*/
    void TracePath()
    {
        Debug.Log($"Path-finding start : {startCell} -> {endCell} | magnitude: {(endCell - startCell).magnitude}");

        pathList = new();
        HashSet<Vector3Int> visited = new() { startCell };

        Vector3Int current = startCell;
        pathList.Add(current);

        List<Vector3Int> dir = new List<Vector3Int>();

        int trial = 0;
        while (trial < maxTrial)
        {
            for (int step = 0; step < maxSteps && current != endCell; ++step)
            {   
                Vector3Int bestNext = current;
                Vector3Int bestDir = Vector3Int.zero;

                // 현재 셀의 potential 초기화 = 전위 재충전 (= 번개 채널에서 Stepped Leader 와 유사한 방식)
                float bestPhi = phi[startCell.x, startCell.y, startCell.z]; 

                foreach (var d in dirs)
                {
                    Vector3Int nb = current + d;
                    if (!InBounds(nb) || visited.Contains(nb)) continue;
                    //if (d == Vector3Int.up && dir.Count >= 1 && dir[^1] == Vector3Int.up) continue; // y축 방향으로 연달아 두번 상승 방지
                    //if (d == Vector3Int.up && dir.Count >= 2 && dir[^2] == Vector3Int.up) continue; // y축 방향으로 번갈아 두번 상승 방지
                    //if (d == Vector3Int.up && dir.Count >= 3 && dir[^3] == d * -1) continue; // U turn 방지
                    //if (d == Vector3Int.down && dir.Count >= 3 && dir[^3] == d * -1) continue; // U turn 방지
                    //if (dir.Count >= 2 && dir[^2] != dir[^1])  continue; // 짧은 지그재그 방지

                    float nbPhi = phi[nb.x, nb.y, nb.z];
                    if (nbPhi < bestPhi) // 가장 낮은 potential 을 기록
                    {
                        bestPhi = nbPhi;
                        bestNext = nb;
                        bestDir = d;
                    }
                }

                if (bestNext == current)   // 더 낮은 potential 이 없으면 로컬 minima ⇒ 중단
                    break;

                dir.Add(bestDir);
                current = bestNext;
                visited.Add(current);
                pathList.Add(current);

                //Debug.Log($"current cell: {current}");
            }

            // 너무 돌아가는 경로는 배제; 기준치는 두 점 사이 거리의 제곱에 비례하도록 설정
            if (pathList.Count < (endCell - startCell).magnitude * (endCell - startCell).magnitude * tolerence && current == endCell)
            {
                Debug.Log($"Path-finding is success. | Cell count : {pathList.Count} | Trials : {trial}");
                break;
            }

            Debug.Log($"Retry path-finding ({trial})");
            trial++;
        }

        if (trial >= maxTrial)
        {
            pathList = null;
            Debug.LogError($"Path-finding is failed");
        }
    }

    bool InBounds(Vector3Int v)
    {
        int startOffset = 0;
        int endOffset = 0; 

        if (WFCGeneratorForCube.wfcg.isOuterEnable)
        {
            startOffset += 1;
            endOffset -= 1;
        }

        if (v.x >= startOffset && v.y >= 0 && v.z >= startOffset &&
            v.x <= gridSize.x - 1 + endOffset && v.y <= gridSize.y - 1 && v.z <= gridSize.z - 1 + endOffset)
            return true;

        return false;
    }
}
