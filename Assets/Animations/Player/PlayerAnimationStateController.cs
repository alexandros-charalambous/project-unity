
using UnityEngine;

public class AnimationStateController : MonoBehaviour
{

    PlayerMovementController playerMovementController;
    Animator animator;
    int isWalkingHash;
    int isRunningHash;
    int isJumpingHash;
    int isGroundedHash;
    int isSlidingHash;
    int isCrouchingHash;
    int isRollingHash;

    void Start()
    {
        animator = GetComponent<Animator>();
        playerMovementController = GetComponentInParent<PlayerMovementController>();

        isWalkingHash = Animator.StringToHash("isWalking");
        isRunningHash = Animator.StringToHash("isRunning");
        isJumpingHash = Animator.StringToHash("isJumping");
        isGroundedHash = Animator.StringToHash("isGrounded");
        isSlidingHash = Animator.StringToHash("isSliding");
        isCrouchingHash = Animator.StringToHash("isCrouching");
        isRollingHash = Animator.StringToHash("isRolling");
    }

    // Update is called once per frame
    void Update()
    {
        bool isWalking = Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0;
        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        bool isJumping = Input.GetKeyDown(KeyCode.Space);
        bool isGrounded = playerMovementController.characterController.isGrounded;
        bool isSliding = playerMovementController.isSliding;
        bool isCrouching = Input.GetKey(KeyCode.C);
        bool isRolling = playerMovementController.rollAnimation;
        bool isWalkingUphill = playerMovementController.isWalkingUphill;


        animator.SetBool(isGroundedHash, isGrounded);

        if (isRolling)
        {
            animator.SetBool(isRollingHash, true);
        }
        else
        {
            animator.SetBool(isRollingHash, false);
        }

        if (isJumping)
        {
            animator.SetBool(isJumpingHash, true);
        }
        else
        {
            animator.SetBool(isJumpingHash, false);
        }

        if (isSliding)
        {
            animator.SetBool(isSlidingHash, true);
        }
        else
        {
            animator.SetBool(isSlidingHash, false);
        }

        if (isCrouching)
        {
            animator.SetBool(isCrouchingHash, true);
        }
        else
        {
            animator.SetBool(isCrouchingHash, false);
        }

        if (isWalking)
        {
            animator.SetBool(isWalkingHash, true);
            if (isWalkingUphill)
            {
                animator.speed = .5f;
            }
        }
        else
        {
            animator.SetBool(isWalkingHash, false);
            animator.speed = 1f;
        }

        if (isWalking && isRunning && !isWalkingUphill)
        {
            animator.SetBool(isRunningHash, true);
        }
        else
        {
            animator.SetBool(isRunningHash, false);
        }

    }
}
