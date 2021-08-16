using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class worldToViewFunctionTest : MonoBehaviour
{
    public GameObject Cube;
    Camera cam;
    // Start is called before the first frame update
    void Start()
    {
        cam = GetComponent<Camera>();
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 viewPos = cam.WorldToViewportPoint(Cube.transform.position);
        Debug.Log(viewPos);
    }
}
