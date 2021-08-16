using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VisibleTest : MonoBehaviour
{
    Transform[] gameObjsTranforms;
    Vector3[] gameObjsViewPos;
    Mesh currentMesh;
    GameObject camObj;
    Camera cam;
    Vector2 foveaCoordinate;
    float E1;
    int verticeLimit = 150000;
    float error = 50000;
    float searchStep = 0.01f;
    int frameCount = 0;

    // Use this for initialization
    void Start()
    {
        camObj = GameObject.Find("/Camera");
        cam = camObj.GetComponent<Camera>();

        gameObjsTranforms = gameObject.GetComponentsInChildren<Transform>();

        foveaCoordinate = new Vector2(0.5f,0.5f);
        E1 = 0.05f;
    }

    // Update is called once per frame
    void Update()
    {
        gameObjsViewPos = worldPos2ViewPos(cam, gameObjsTranforms);
        E1 = SearchBestEccentricity(gameObjsViewPos, E1, foveaCoordinate);
        frameCount++;
    }

    Vector3[] worldPos2ViewPos(Camera cam, Transform[] gameObjsTranforms)
    {
        Vector3[] viewPoss = new Vector3[gameObjsTranforms.Length];
        int count = 0;
        foreach (var transform in gameObjsTranforms)
        {
            Vector3 viewPos = cam.WorldToViewportPoint(transform.position);   
            viewPoss[count] = viewPos;
            count++;   
        }
        return viewPoss;
    }


    float SearchBestEccentricity(Vector3[] gameObjsViewPos, float initialE1, Vector2 foveaCoordinate)
    {
        Debug.Log("has entered into SearchBestEccentricity");
        float E1 = initialE1;
        int totalVerticesCount = 0;
        int[] totalVerticesCountBuffer = new int[3];

        while(true)
        {
            totalVerticesCount = 0;
            int index = 0;
            foreach (var viewPos in gameObjsViewPos)
            {
                // if both x and y are within the range of [0,1] and the rectangle formed by foveaCoordinate and eccentricity, current gameObj is visible to camera
                if(viewPos.x >= foveaCoordinate.x - E1 && viewPos.x <= foveaCoordinate.x + E1 && viewPos.y >= foveaCoordinate.y - E1 && viewPos.y <= foveaCoordinate.y + E1) 
                {
                    try
                    {
                        currentMesh = gameObjsTranforms[index].gameObject.GetComponent<MeshFilter>().mesh; // get its mesh
                        totalVerticesCount += currentMesh.vertexCount; 
                    }
                    catch (Exception e)
                    {
                        var str = e.Message; // to elminate the annoying warning
                    }
                }
                index++;
            }

            totalVerticesCountBuffer[0] = totalVerticesCountBuffer[1]; // this is to prevent search algorithm stuck in an specific area
            totalVerticesCountBuffer[1] = totalVerticesCountBuffer[2];
            totalVerticesCountBuffer[2] = totalVerticesCount;
            if(totalVerticesCountBuffer[0] == totalVerticesCountBuffer[2] && totalVerticesCountBuffer[1] != totalVerticesCountBuffer[2] && totalVerticesCountBuffer[2] != 0) searchStep /= 2;

            Debug.Log(string.Format("frame: {0}, E1: {1}, totalVerticesCount: {2}, search step: {3}", frameCount, E1, totalVerticesCount, searchStep));
            
            if(totalVerticesCount < verticeLimit - error) E1 += searchStep; // should increase E1 to reduce network bandwidth requirments
            else if(totalVerticesCount > verticeLimit + error) E1 -= searchStep; // should decrease E1 to maintain Client FPS
            else return E1; // return best E1
        }
    }

}
