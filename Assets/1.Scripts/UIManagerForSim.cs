using UnityEngine;

public class UIManagerForSim : MonoBehaviour
{
    public GameObject settingPanel;
    private bool isSettingPanelView = true;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (isSettingPanelView)
            {
                isSettingPanelView = false;
                settingPanel.SetActive(false);
            }
            else
            {
                isSettingPanelView = true;
                settingPanel.SetActive(true);
            }
        }
    }
}
