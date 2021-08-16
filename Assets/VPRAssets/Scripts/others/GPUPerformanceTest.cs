using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

public class GPUPerformanceTest : MonoBehaviour
{
    public Camera cam;
    private FRutils _FRutils;

    GameObject borderScene; // borderScene is used for checking the border
    Renderer[] borderRenderers;
    
    List<PerformanceData> PerformanceDataset;
    Vector2 foveaCoordinate = new Vector2(0.5f, 0.5f);
    float initialE1 = 0.5f;
    float currentE1;
    float searchStep = 0.02f;
    float xMin, xMax, zMin, zMax;
    int xMoveCount, zMoveCount, yRotateCount;
    float xMoveStride, zMoveStride, yRotateStride;
    int xCount, zCount, yCount;
    float initialRotationX, initialRotationZ, initialPositionY;
    float latency;
    int frameCount = 0;
    int targetFrameRate = 60;
    bool performRendering = false; // To indicate whether last Update() perform the rendering

    // Unity profiler
    ProfilerRecorder setPassCallsRecorder;
    ProfilerRecorder drawCallsRecorder;
    ProfilerRecorder verticesRecorder;
    ProfilerRecorder trianglesRecorder;

    public class PerformanceData
    {
        public PerformanceData()
        {
        }
        public long setPassCall { get; set; }
        public long drawCall { get; set; }
        public long vertices { get; set; }
        public long triangles { get; set; }
        public float eccentricity { get; set; }
        public float latencyMilliSecond { get; set; }
        public override string ToString()
        {
            return 
            this.setPassCall.ToString() + "             " 
            + this.drawCall.ToString() + "             " 
            + this.vertices.ToString() + "             "
            + this.triangles.ToString() + "             " 
            + this.eccentricity.ToString() + "             " 
            + this.latencyMilliSecond.ToString("F4");
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = 90;

        PerformanceDataset = new List<PerformanceData>();
        
        _FRutils = new FRutils(cam);
        cam.enabled = false; // manually control rendering
        currentE1 = initialE1;
        
        // Get bounds of the moving area
        Bounds b = new Bounds();
        borderScene = GameObject.Find("/Map_v1/Static");
        borderRenderers = borderScene.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in borderRenderers)
        {
            b.Encapsulate(r.bounds); // refreshing bounds according each renderer
        }

        xMin = b.center.x - b.extents.x;
        xMax = b.center.x + b.extents.x;
        zMin = b.center.z - b.extents.z;
        zMax = b.center.z + b.extents.z;

        xMoveCount = 30;
        zMoveCount = 30;
        yRotateCount = 20;
        xMoveStride = (xMax - xMin) / xMoveCount;
        zMoveStride = (zMax - zMin) / zMoveCount;
        yRotateStride = 360 / yRotateCount;

        xCount = 0;
        zCount = 0;
        yCount = 0;

        initialPositionY = transform.position.y;
        initialRotationX = transform.rotation.x;
        initialRotationZ = transform.rotation.z;

        // Start profiling
        setPassCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        verticesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
        trianglesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
    }

    // Update is called once per frame
    void Update()
    {
        latency = Time.deltaTime;
        if(latency < 1f / targetFrameRate && performRendering)
        {
            // Add data to output text file
            if(frameCount > 0 && frameCount < xMoveCount * zMoveCount * yRotateCount + 1)
            {
                PerformanceDataset.Add(new PerformanceData{setPassCall = setPassCallsRecorder.LastValue,
                                                            drawCall = drawCallsRecorder.LastValue,
                                                            vertices = verticesRecorder.LastValue,
                                                            triangles = trianglesRecorder.LastValue,
                                                            eccentricity = currentE1,
                                                            latencyMilliSecond = latency * 1000});
            }
            
            // move to next grid points
            transform.position = new Vector3(xMin + xCount * xMoveStride, initialPositionY, zMin + zCount * zMoveStride);
            transform.rotation = Quaternion.Euler(initialRotationX, 0.0f + yCount * yRotateStride, initialRotationZ);

            yCount++;
            if(yCount == yRotateCount)
            {
                yCount = 0;
                xCount++;
            }
            if(xCount == xMoveCount)
            {
                xCount = 0;
                zCount++;
            }
            if(zCount == zMoveCount)
            {
                saveTextFile();
                Application.Quit();
            }

            frameCount++;
            currentE1 = initialE1; // reset E1 and performRendering bool
            performRendering = false;
        }
        else
        {
            currentE1 -= searchStep;
            if(currentE1 < 0) currentE1 = 0;
            // Decrease Eccentricity to perform rendering
            cam.ResetProjectionMatrix();
            _FRutils.RefreshPM(cam, foveaCoordinate, currentE1);
            cam.Render();
            performRendering = true;
        }
        
    }

    void saveTextFile()
    {
        // save results to txt file
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var item in PerformanceDataset)
        {
            sb.AppendLine(item.ToString());
        }

        Console.WriteLine(sb.ToString());
        if(Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(
                Application.dataPath, "TwoOrThreeLayerSQVRResults", "GPUPerformance.txt"), 
                sb.ToString());
        }
        else if(Application.platform == RuntimePlatform.Android)
        {
            // Go to your Player settings, for Android, Change Write access from "Internal Only" to External (SDCard). 
            // You can then use Application.persistentDataPath to get the location of your external storage path.
            // Application.persistentDataPath on android points to /storage/emulated/0/Android/data/<packagename>/files on most devices
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(
                Application.persistentDataPath, "GPUPerformance.txt"),
                sb.ToString());
        }
        Console.ReadLine();
    }
}
