using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float moveSpeed = 50f;
    public float lookSpeed = 2f;
    public float zoomSpeed = 10f;

    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    public ProceduralTerrain terrain;
    public float minHeightAboveTerrain = 30f;

    void LateUpdate()
    {
        Vector3 pos = transform.position;

        float terrainHeight = terrain.GetHeightAtPosition(pos.x, pos.z);

        if (pos.y < terrainHeight + minHeightAboveTerrain)
        {
            pos.y = terrainHeight + minHeightAboveTerrain;
            transform.position = pos;
        }

        transform.position = pos;
    }

    void Update()
    {
        rotationX += Input.GetAxis("Mouse X") * lookSpeed;
        rotationY -= Input.GetAxis("Mouse Y") * lookSpeed;
        rotationY = Mathf.Clamp(rotationY, -90f, 90f);

        transform.rotation = Quaternion.Euler(rotationY, rotationX, 0);

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        float moveY = 0;

        if (Input.GetKey(KeyCode.E)) moveY = 1;
        if (Input.GetKey(KeyCode.Q)) moveY = -1;

        Vector3 move = transform.right * moveX + transform.up * moveY + transform.forward * moveZ;
        transform.position += move * moveSpeed * Time.deltaTime;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            Camera.main.fieldOfView -= scroll * zoomSpeed;
            Camera.main.fieldOfView = Mathf.Clamp(Camera.main.fieldOfView, 20, 100);
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }
}