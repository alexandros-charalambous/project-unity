using System;
using UnityEngine;

public class PlayerMovementController : MonoBehaviour
{
    public CharacterController characterController;
    public Transform camTransform;

    [Header("Movement Parameters")]
    [SerializeField] private float currentSpeed;
    private Vector3 velocity;

    [Header("Jump Parameters")]
    [SerializeField] private float gravity;
    [SerializeField] private float jumpHeight;
    public float jumpVelocity;
    [SerializeField] private Vector3 onAirDirectionVelocity;

    [Header("Slide Parameters")]
    [SerializeField] private float groundAngle;
    [SerializeField] private Vector3 slideDirectionVelocity;
    public Boolean isSliding;


    void Start()
    {
        characterController = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        if (Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0)
        {
            var forwardMovement = DirectionMovement();
            var directionMovement = new Vector3(forwardMovement.x, VerticalMovement().y, forwardMovement.z);
            characterController.Move(directionMovement * Time.deltaTime);
        }
        else
        {
            characterController.Move(VerticalMovement() * Time.deltaTime);
            currentSpeed = 7.5f;
        }
    }

    private Vector3 DirectionMovement()
    {
        Vector3 direction = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical")).normalized;
        float speed = GetSpeed(direction);
        velocity =
            //slide movement
            groundAngle > characterController.slopeLimit && direction.magnitude != 0f ? ForwardMovement(direction) * speed * .6f + slideDirectionVelocity
            //walk jump movement
            : onAirDirectionVelocity != Vector3.zero ? ForwardMovement(direction) * speed * .3f + onAirDirectionVelocity * speed * .7f
            //idle jump movement
            : direction.magnitude != 0f && !characterController.isGrounded ? ForwardMovement(direction) * speed * .6f
            //normal movement
            : direction.magnitude != 0f ? ForwardMovement(direction) * speed
            //idle
            : Vector3.zero;
        return velocity;
    }

    private Vector3 ForwardMovement(Vector3 dir)
    {
        float targetAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + camTransform.eulerAngles.y; //Angle relative to cam
        Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        if (!isSliding)
        {
            Quaternion rotation = Quaternion.Euler(0f, targetAngle, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, .07f);
        }

        return moveDir.normalized;
    }

    private float GetSpeed(Vector3 direction)
    {
        //Change speed
        if (characterController.isGrounded)
        {
            //Crouch Movement
            if (Input.GetKey(KeyCode.LeftControl))
            {
                currentSpeed = Mathf.Clamp(currentSpeed -= currentSpeed * 4f * Time.deltaTime, 4f, 12.5f);
            }
            //Run Movement
            else
            {
                currentSpeed = Mathf.Clamp(Input.GetKey(KeyCode.LeftShift) && direction != Vector3.zero ? currentSpeed += currentSpeed * 2f * Time.deltaTime : currentSpeed -= currentSpeed * 2f * Time.deltaTime, 7.5f, 12.5f);
            }
        }

        return currentSpeed;
    }

    private Vector3 VerticalMovement()
    {
        if (characterController.isGrounded)
        {
            Vector3 castOrigin = transform.position - new Vector3(0f, characterController.height / 2 - characterController.radius - characterController.center.y, 0f);
            var sphereCast = Physics.SphereCast(castOrigin, characterController.radius - .01f, Vector3.down,
                out var hit, castOrigin.y + 0.01f, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore);
            groundAngle = Vector3.Angle(Vector3.up, hit.normal);
            if (sphereCast && groundAngle > characterController.slopeLimit)
            {
                velocity = SlideMovement(hit);
                isSliding = true;
                if (Input.GetButtonDown("Jump"))
                {
                    jumpVelocity = Mathf.Sqrt(jumpHeight * -1.5f * gravity);
                    isSliding = false;
                }
            }
            else
            {
                isSliding = false;
                slideDirectionVelocity = Vector3.zero;
                if (Input.GetButtonDown("Jump"))
                {
                    jumpVelocity = Mathf.Sqrt(jumpHeight * -4f * gravity);
                    onAirDirectionVelocity = velocity.normalized;
                }
                else if (jumpVelocity < 0)
                {
                    jumpVelocity = -9.80665f;
                }
            }
        }
        jumpVelocity += 2f * gravity * Time.deltaTime;
        velocity.y = jumpVelocity;
        return velocity;
    }

    public Vector3 SlideMovement(RaycastHit hit)
    {
        if (Mathf.Abs(slideDirectionVelocity.x) < 17.5f && Mathf.Abs(slideDirectionVelocity.z) < 17.5f)
        {
            float slideDirectionX = (1f - hit.normal.y) * hit.normal.x;
            float slideDirectionZ = (1f - hit.normal.y) * hit.normal.z;
            slideDirectionVelocity.x += slideDirectionX;
            slideDirectionVelocity.z += slideDirectionZ;
            transform.rotation = Quaternion.Euler(slideDirectionVelocity);
        }

        return new Vector3(slideDirectionVelocity.x, velocity.y, slideDirectionVelocity.z);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        velocity = groundAngle > characterController.slopeLimit ? velocity : Vector3.zero;
        onAirDirectionVelocity = Vector3.zero;
    }
}
