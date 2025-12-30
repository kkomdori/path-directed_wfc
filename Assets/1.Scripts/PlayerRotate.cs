using UnityEngine;


public class PlayerRotate : MonoBehaviour
{

    public float rotSpeed = 5f;
    public float smoothSpeed = 10f; // higher = snappier, lower = smoother
    public RotateAxis rotateAxis;

    private float mh = 0; // horizontal mouse value

    private void Update()
    {
        if (Cursor.lockState == CursorLockMode.None) return;

        float mouse_X = Input.GetAxis("Mouse X");
        mh += mouse_X * rotSpeed;

        // current and target euler angles
        Vector3 current = transform.eulerAngles;
        Vector3 target = current;

        switch (rotateAxis)
        {
            case RotateAxis.x:
                target.x = mh;
                break;
            case RotateAxis.y:
                target.y = mh;
                break;
            default:
                target.z = mh;
                break;
        }

        // Interpolate angles smoothly (handles wrap-around)
        float t = smoothSpeed * Time.deltaTime;
        Vector3 smoothed = new Vector3(
            Mathf.LerpAngle(current.x, target.x, t),
            Mathf.LerpAngle(current.y, target.y, t),
            Mathf.LerpAngle(current.z, target.z, t)
        );

        transform.eulerAngles = smoothed;
    }
}