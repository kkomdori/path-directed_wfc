using System.Collections.Generic;
using UnityEngine;

public class PotentialMapVisualizer : MonoBehaviour
{
    private LineRenderer lineRenderer;

    public Material baseMaterial;
    public float cubeSize;
    public Vector3 cubeMapSize;
    public float[,,] phi;
    public bool inDrawingOn = true;

    private Mesh indicatorMesh;
    private Material indicatorMaterial;
    private MaterialPropertyBlock indicatorPropBlock;

    // LineRenderer용 머티리얼 캐싱 (메모리 누수 방지)
    private Material lineMaterial;

    private struct DrawData
    {
        public Matrix4x4 matrix;
        public Color color;
    }
    private DrawData[] drawDataCache;
    private int drawCount = 0;

    private void OnDestroy()
    {
        // 생성한 머티리얼들 반드시 파괴
        if (indicatorMaterial != null) Destroy(indicatorMaterial);
        if (lineMaterial != null) Destroy(lineMaterial);
    }

    private void Update()
    {
        if (!inDrawingOn) return;
        if (drawDataCache == null || indicatorMesh == null || indicatorMaterial == null) return;

        // [최적화 팁] 만약 drawCount가 수천 개라면 Graphics.DrawMeshInstanced 사용을 고려해야 함.
        // 현재 방식(DrawMesh 루프)은 약 1~2천 개까지는 괜찮음.
        for (int i = 0; i < drawCount; i++)
        {
            indicatorPropBlock.SetColor("_Color", drawDataCache[i].color);
            // URP 환경이라면 아래 주석 해제 (Standard Shader는 _Color만 쓰면 됨)
            // indicatorPropBlock.SetColor("_BaseColor", drawDataCache[i].color); 

            Graphics.DrawMesh(
                indicatorMesh,
                drawDataCache[i].matrix,
                indicatorMaterial,
                0,
                null,
                0,
                indicatorPropBlock
            );
        }
    }

    public void Initialize(int cubeSize, Vector3Int cubeMapSize, float[,,] phi, Material mat = null)
    {
        this.cubeSize = cubeSize;
        this.cubeMapSize = cubeMapSize;
        this.phi = phi;
        if (mat != null) this.baseMaterial = mat;
        BakePotentialMap();
    }

    public void BakePotentialMap()
    {
        if (phi == null) return;

        // --- (1) 리소스 준비 ---
        if (indicatorMesh == null)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicatorMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(temp);
        }

        if (indicatorMaterial == null)
        {
            if (baseMaterial != null)
                indicatorMaterial = new Material(baseMaterial);
            else
                indicatorMaterial = new Material(Shader.Find("Standard"));

            // GPU Instancing 활성화 (필수)
            indicatorMaterial.enableInstancing = true;

            // 투명 모드 설정 (Standard Shader 기준)
            SetupMaterialTransparent(indicatorMaterial);
        }

        if (indicatorPropBlock == null) indicatorPropBlock = new MaterialPropertyBlock();

        // --- (2) Min/Max 계산 및 캐싱 (기존과 동일) ---
        int sx = phi.GetLength(0);
        int sy = phi.GetLength(1);
        int sz = phi.GetLength(2);

        float min = float.MaxValue;
        float max = float.MinValue;

        for (int x = 0; x < sx; x++)
            for (int y = 0; y < sy; y++)
                for (int z = 0; z < sz; z++)
                {
                    float v = Mathf.Clamp(phi[x, y, z], 0f, 120f);
                    if (float.IsNaN(v) || float.IsInfinity(v)) continue;
                    if (v < min) min = v;
                    if (v > max) max = v;
                }

        float range = Mathf.Max(1e-6f, max - min);

        // --- (3) 그리기 데이터 캐싱 ---
        int maxCount = sx * sy * sz;
        if (drawDataCache == null || drawDataCache.Length != maxCount)
            drawDataCache = new DrawData[maxCount];

        drawCount = 0;

        for (int x = 0; x < Mathf.Min(sx, cubeMapSize.x); x++)
        {
            for (int y = 0; y < Mathf.Min(sy, cubeMapSize.y); y++)
            {
                for (int z = 0; z < Mathf.Min(sz, cubeMapSize.z); z++)
                {
                    float v = phi[x, y, z];
                    if (float.IsNaN(v) || float.IsInfinity(v)) continue;

                    float n = Mathf.Clamp01((v - min) / range);
                    float sizeScale = cubeSize * 0.5f;
                    float alpha = 0.3f; // 투명도

                    Color targetColor = GetHSVColor(n);
                    targetColor.a = alpha;

                    Vector3 pos = new Vector3(x * cubeSize, y * cubeSize, z * cubeSize);

                    drawDataCache[drawCount].matrix = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * sizeScale);
                    drawDataCache[drawCount].color = targetColor;
                    drawCount++;
                }
            }
        }
    }

    // [보조 함수] Standard Shader 투명 설정 분리
    private void SetupMaterialTransparent(Material mat)
    {
        mat.SetFloat("_Mode", 3); // 3 = Transparent
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON"); // Standard 쉐이더는 _ALPHAPREMULTIPLY_ON을 쓸 수도 있음
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON"); // 투명도 블렌딩을 위해 보통 사용
        mat.renderQueue = 3000;
    }

    public void DrawPath(List<Vector3Int> path)
    {
        if (path == null || path.Count < 2) return;

        // PathLine 오브젝트가 없으면 생성, 있으면 재사용
        GameObject go;
        Transform child = transform.Find("PathLine");
        if (child != null)
        {
            go = child.gameObject;
            lineRenderer = go.GetComponent<LineRenderer>();
        }
        else
        {
            go = new GameObject("PathLine");
            go.transform.SetParent(this.transform);
            lineRenderer = go.AddComponent<LineRenderer>();

            // 머티리얼 재사용 (누수 방지)
            if (lineMaterial == null)
                lineMaterial = new Material(Shader.Find("Sprites/Default"));

            lineRenderer.material = lineMaterial;
        }

        lineRenderer.startColor = Color.yellow;
        lineRenderer.endColor = Color.yellow;
        lineRenderer.startWidth = 0.5f;
        lineRenderer.endWidth = 0.5f;
        lineRenderer.numCornerVertices = 5;

        lineRenderer.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++)
        {
            lineRenderer.SetPosition(i, (Vector3)path[i] * cubeSize);
        }
    }

    private Color GetHSVColor(float n)
    {
        // (기존 코드 동일)
        float h1, s1, v1;
        float h2, s2, v2;
        Color.RGBToHSV(Color.red, out h1, out s1, out v1);
        Color.RGBToHSV(Color.cyan, out h2, out s2, out v2);
        float lerpH = Mathf.Lerp(h1, h2, n);
        return Color.HSVToRGB(lerpH, 1f, 1f);
    }
}