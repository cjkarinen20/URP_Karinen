using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public GameObject mainCamera, rippleCamera;
    public CharacterController controller;

    public ParticleSystem ripple; 

    public float speed = 12f;
    public int rippleSpeed1, rippleSpeed2, rippleSpeed3;
    public float rippleSize1, rippleSize2, rippleSize3;
    public float rippleLifetime1, rippleLifetime2, rippleLifetime3;


    [SerializeField] private float velocityXZ, velocityY;

    private Vector3 playerPos;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;

        controller.Move(move * speed * Time.deltaTime);

        velocityXZ = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(playerPos.x, 0, playerPos.z));
        velocityY = Vector3.Distance(new Vector3(0, transform.position.y, 0), new Vector3(0, playerPos.y, 0));
        playerPos = transform.position;

        rippleCamera.transform.position = transform.position + Vector3.up * 10;
        Shader.SetGlobalVector("_Player", transform.position);

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
}
