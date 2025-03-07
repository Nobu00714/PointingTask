using System.Diagnostics.Tracing;
using System.Xml.Schema;
using System.Data.Common;
//using System.Reflection.Metadata.Ecma335;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using uWintab;
using UnityEngine.SceneManagement;

public class CameraSizeAdjust : MonoBehaviour
{
    [SerializeField] private Camera camera;
    // Start is called before the first frame update
    void Start()
    {
        camera.orthographicSize = Screen.currentResolution.height/2;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
