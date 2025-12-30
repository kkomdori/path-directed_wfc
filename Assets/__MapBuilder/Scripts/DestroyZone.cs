using UnityEngine;

public class DestroyZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            other.transform.position = WFCGeneratorForCube.wfcg.GridToWorld(WFCGeneratorForCube.wfcg.entryCell);
        }
        else
        {
            other.gameObject.SetActive(false);
        }
    }
}
