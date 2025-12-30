
using UnityEngine;
using System.Collections.Generic;

/*
GameObject
 ├─ MeshFilter     ---> Mesh (기하 정보: 정점, 삼각형, UV 등)
 └─ MeshRenderer   ---> Material(s) (시각 정보: 색상, 텍스처, 셰이더)

하나의 Mesh는 여러 개의 subMesh로 나뉠 수 있음
하나의 MeshRenderer는 여러 개의 Material을 가질 수 있음
Material[i]는 Mesh의 subMesh[i]에 대응됨
*/

public class MeshCombiner
{
    public static void CombineMeshesByMaterial(Dictionary<Material, List<MeshFilter>> groups, GameObject cubeMapParent, string exportPath)
    {
        foreach (var group in groups)
        {
            Material targetMaterial = group.Key;
            List<CombineInstance> combineList = new();

            foreach (var filter in group.Value)
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;

                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || renderer.sharedMaterials == null) continue;

                // 모든 subMesh 확인
                for (int subMeshIdx = 0; subMeshIdx < mesh.subMeshCount; subMeshIdx++)
                {
                    if (subMeshIdx >= renderer.sharedMaterials.Length) continue;
                    if (renderer.sharedMaterials[subMeshIdx] != targetMaterial) continue;

                    CombineInstance ci = new()
                    {
                        mesh = mesh,
                        subMeshIndex = subMeshIdx,
                        transform = filter.transform.localToWorldMatrix
                    };
                    combineList.Add(ci);
                }
            }

            if (combineList.Count == 0) continue;

            Mesh combinedMesh = new();
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combinedMesh.CombineMeshes(combineList.ToArray(), true, true); // mergeSubMeshes: true

            GameObject combinedObj = new($"CombinedMesh_{targetMaterial.name}");
            combinedObj.AddComponent<MeshFilter>().mesh = combinedMesh;
            combinedObj.AddComponent<MeshRenderer>().material = targetMaterial;
            combinedObj.isStatic = true;
            combinedObj.AddComponent<MeshCollider>().sharedMesh = combinedMesh;
            combinedObj.transform.parent = cubeMapParent.transform;

            foreach (var filter in group.Value)
            {
                if (filter != null && filter.gameObject != null)
                    filter.gameObject.SetActive(false);
            }
        }
    }
}
