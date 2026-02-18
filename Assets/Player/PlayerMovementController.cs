using System;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerMovementController : MonoBehaviour
{
    public CharacterController characterController;
    public Transform camTransform;

    [Header("Movement Parameters")]
    [SerializeField] private float currentSpeed;
    [SerializeField] private Vector3 velocity;
    public bool isWalkingUphill;

    [Header("Jump Parameters")]
    [SerializeField] private float gravity;
    [SerializeField] private float jumpHeight;
    public float jumpVelocity;
    [SerializeField] private Vector3 onAirDirectionVelocity;

    [Header("Slide Parameters")]
    [SerializeField] private float groundAngle;
    [SerializeField] private Vector3 slideDirectionVelocity;
    public bool isSliding;
    private bool sphereCast;
    private RaycastHit hit;
    private float dotProduct;

    [Header("Roll Parameters")]
    private const float rollSpeed = 10f;
    private const float rollDuration = 0.65f;
    private const float rollCooldown = 0.65f;
    public bool isRolling;
    public bool rollAnimation;
    private float rollTimer;
    private float rollCooldownTimer;
    private Vector3 rollDirection;

    private const float MIN_SPEED = 4f;
    private const float WALK_SPEED = 7.5f;
    private const float RUN_SPEED = 12.5f;
    private const float SLOPE = 35f;
    private const float SLIDE_SPEED_LIMIT = 17.5f;
    private const float ROTATION_SPEED = 10f; // Added for smoother rotation

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Cache input values
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");
        bool isCrouching = Input.GetKey(KeyCode.C);
        bool isJumping = Input.GetButtonDown("Jump");
        bool isRollingInput = Input.GetKey(KeyCode.LeftControl);

        UpdateGroundAngle();

        if (isRolling)
        {
            rollAnimation = false;
            HandleRoll(isJumping);
        }
        else
        {
            if (rollCooldownTimer > 0f)
            {
                rollCooldownTimer -= Time.deltaTime;
            }

            if (isRollingInput && rollCooldownTimer <= 0f && characterController.isGrounded && !isSliding)
            {
                rollAnimation = true;
                Vector3 direction = new Vector3(horizontalInput, 0f, verticalInput).normalized;
                StartRoll(direction);
            }
            else
            {
                HandleMovement(horizontalInput, verticalInput, isCrouching, isJumping);
            }
        }
    }

    private void HandleMovement(float horizontalInput, float verticalInput, bool isCrouching, bool isJumping)
    {
        if (horizontalInput != 0 || verticalInput != 0)
        {
            Vector3 forwardMovement = DirectionMovement(horizontalInput, verticalInput, isCrouching);
            Vector3 directionMovement = new Vector3(forwardMovement.x, VerticalMovement(isJumping, isCrouching).y, forwardMovement.z);
            characterController.Move(directionMovement * Time.deltaTime);
        }
        else
        {
            characterController.Move(VerticalMovement(isJumping, isCrouching) * Time.deltaTime);
            currentSpeed = MIN_SPEED;
            isWalkingUphill = false;
        }
    }

    //Check ground angle
    private void UpdateGroundAngle()
    {
        if (characterController.isGrounded)
        {
            Vector3 castOrigin = transform.position - new Vector3(0f, characterController.height / 2 - characterController.radius - characterController.center.y, 0f);
            sphereCast = Physics.SphereCast(castOrigin, characterController.radius - 0.01f, Vector3.down,
                out hit, castOrigin.y + 0.01f, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore);
            groundAngle = Vector3.Angle(Vector3.up, hit.normal);
        }
    }

    private Vector3 DirectionMovement(float horizontalInput, float verticalInput, bool isCrouching)
    {
        Vector3 direction = GetMovementDirection(new Vector3(horizontalInput, 0f, verticalInput).normalized);
        float speed = GetSpeed(direction, isCrouching);
        dotProduct = Vector3.Dot(direction, hit.normal);

        velocity = CalculateVelocity(direction, speed, isCrouching);
        return velocity;
    }

    private Vector3 CalculateVelocity(Vector3 direction, float speed, bool isCrouching)
    {
        if (groundAngle > characterController.slopeLimit && direction.magnitude != 0f)
        {
            return direction * speed * 0.6f + slideDirectionVelocity; // slide slope movement
        }
        if (groundAngle > 35 && isCrouching && dotProduct > 0f && direction.magnitude != 0f && !isRolling)
        {
            return direction * speed * 0.6f + slideDirectionVelocity; // slide forced movement
        }
        if (onAirDirectionVelocity != Vector3.zero)
        {
            return direction * speed * 0.3f + onAirDirectionVelocity * speed * 0.7f; // walk jump movement
        }
        if (direction.magnitude != 0f && !characterController.isGrounded)
        {
            return direction * speed * 0.6f; // idle jump movement
        }
        if (direction.magnitude != 0f)
        {
            return direction * speed; // walking movement
        }
        return Vector3.zero; // idle
    }

    private Vector3 GetMovementDirection(Vector3 direction)
    {
        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + camTransform.eulerAngles.y; //Angle relative to cam
        Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

        if (!isSliding)
        {
            RotatePlayerTowards(targetAngle);
        }

        return moveDir.normalized;
    }

    private void StartRoll(Vector3 direction)
    {
        isRolling = true;
        rollTimer = rollDuration;
        rollCooldownTimer = rollCooldown;
        rollDirection = GetMovementDirection(direction);

        if (rollDirection == Vector3.zero) // If no movement input, roll forward
        {
            rollDirection = transform.forward;
        }

        float targetAngle = Mathf.Atan2(rollDirection.x, rollDirection.z) * Mathf.Rad2Deg;
        RotatePlayerTowards(targetAngle);
    }

    private void HandleRoll(bool isJumping)
    {
        //Jump while rolling
        if (isJumping)
        {
            isRolling = false;
            Vector3 forwardMovement = DirectionMovement(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"), false);
            characterController.Move(new Vector3(rollDirection.x + forwardMovement.x * rollSpeed, VerticalMovement(isJumping, false).y, rollDirection.z + forwardMovement.z * rollSpeed) * Time.deltaTime);
        }
        else if (rollTimer > 0)
        {
            rollDirection.y = VerticalMovement(false, false).y;
            characterController.Move(rollDirection * rollSpeed * Time.deltaTime);
            rollTimer -= Time.deltaTime;
        }
        else
        {
            isRolling = false;
        }
    }

    private void RotatePlayerTowards(float targetAngle)
    {
        Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
        if (isRolling)
        {
            transform.rotation = targetRotation;
        }
        else
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, ROTATION_SPEED * Time.deltaTime);
        }
    }

    private float GetSpeed(Vector3 direction, bool isCrouching)
    {
        if (!characterController.isGrounded) return currentSpeed;

        if (isCrouching && direction != Vector3.zero)
        {
            isWalkingUphill = false;
            currentSpeed = Mathf.Max(MIN_SPEED, currentSpeed - currentSpeed * 2f * Time.deltaTime);
        }
        else if (dotProduct < 0f && groundAngle > SLOPE && direction != Vector3.zero)
        {
            isWalkingUphill = true;
            currentSpeed = Mathf.Max(MIN_SPEED, currentSpeed - currentSpeed * 2f * Time.deltaTime);
        }
        else
        {
            isWalkingUphill = false;
            float targetSpeed = Input.GetKey(KeyCode.LeftShift) && direction != Vector3.zero ? RUN_SPEED : WALK_SPEED;
            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, 2f * Time.deltaTime);
        }
        return currentSpeed;
    }

    private Vector3 VerticalMovement(bool isJumping, bool isCrouching)
    {
        if (characterController.isGrounded)
        {
            bool shouldSlide = (sphereCast && groundAngle > characterController.slopeLimit) || 
                               (sphereCast && groundAngle > 35 && isCrouching && (velocity.Equals(Vector3.zero) || dotProduct > 0f));

            if (shouldSlide)
            {
                velocity = SlideMovement();
                isSliding = true;
                if (isJumping && !isRolling)
                {
                    isSliding = false;
                    jumpVelocity = Mathf.Sqrt(jumpHeight * -1.5f * gravity);
                }
            }
            else
            {
                isSliding = false;
                slideDirectionVelocity = Vector3.zero;
                if (isJumping)
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

    public Vector3 SlideMovement()
    {
        if (Mathf.Abs(slideDirectionVelocity.x) < SLIDE_SPEED_LIMIT && Mathf.Abs(slideDirectionVelocity.z) < SLIDE_SPEED_LIMIT && !isRolling)
        {
            float slideDirectionX = (1f - hit.normal.y) * hit.normal.x;
            float slideDirectionZ = (1f - hit.normal.y) * hit.normal.z;
            slideDirectionVelocity.x += slideDirectionX;
            slideDirectionVelocity.z += slideDirectionZ;
            RotatePlayerTowards(Mathf.Atan2(slideDirectionVelocity.x, slideDirectionVelocity.z) * Mathf.Rad2Deg);
        }

        return new Vector3(slideDirectionVelocity.x, velocity.y, slideDirectionVelocity.z);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (groundAngle <= characterController.slopeLimit)
        {
            velocity = Vector3.zero;
        }
        onAirDirectionVelocity = Vector3.zero;
    }
}
