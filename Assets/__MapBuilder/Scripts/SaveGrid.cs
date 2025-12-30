using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class SaveGrid : ScriptableObject
{
    public List<CellWrapper> serializedGrid; // Cell은 직접 저장 안 됨
    public Vector3Int gridSize;
    public List<Vector3Int> pathList;

    [System.Serializable]
    public class CellWrapper
    {
        public Vector3Int pos;
        public Cell cell; // prefab 부분은 저장 불가; 아마도 Cell 데이터형이 복잡해서?
        public string cubeName;
        public int rNum;
        public float scale;
        public float yOffset;
        public bool isEnablePaint;
    }

    public void Init(Cell[,,] grid)
    {
        serializedGrid = new List<CellWrapper>();
        for (int x = 0; x < grid.GetLength(0); x++)
            for (int y = 0; y < grid.GetLength(1); y++)
                for (int z = 0; z < grid.GetLength(2); z++)
                {
                    //if (grid[x, y, z].possibleCubes[0] == null) continue;
                    if (!grid[x, y, z].IsCollapsed) continue;

                    //Debug.Log(grid[x, y, z].possibleCubes[0].cubeName);


                    CellWrapper wrapper = new CellWrapper // 변수 한번에 초기화
                    {
                        pos = new Vector3Int(x, y, z),
                        cell = grid[x, y, z],

                        cubeName = grid[x, y, z].possibleCubes[0].cubeName,
                        rNum = grid[x, y, z].possibleCubes[0].rNum,
                        scale = grid[x, y, z].possibleCubes[0].scale,
                        yOffset = grid[x, y, z].possibleCubes[0].yOffset,
                        isEnablePaint = grid[x, y, z].possibleCubes[0].isEnablePaint,
                    };

                    serializedGrid.Add(wrapper);
                }
        gridSize = new Vector3Int(grid.GetLength(0), grid.GetLength(1), grid.GetLength(2));
    }

    public Cell[,,] ToGrid(Vector3Int size)
    {
        Cell[,,] grid = new Cell[gridSize.x, gridSize.y, gridSize.z];

        foreach (var wrapper in serializedGrid)
        {
            if (wrapper.pos.x >= gridSize.x || wrapper.pos.y >= gridSize.y || wrapper.pos.z >= gridSize.z) continue;

            grid[wrapper.pos.x, wrapper.pos.y, wrapper.pos.z] = wrapper.cell;
            grid[wrapper.pos.x, wrapper.pos.y, wrapper.pos.z].collapsedCube_name = wrapper.cubeName;
            grid[wrapper.pos.x, wrapper.pos.y, wrapper.pos.z].collapsedCube_rNum = wrapper.rNum;
            grid[wrapper.pos.x, wrapper.pos.y, wrapper.pos.z].collapsedCube_scale = wrapper.scale;
            grid[wrapper.pos.x, wrapper.pos.y, wrapper.pos.z].collapsedCube_yOffset = wrapper.yOffset;
            grid[wrapper.pos.x, wrapper.pos.y, wrapper.pos.z].collapsedCube_isEnablePaint = wrapper.isEnablePaint;
        }
        return grid;
    }
}
