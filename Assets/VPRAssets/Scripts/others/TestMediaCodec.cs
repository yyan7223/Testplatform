using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class TestMediaCodec : MonoBehaviour
{
    Camera cam;
    public Material MediaCodecTest;
    AndroidJavaObject DecodeTest;
    // Start is called before the first frame update
    void Start()
    {
        cam = gameObject.GetComponent<Camera>();

        DecodeTest = new AndroidJavaObject("com.example.mediacodectest.DecodeTest");
        DecodeTest.Call("startPlayback");
    }

    // Update is called once per frame
    void Update()
    {
        //int texId = DecodeTest.Call<int>("getTextureId");
        //Debug.Log(string.Format("TexID is: {0}", texId));
        //Debug.Log(DecodeTest.Call<string>("JAVAInterfaceTest"));

    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        DecodeTest.Call("updateTexImage");
        int texId = DecodeTest.Call<int>("getTextureId");
        Debug.Log(string.Format("TexID is: {0}", texId));
        Texture2D videoTexture = Texture2D.CreateExternalTexture(960, 540, TextureFormat.RGBA32, false, false, new System.IntPtr(texId));
        saveTexture2D(videoTexture);
        MediaCodecTest.SetTexture("_MainTex", videoTexture);

        Graphics.Blit(videoTexture, destination, MediaCodecTest);
    }

    void saveTexture2D(Texture2D tex)
    {
        byte[] bytes = tex.EncodeToPNG();
        string path = "MediaCodecTest.png";
        File.WriteAllBytes(Application.persistentDataPath + path, bytes);
        // Debug.Log("Successfully saved a texture");
    }
}
