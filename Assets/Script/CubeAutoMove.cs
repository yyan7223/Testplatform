using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeAutoMove : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.position += transform.right * 0.005f;
    }
}
