using UnityEngine;
using UnityEngine.UI;

public enum RotateAxis
{
    x,
    y,
    z
}

[RequireComponent(typeof(CamRotate))]
[RequireComponent(typeof(PlayerRotate))]
[RequireComponent(typeof(CharacterController))]

public class PlayerControllerTest : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float sneakSpeed = 1.5f;
    public float jumpPower = 5f;
    private float speed;

    [Header("Physics")]
    public bool useGravity = false;
    private CharacterController cc;
    private float gravity = -20f;
    private float yVelocity = 0f;
    private bool isJumping = false;

    private void Start()
    {
        cc = GetComponent<CharacterController>();
        speed = walkSpeed;
    }

    private void Update()
    {
        if (cc == null || !cc.enabled) return;

        if (useGravity)
        {
            ApplyGravity();
            HandleJump();
        }
        else
        {
            HandleUpAndDown();
        }

        HandleMovement();
    }

    void ApplyGravity()
    {
        yVelocity += gravity * Time.deltaTime;
        Vector3 move = new Vector3(0, yVelocity, 0);
        cc.Move(move * Time.deltaTime);

        if (cc.isGrounded && yVelocity < 0)
        {
            yVelocity = -1f;
            isJumping = false;
        }
    }

    void HandleMovement()
    {
        float xx = Input.GetAxisRaw("Horizontal");
        float zz = Input.GetAxisRaw("Vertical");

        Vector3 dir = new Vector3(xx, 0, zz).normalized;

        if (dir.magnitude > 0)
        {
            dir = Camera.main.transform.TransformDirection(dir);
            dir.y = 0;

            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                speed = sneakSpeed;
            }
            else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                speed = runSpeed;
            }
            else speed = walkSpeed;
        }
        else speed = 0f;

        Vector3 move = dir * speed;
        move.y = yVelocity;
        cc.Move(move * Time.deltaTime);
    }

    void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && !isJumping && cc.isGrounded)
        {
            yVelocity = jumpPower;
            isJumping = true;
        }

        if (cc.isGrounded && yVelocity < 0)
        {
            yVelocity = -1f;
            isJumping = false;
        }
    }

    void HandleUpAndDown()
    {
        Vector3 dir = Vector3.zero;

        if (Input.GetButton("Jump"))
        {
            cc.Move(Vector3.up * jumpPower * 2f * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C))
        {
            cc.Move(Vector3.down * jumpPower * 2f * Time.deltaTime);
        }
    }
}
