using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControlManager : MonoBehaviour
{

    #region Singleton
    public static ControlManager gm;
    private void Awake()
    {
        if (gm != null)
            Destroy(gameObject);
        else
            gm = this;
    }
    #endregion


    public enum MovingMode
    {
        normal,
        elevator,
    }

    public MovingMode movingMode;

    /*
    private void Start()
    {
        // 유니티에서 플레이 화면에 마우스 감추기
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        movingMode = MovingMode.normal;
    }
    */
}
