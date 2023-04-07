using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Movement : MonoBehaviour
{
    public GameObject mainCamera, rippleCamera;

    public ParticleSystem ripple;

    [Header("Movement")]
    private float moveSpeed;
    public float walkSpeed;
    public float sprintSpeed;
    public float slideSpeed;
    public float wallRunSpeed;
    private float desiredMoveSpeed;
    private float lastDesiredMoveSpeed;
    public float speedIncreaseMultiplier;
    public float slopeIncreaseMultiplier;
    public float groundDrag;
    public float waterDrag;

    [Header("Jumping")]
    public float jumpForce;
    public float jumpCoolDown;
    public float airMultiplier;
    bool readyToJump;

    [Header("Crouching")]
    public float crouchSpeed;
    public float crouchYScale;
    private float startYScale;

    [Header("Water Ripples")]
    public int rippleSpeed1;
    public int rippleSpeed2;
    public int rippleSpeed3;
    public float rippleSize1, rippleSize2, rippleSize3;
    public float rippleLifetime1, rippleLifetime2, rippleLifetime3;

    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode crouchKey = KeyCode.LeftControl;

    [Header("Ground Check")]
    public float playerHeight;
    public Transform groundCheck;
    public float groundDistance = 0.2f;
    public LayerMask ground;
    public LayerMask water;
    bool grounded;
    bool inWater;

    [Header("Slope Handling")]
    public float maxSlopeAngle;
    private RaycastHit slopeHit;
    private bool exitingSlope;

    [Header("Input")]
    float horInput;
    float vertInput;

    [Header("References")]
    public Transform orientation;
    Vector3 moveDirection;
    private Vector3 playerPos;
    Rigidbody rb;

    [SerializeField] private float velocityXZ, velocityY;

    public MovementState state;
    public enum MovementState //used to determine jumping, sprinting, air strafing and crouching
    {
        walking,
        sprinting,
        wallrunning,
        crouching,
        sliding,
        air
    }
    public bool sliding;
    public bool crouching;
    public bool wallRunning;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        readyToJump = true;

        ResetJump(); //applies cooldown and resets readyToJump to true

        startYScale = transform.localScale.y; //starting height of the player
    }
    private void Update()
    {
        //ground check
        grounded = Physics.CheckSphere(groundCheck.position, groundDistance, ground);
        inWater = Physics.CheckSphere(groundCheck.position, playerHeight * 0.5f + 0.4f, water);

        Debug.Log("grounded: " + grounded);
        Debug.Log("inWater: " + inWater);

        myInput();
        SpeedControl();
        StateHandler(); //changes the movement state

        velocityXZ = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(playerPos.x, 0, playerPos.z));
        velocityY = Vector3.Distance(new Vector3(0, transform.position.y, 0), new Vector3(0, playerPos.y, 0));
        playerPos = transform.position;

        rippleCamera.transform.position = transform.position + Vector3.up * 10;
        Shader.SetGlobalVector("_Player", transform.position);

        //handle drag
        if (grounded)
            rb.drag = groundDrag;
        else if (inWater)
            rb.drag = waterDrag;
        else
            rb.drag = 0;
    }
    private void emissionParams(int speed, float size, float lifetime)
    {
        var emitParams = new ParticleSystem.EmitParams();
        emitParams.position = ripple.transform.position + ripple.transform.forward * 0.5f;
        emitParams.velocity = ripple.transform.forward * speed;
        emitParams.startSize = size;
        emitParams.startLifetime = lifetime;
        emitParams.startColor = Color.white;
        ripple.Emit(emitParams, 1);
        //position, velocity, size, lifetime and color
    }
    void createRipple(int start, int end, int delta, int speed, float size, float lifetime)
    {
        Vector3 forward = ripple.transform.eulerAngles;
        forward.y = start;
        ripple.transform.eulerAngles = forward;

        for (int i = start; i < end; i += delta)
        {
            emissionParams(speed, size, lifetime); //seperate function to emit particle using modified parameters
            ripple.transform.eulerAngles += Vector3.up * 3;
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == 4 && velocityY > 0.03f)
        {
            createRipple(-180, 180, 3, rippleSpeed1, rippleSize1, rippleLifetime1);
        }
    }
    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.layer == 4 && velocityXZ > 0.02f && Time.renderedFrameCount % 5 == 0)
        {
            int y = (int)transform.eulerAngles.y;
            createRipple(y-90, y+90, 3, rippleSpeed2, rippleSize2, rippleLifetime2);
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == 4 && velocityY > 0.03f)
        {
            createRipple(-100, 100, 3, rippleSpeed3, rippleSize3, rippleLifetime3);
        }
    }
    private void FixedUpdate()
    {
        MovePlayer();
    }
    private void myInput()
    {
        horInput = Input.GetAxisRaw("Horizontal");
        vertInput = Input.GetAxisRaw("Vertical");

        //when to jump
        if (Input.GetKey(jumpKey) && readyToJump && grounded)
        {
            readyToJump = false;

            Jump();

            if (grounded)
                Invoke(nameof(ResetJump), jumpCoolDown);
        }
        //start crouching
        if (Input.GetKeyDown(crouchKey))
        {
            transform.localScale = new Vector3(transform.localScale.x, crouchYScale, transform.localScale.z);
            rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
        }
        //stop crouching
        if (Input.GetKeyUp(crouchKey))
        {
            transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
        }
    }
    private void StateHandler()
    {
        //mode - wallrunning
        if(wallRunning)
        {
            state = MovementState.wallrunning;
            desiredMoveSpeed = wallRunSpeed;
        }

        //mode - sliding
        if(sliding)
        {
            state = MovementState.sliding;

            if (OnSlope() && rb.velocity.y < 0.1f)
                desiredMoveSpeed = slideSpeed;

            else
            {
                desiredMoveSpeed = sprintSpeed;
            }
        }

        //mode - crouching
        else if (Input.GetKey(crouchKey))
        {
            state = MovementState.crouching;
            desiredMoveSpeed = crouchSpeed;
        }
        //mode - sprinting
        else if(grounded && Input.GetKey(sprintKey))
        {
            state = MovementState.sprinting;
            desiredMoveSpeed = sprintSpeed;
        }

        //mode - walking
        else if (grounded)
        {
            state = MovementState.walking;
            desiredMoveSpeed = walkSpeed;
        }

        //mode - air
        else 
        {
            state = MovementState.air;
        }
        //check if difference has changed significantly
        if(Mathf.Abs(desiredMoveSpeed - lastDesiredMoveSpeed) > 4f && moveSpeed != 0)
        {
            StopAllCoroutines();
            StartCoroutine(lerpMoveSpeed());
        }
        else
        {
            moveSpeed = desiredMoveSpeed;
        }

        lastDesiredMoveSpeed = desiredMoveSpeed;
    }
    private IEnumerator lerpMoveSpeed()
    {
        //smoothly transition movespeed to desired value
        float time = 0;
        float difference = Mathf.Abs(desiredMoveSpeed - moveSpeed);
        float startValue = moveSpeed;

        while (time < difference)
        {
            moveSpeed = Mathf.Lerp(startValue, desiredMoveSpeed, time / difference);
            
            if(OnSlope())
            {
                float slopeAngle = Vector3.Angle(Vector3.up, slopeHit.normal);
                float slopeAngleIncrease = 1 + (slopeAngle / 90f);

                time += Time.deltaTime * speedIncreaseMultiplier * slopeIncreaseMultiplier * slopeAngleIncrease;
            }
            else
                time += Time.deltaTime * speedIncreaseMultiplier;

            yield return null;
        }
        moveSpeed = desiredMoveSpeed;
    }
    private void MovePlayer()
    {
        //calculate movement direction
        moveDirection = orientation.forward * vertInput + orientation.right * horInput;

        //on ground
        if(grounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
        //on slope
        if (OnSlope() && !exitingSlope)
        {
            rb.AddForce(GetSlopeMoveDirection(moveDirection) * moveSpeed * 20f, ForceMode.Force);

            if (rb.velocity.y > 0)
                rb.AddForce(Vector3.down * 80f, ForceMode.Force);
        }
        //in air
        else if (!grounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);

        //prevents sliding while on slope by turning off gravity
        if(wallRunning) rb.useGravity = !OnSlope();
    }
    private void SpeedControl()
    {
        //limiting speed on slope
        if(OnSlope() && !exitingSlope)
        {
            if (rb.velocity.magnitude > moveSpeed)
                rb.velocity = rb.velocity.normalized * moveSpeed;
        }
        //limiting speed on ground or in air
        else
        {
            Vector3 flatVal = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            //limit velocity if needed
            if (flatVal.magnitude > moveSpeed)
            {
                Vector3 limitedVal = flatVal.normalized * moveSpeed;
                rb.velocity = new Vector3(limitedVal.x, rb.velocity.y, limitedVal.z);
            }
        }
    }
    private void Jump()
    {
        exitingSlope = true;

        //reset y velocity
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
    }
    private void ResetJump()
    {
        readyToJump = true;

        exitingSlope = false;
    }
    public bool OnSlope()
    {
        if(Physics.Raycast(transform.position, Vector3.down, out slopeHit, startYScale * 0.5f + 0.3f))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle < maxSlopeAngle && angle != 0;
        }

        return false; //if raycast doesn't hit anything
    }
    public Vector3 GetSlopeMoveDirection(Vector3 direction)
    {
        return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
    }

}
