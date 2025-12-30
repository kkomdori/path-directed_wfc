using UnityEngine;

public class CamRotate : MonoBehaviour
{
    public float rotSpeed = 1.5f;
    public float smoothSpeed = 10f; // higher = snappier, lower = smoother
    public RotateAxis rotateAxis;

    private float mv = 0; // vertical mouse value

    private void Update()
    {
        float mouse_Y = Input.GetAxis("Mouse Y");
        mv += mouse_Y * rotSpeed;
        mv = Mathf.Clamp(mv, -90f, 90f);

        Vector3 current = transform.eulerAngles;
        Vector3 target = current;

        switch (rotateAxis)
        {
            case RotateAxis.x:
                target.x = -mv;
                break;
            case RotateAxis.y:
                target.y = -mv;
                break;
            default:
                target.z = -mv;
                break;
        }

        float t = smoothSpeed * Time.deltaTime;
        Vector3 smoothed = new Vector3(
            Mathf.LerpAngle(current.x, target.x, t),
            Mathf.LerpAngle(current.y, target.y, t),
            Mathf.LerpAngle(current.z, target.z, t)
        );

        transform.eulerAngles = smoothed;
    }
}
